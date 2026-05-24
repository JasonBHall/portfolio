using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StarshipSimulation.Shared.Economy;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;
using StarshipSimulation.Shared.Entities.Orders;

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Runs the trade scheduling algorithm each macro tick.
    ///
    /// Responsibilities:
    ///   - Scan the logistics pool (entities with LogisticsComponent.IsAvailable)
    ///   - Gather resource needs from all ProductionComponents
    ///   - Gather resource surpluses from all ProductionComponents
    ///   - Run the token-pass need-based scheduler
    ///   - Evaluate idle ships by throughput (units / travel seconds)
    ///   - Award contracts, create TradeJobs, assign 4-step orders
    ///   - Track in-transit amounts to prevent clustering
    ///
    /// Does NOT:
    ///   - Execute movement (MovementSystem)
    ///   - Execute load/unload transfers (OrderSystem)
    ///   - Tick production (ProductionSystem)
    ///
    /// See Core Truths — Economy System, Trade Scheduling Algorithm.
    /// </summary>
    public class TradeSystem
    {
        private readonly UniverseService  _universe;
        private readonly NavigationSystem _navigation;

        // Active jobs — keyed by job Id
        public Dictionary<string, TradeJob> Jobs { get; } = new();

        // Cap active jobs to prevent runaway scheduling
        private const int MaxActiveJobs = 1024;

        // Small job lot threshold — below this many units, prefer smallest capable ship
        // Expressed in resource units. Ships with smallest free slot count that can carry
        // the batch are preferred. Falls back to any available ship if none small enough.
        private const float SmallJobStackThreshold = 1.0f;  // 1 stack or less = small job

        public TradeSystem(UniverseService universe, NavigationSystem navigation)
        {
            _universe   = universe;
            _navigation = navigation;
        }

        // ============================================================
        // TICK — called by UniverseService.RunMacroTick()
        // ============================================================

        public void Tick(double deltaSeconds)
        {
            // Step 1 — Any available logistics entities?
            var availableShips = GetAvailableShips();
            if (availableShips.Count == 0) return;

            CleanupJobs();

            if (Jobs.Values.Count(j => j.IsActive) >= MaxActiveJobs) return;

            // Step 2 — Any surplus in the network?
            var inTransit = BuildInTransitMap();
            var surpluses = BuildSurplusMap();
            if (surpluses.Count == 0) return;

            // Step 3 — Any serviceable needs? (pre-filtered to only those with surplus)
            var allNeeds = GatherNeeds(inTransit);
            if (allNeeds.Count == 0) return;

            var needs = allNeeds
                .Where(n => surpluses.ContainsKey(n.Resource))
                .OrderBy(n => n.NeedScore)
                .ToList();

            if (needs.Count == 0) return;

            // Step 4 — Token-pass: assign each need to the highest-throughput entity
            var remainingShips    = new List<Entity>(availableShips);
            var spokenForThisTick = new Dictionary<string, int>();

            foreach (var need in needs)
            {
                if (remainingShips.Count == 0) break;

                // Surplus existence guaranteed by pre-filter above.
                // Still fetch the list — it may have been depleted by earlier assignments.
                if (!surpluses.TryGetValue(need.Resource, out var providers))
                    continue;

                // Deduct amounts already spoken for this tick
                var availableProviders = providers
                    .Select(p =>
                    {
                        string key     = $"{p.EntityId}:{need.Resource}";
                        int spokenFor  = spokenForThisTick.GetValueOrDefault(key, 0);
                        int netSurplus = Math.Max(0, p.AmountUnits - spokenFor);
                        return (p.EntityId, AmountUnits: netSurplus);
                    })
                    .Where(p => p.AmountUnits > 0)
                    .ToList();

                if (availableProviders.Count == 0) continue;  // fully spoken for this tick

                // Evaluate each available ship against each provider
                var evaluations = EvaluateShips(
                    remainingShips, availableProviders, need);

                if (evaluations.Count == 0) continue;

                // Apply small-job-lot preference
                var winner = SelectWinner(evaluations, need.Resource);
                if (winner == null) continue;

                // Create job and assign order
                var job = CreateAndAssignJob(
                    winner.Ship, winner.ProviderEntityId,
                    need.EntityId, need.Resource, winner.AmountUnits);

                if (job == null) continue;

                // Mark amounts as spoken for so subsequent needs this tick don't double-book
                string spokKey = $"{winner.ProviderEntityId}:{need.Resource}";
                spokenForThisTick[spokKey] =
                    spokenForThisTick.GetValueOrDefault(spokKey, 0) + winner.AmountUnits;

                remainingShips.Remove(winner.Ship);

                Console.WriteLine(
                    $"[Trade] Job {job.Id[..8]}: {winner.AmountUnits} {need.Resource} " +
                    $"from {winner.ProviderEntityId[..8]} → {need.EntityId[..8]} " +
                    $"via {winner.Ship.Name}");
            }
        }

        // ============================================================
        // AVAILABLE SHIPS
        // ============================================================

        private List<Entity> GetAvailableShips()
        {
            var result = new List<Entity>();

            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                var logistics = entity.GetComponent<LogisticsComponent>();
                if (logistics == null || !logistics.IsAvailable) continue;

                var stats = entity.GetComponent<ShipStatsComponent>();
                if (stats == null) continue;

                // Force fresh recalculation — TransferIn may have dirtied the cache
                // earlier this macro tick before MovementSystem ran.
                stats.Tick(entity, 0);
                if (stats.FreeStackSlots <= 0) continue;

                result.Add(entity);
            }

            return result;
        }

        // ============================================================
        // NEED GATHERING
        // ============================================================

        /// <summary>
        /// Gathers all resource needs from all production components in the universe.
        /// Each input bunker reports independently — a 4-input factory reports 4 needs.
        /// Sorted by needScore ascending (lower = more urgent).
        /// </summary>
        private List<ResourceNeed> GatherNeeds(
            Dictionary<string, Dictionary<string, int>> inTransitMap)
        {
            var needs = new List<ResourceNeed>();

            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                foreach (var prod in entity.GetAllComponents()
                                           .OfType<ProductionComponent>()
                                           .Where(p => p.IsOperational &&
                                                       p.ActiveRecipe != null &&
                                                       !p.ActiveRecipe.IsExtractor))
                {
                    // Build the in-transit dict for this specific entity
                    var entityInTransit = new Dictionary<string, int>();
                    if (inTransitMap.TryGetValue(entity.Id, out var resMap))
                        entityInTransit = resMap;

                    foreach (var need in prod.GetNeeds(entity.Id, entityInTransit))
                        needs.Add(need);
                }
            }

            // Sort by urgency — lowest needScore = most empty = highest priority
            return needs.OrderBy(n => n.NeedScore).ToList();
        }

        // ============================================================
        // SURPLUS MAP
        // ============================================================

        /// <summary>
        /// Builds a map of all available surpluses in the universe.
        /// Key: resource name → list of entities with surplus of that resource.
        /// Amounts are gross (before spoken-for deduction — that happens in the tick loop).
        /// </summary>
        private Dictionary<string, List<ResourceSurplus>> BuildSurplusMap()
        {
            var map = new Dictionary<string, List<ResourceSurplus>>();

            // Build spoken-for map from active jobs (already committed this tick)
            var committedByProvider = new Dictionary<string, int>();
            foreach (var job in Jobs.Values.Where(j => j.IsActive))
            {
                string key = $"{job.FromEntityId}:{job.Resource}";
                committedByProvider[key] =
                    committedByProvider.GetValueOrDefault(key, 0) + job.Amount;
            }

            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                foreach (var prod in entity.GetAllComponents()
                                           .OfType<ProductionComponent>()
                                           .Where(p => p.IsOperational &&
                                                       p.ActiveRecipe != null))
                {
                    var spokenFor = new Dictionary<string, int>();

                    foreach (var (resource, _) in prod.ActiveRecipe!.Outputs)
                    {
                        string key = $"{entity.Id}:{resource}";
                        if (committedByProvider.TryGetValue(key, out int amount))
                            spokenFor[resource] = amount;
                    }

                    foreach (var surplus in prod.GetSurpluses(entity.Id, spokenFor))
                    {
                        if (!map.ContainsKey(surplus.Resource))
                            map[surplus.Resource] = new List<ResourceSurplus>();

                        map[surplus.Resource].Add(surplus);
                    }
                }
            }

            return map;
        }

        // ============================================================
        // IN-TRANSIT MAP
        // ============================================================

        /// <summary>
        /// Builds a map of resources already in transit to each entity.
        /// Used in need scoring to count in-flight deliveries against current need.
        /// Structure: toEntityId → resource → units in transit
        /// </summary>
        private Dictionary<string, Dictionary<string, int>> BuildInTransitMap()
        {
            var map = new Dictionary<string, Dictionary<string, int>>();

            foreach (var job in Jobs.Values.Where(j => j.IsActive))
            {
                if (!map.ContainsKey(job.ToEntityId))
                    map[job.ToEntityId] = new Dictionary<string, int>();

                var resMap = map[job.ToEntityId];
                resMap[job.Resource] = resMap.GetValueOrDefault(job.Resource, 0) + job.Amount;
            }

            return map;
        }

        // ============================================================
        // SHIP EVALUATION
        // ============================================================

        private record ShipEvaluation(
            Entity Ship,
            string ProviderEntityId,
            int    AmountUnits,
            int    FreeSlots,
            double Throughput);

        /// <summary>
        /// Evaluates each available ship against each provider for a given need.
        /// Returns a list of viable evaluations sorted by throughput descending.
        ///
        /// Throughput = deliverable_units / travel_seconds
        /// travel_seconds = ship→provider leg + provider→requester leg
        /// </summary>
        private List<ShipEvaluation> EvaluateShips(
            List<Entity>                          ships,
            List<(string EntityId, int AmountUnits)> providers,
            ResourceNeed                          need)
        {
            var evaluations = new List<ShipEvaluation>();

            // Get stack size for this resource
            int stackSize = 1;
            if (ResourceRegistry.TryGet(need.Resource, out var resDef) && resDef != null)
                stackSize = Math.Max(1, resDef.StackSize);

            foreach (var ship in ships)
            {
                var stats    = ship.GetComponent<ShipStatsComponent>();
                if (stats == null) continue;

                int freeSlots = stats.FreeStackSlots;
                if (freeSlots <= 0) continue;

                // Check ship's cargo modules accept this resource
                bool canCarry = ship.GetAllComponents()
                    .OfType<CargoModule>()
                    .Any(c => c.IsOperational && c.CanAccept(need.Resource) && c.FreeSlots > 0);
                if (!canCarry) continue;

                int shipCapacityUnits = freeSlots * stackSize;

                // Evaluate against best provider
                string  bestProviderId = "";
                int     bestDeliverable = 0;
                double  bestThroughput  = 0.0;

                foreach (var (providerId, surplusUnits) in providers)
                {
                    if (surplusUnits <= 0) continue;

                    int deliverable = Math.Min(shipCapacityUnits,
                                     Math.Min(surplusUnits, need.AmountUnits));
                    if (deliverable <= 0) continue;

                    var providerEntity = _universe.GetEntity(providerId);
                    if (providerEntity == null) continue;

                    // leg1: ship current position → provider (not cached, ship moves)
                    double leg1 = _navigation.EstimateTravelTime(
                        ship.Position, providerEntity.Position, stats);

                    // leg2: provider → requester (cached, both static entities)
                    double leg2 = _navigation.EstimateTravelTimeBetweenEntities(
                        providerId, need.EntityId, stats);

                    double totalSeconds = leg1 + leg2;
                    if (double.IsInfinity(totalSeconds) || totalSeconds <= 0.0001)
                        totalSeconds = 0.0001;

                    double throughput = (double)deliverable / totalSeconds;

                    if (throughput > bestThroughput)
                    {
                        bestThroughput  = throughput;
                        bestDeliverable = deliverable;
                        bestProviderId  = providerId;
                    }
                }

                if (bestProviderId.Length > 0 && bestDeliverable > 0)
                {
                    evaluations.Add(new ShipEvaluation(
                        ship, bestProviderId, bestDeliverable, freeSlots, bestThroughput));
                }
            }

            return evaluations;
        }

        // ============================================================
        // WINNER SELECTION — small job lot preference
        // ============================================================

        private ShipEvaluation? SelectWinner(
            List<ShipEvaluation> evaluations,
            string               resource)
        {
            if (evaluations.Count == 0) return null;

            int stackSize = 1;
            if (ResourceRegistry.TryGet(resource, out var resDef) && resDef != null)
                stackSize = Math.Max(1, resDef.StackSize);

            int    maxDeliverable    = evaluations.Max(e => e.AmountUnits);
            float  maxDelivStacks    = (float)maxDeliverable / stackSize;
            bool   isSmallJobLot     = maxDelivStacks < SmallJobStackThreshold;

            if (isSmallJobLot)
            {
                // Prefer smallest-capacity ship that can carry the batch
                int smallestSlots = evaluations.Min(e => e.FreeSlots);
                var smallCandidates = evaluations
                    .Where(e => e.FreeSlots == smallestSlots)
                    .ToList();

                if (smallCandidates.Count > 0)
                    return smallCandidates.OrderByDescending(e => e.Throughput).First();
            }

            // Standard — highest throughput wins
            return evaluations.OrderByDescending(e => e.Throughput).First();
        }

        // ============================================================
        // JOB CREATION AND ORDER ASSIGNMENT
        // ============================================================

        private TradeJob? CreateAndAssignJob(
            Entity ship,
            string fromEntityId,
            string toEntityId,
            string resource,
            int    amount)
        {
            var fromEntity = _universe.GetEntity(fromEntityId);
            var toEntity   = _universe.GetEntity(toEntityId);
            if (fromEntity == null || toEntity == null) return null;

            // Create job
            var job = new TradeJob
            {
                Resource      = resource,
                Amount        = amount,
                FromEntityId  = fromEntityId,
                ToEntityId    = toEntityId,
                AssignedShipId = ship.Id,
                Status        = TradeJobStatus.Assigned
            };
            job.AdvanceStatus(TradeJobStatus.Assigned);
            Jobs[job.Id] = job;

            // Assign LogisticsComponent job
            var logistics = ship.GetComponent<LogisticsComponent>()!;
            logistics.AssignJob(job.Id,
                $"Delivering {amount} {resource} → {toEntity.Name}");

            // Build 4-step order and clear any stale route from previous order
            ship.CurrentOrder          = BuildTradeOrder(job);
            ship.ActiveRoute           = null;
            ship.ActiveSegmentIndex    = 0;
            ship.SegmentElapsedSeconds = 0;
            ship.HasArrived            = false;
            ship.TraversalPlan         = null;
            ship.MarkDirty();

            // Update logs
            ship.Log.SetIntent(logistics.CurrentJobSummary ?? "");
            ship.Log.SetStatus($"Moving to {fromEntity.Name}");
            ship.Log.Event("Trade",
                $"Job {job.Id[..8]}: collect {amount} {resource} " +
                $"from {fromEntity.Name} → deliver to {toEntity.Name}");

            toEntity.Log.Info("Trade",
                $"Incoming delivery: {amount} {resource} via {ship.Name}");

            return job;
        }

        private static Order BuildTradeOrder(TradeJob job) => new()
        {
            Type  = OrderType.LoadUnload,
            Steps = new List<OrderStep>
            {
                // Step 1: move to provider
                new() { StepType = StepType.MoveTo, TargetEntityId = job.FromEntityId },

                // Step 2: load at provider
                new()
                {
                    StepType       = StepType.Interact,
                    Action         = "Load",
                    TargetEntityId = job.FromEntityId,
                    Parameters     = new Dictionary<string, string>
                    {
                        ["resource"] = job.Resource,
                        ["amount"]   = job.Amount.ToString(),
                        ["jobId"]    = job.Id
                    }
                },

                // Step 3: move to requester
                new() { StepType = StepType.MoveTo, TargetEntityId = job.ToEntityId },

                // Step 4: unload at requester
                new()
                {
                    StepType       = StepType.Interact,
                    Action         = "Unload",
                    TargetEntityId = job.ToEntityId,
                    Parameters     = new Dictionary<string, string>
                    {
                        ["resource"] = job.Resource,
                        ["amount"]   = job.Amount.ToString(),
                        ["jobId"]    = job.Id
                    }
                }
            }
        };

        // ============================================================
        // JOB LIFECYCLE
        // ============================================================

        /// <summary>
        /// Marks a job as InTransit (ship loaded, en route to requester).
        /// Called by OrderSystem after a successful Load step.
        /// </summary>
        public void MarkJobInTransit(string jobId)
        {
            if (Jobs.TryGetValue(jobId, out var job))
                job.AdvanceStatus(TradeJobStatus.InTransit);
        }

        /// <summary>
        /// Marks a job as Completed.
        /// Called by OrderSystem after a successful Unload step.
        /// </summary>
        public void MarkJobCompleted(string jobId)
        {
            if (Jobs.TryGetValue(jobId, out var job))
            {
                job.AdvanceStatus(TradeJobStatus.Completed);
                Console.WriteLine($"[Trade] Job {jobId[..8]} completed.");
            }
        }

        /// <summary>
        /// Marks a job as Failed (ship couldn't complete).
        /// Called by OrderSystem on error.
        /// </summary>
        public void MarkJobFailed(string jobId, string reason)
        {
            if (Jobs.TryGetValue(jobId, out var job))
            {
                job.AdvanceStatus(TradeJobStatus.Failed);
                Console.WriteLine($"[Trade] Job {jobId[..8]} failed: {reason}");
            }
        }

        // ============================================================
        // CLEANUP
        // ============================================================

        private void CleanupJobs()
        {
            var toRemove = Jobs.Keys
                .Where(k => Jobs[k].Status is TradeJobStatus.Completed
                                           or TradeJobStatus.Failed)
                .Where(k =>
                {
                    var j = Jobs[k];
                    return j.CompletedAt.HasValue &&
                           (DateTime.UtcNow - j.CompletedAt.Value).TotalSeconds > 30;
                })
                .ToList();

            foreach (var id in toRemove)
                Jobs.Remove(id);
        }
    }
}
