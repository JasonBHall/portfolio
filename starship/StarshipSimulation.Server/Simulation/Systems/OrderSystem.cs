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
    /// Executes Orders step by step on each macro tick.
    ///
    /// Responsibilities:
    ///   - Advance MoveTo steps when entity.HasArrived (set by MovementSystem)
    ///   - Execute Load/Unload Interact steps via ProductionComponent.TransferOut/In
    ///   - Clear entity.ActiveRoute between steps
    ///   - Mark TradeJob status transitions via TradeSystem
    ///   - Clear LogisticsComponent.CurrentJobId on job completion
    ///   - Issue rally point move order on job completion if set
    ///   - Write to entity bridge log throughout
    ///
    /// Does NOT:
    ///   - Plan routes (NavigationSystem)
    ///   - Move entities (MovementSystem handles micro ticks)
    ///   - Schedule jobs (TradeSystem)
    ///
    /// Relationship with MovementSystem:
    ///   OrderSystem    → sets entity.CurrentOrder and entity.ActiveRoute
    ///   MovementSystem → executes route segments, sets entity.HasArrived
    ///   OrderSystem    → reads entity.HasArrived on next macro tick, advances step
    ///
    /// See Core Truths — Economy System, Route Storage on Entity.
    /// </summary>
    public class OrderSystem
    {
        private readonly UniverseService  _universe;
        private readonly TradeSystem      _trade;
        private readonly NavigationSystem _navigation;

        private const float ArrivalThreshold = 25f;  // world units — arrival detection

        public OrderSystem(
            UniverseService  universe,
            TradeSystem      trade,
            NavigationSystem navigation)
        {
            _universe   = universe;
            _trade      = trade;
            _navigation = navigation;
        }

        // ============================================================
        // TICK — called by UniverseService.RunMacroTick()
        // ============================================================

        public void Tick(double deltaSeconds)
        {
            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive)        continue;
                if (entity.CurrentOrder == null) continue;

                ProcessEntityOrder(entity);
            }
        }

        // ============================================================
        // ARRIVAL CALLBACK — called by MovementSystem on micro tick
        // when a route completes. Advances the MoveTo step immediately
        // rather than waiting for the next macro tick.
        // This mirrors the legacy OrderSystem which checked arrival
        // every tick rather than using a flag-based handshake.
        // ============================================================

        public void OnEntityArrived(Entity entity)
        {
            var order = entity.CurrentOrder;
            if (order == null) return;

            var step = order.CurrentStep;
            if (step == null || step.StepType != StepType.MoveTo) return;

            // Resolve the target name for logging
            string targetName = "destination";
            if (step.TargetEntityId != null)
                targetName = _universe.GetEntity(step.TargetEntityId)?.Name
                             ?? step.TargetEntityId[..8];
            else if (step.TargetPosition.HasValue)
                targetName = $"({step.TargetPosition.Value.X:F0},{step.TargetPosition.Value.Y:F0})";

            // Advance past the MoveTo step
            entity.HasArrived            = false;
            entity.ActiveRoute           = null;
            entity.ActiveSegmentIndex    = 0;
            entity.SegmentElapsedSeconds = 0;
            order.CurrentStepIndex++;
            entity.MarkDirty();

            entity.Log.SetStatus($"Arrived at {targetName}");
            entity.Log.Info("Order", $"Arrived at {targetName}");
        }

        // ============================================================
        // MAIN ORDER PROCESSOR — macro tick
        // ============================================================

        private void ProcessEntityOrder(Entity entity)
        {
            var order = entity.CurrentOrder!;

            // All steps complete
            if (order.CurrentStepIndex >= order.Steps.Count)
            {
                CompleteOrder(entity, order);
                return;
            }

            var step = order.CurrentStep!;

            switch (step.StepType)
            {
                case StepType.MoveTo:
                    ProcessMoveToStep(entity, step, order);
                    break;

                case StepType.Interact:
                    ProcessInteractStep(entity, step, order);
                    break;

                default:
                    order.CurrentStepIndex++;
                    entity.MarkDirty();
                    break;
            }
        }

        // ============================================================
        // MOVETO STEP — macro tick
        // Only handles: initial route planning + unresolvable targets.
        // Arrival advancement is handled by OnEntityArrived() on micro tick.
        // ============================================================

        private void ProcessMoveToStep(Entity entity, OrderStep step, Order order)
        {
            // Unresolvable target — skip
            Vector2? targetPos = ResolvePosition(step);
            if (targetPos == null)
            {
                entity.Log.Warning("Order", "MoveTo: could not resolve target, skipping");
                order.CurrentStepIndex++;
                entity.MarkDirty();
                return;
            }

            // Already close enough — advance immediately (handles spawn-at-destination case)
            float dist = Vector2.Distance(entity.Position, targetPos.Value);
            if (dist <= ArrivalThreshold)
            {
                OnEntityArrived(entity);
                return;
            }

            // HasArrived set — OnEntityArrived already fired on the micro tick.
            // This macro tick sees it in case the micro tick just happened.
            if (entity.HasArrived)
            {
                OnEntityArrived(entity);
                return;
            }

            // Route already planned and being executed — nothing to do on macro tick
            if (entity.ActiveRoute != null) return;

            // No route yet — plan it (fires once per MoveTo step)
            var stats = entity.GetComponent<ShipStatsComponent>();
            if (stats == null) return;

            entity.ActiveRoute = _navigation.PlanRoute(
                entity.Position, targetPos.Value, stats,
                null, step.TargetEntityId);

            entity.ActiveSegmentIndex    = 0;
            entity.SegmentElapsedSeconds = 0;
            entity.HasArrived            = false;
            entity.MarkDirty();

            string targetName = step.TargetEntityId != null
                ? _universe.GetEntity(step.TargetEntityId)?.Name ?? "entity"
                : $"({targetPos.Value.X:F0},{targetPos.Value.Y:F0})";

            entity.Log.SetStatus($"Moving to {targetName} — {dist:F0}m");
            entity.Log.Info("Order",
                $"Route planned to {targetName}: " +
                $"{entity.ActiveRoute.Segments.Count} segment(s), " +
                $"~{entity.ActiveRoute.TotalSeconds:F0}s");
        }

        // ============================================================
        // INTERACT STEP
        // ============================================================


        // ============================================================
        // INTERACT STEP ENTRY — check dwell first
        // ============================================================

        private void ProcessInteractStep(Entity entity, OrderStep step, Order order)
        {
            if (step.Action == null || step.TargetEntityId == null)
            {
                order.CurrentStepIndex++;
                entity.MarkDirty();
                return;
            }

            // If currently dwelling (loading/unloading in progress), count down
            var logistics = entity.GetComponent<LogisticsComponent>();
            if (logistics != null && logistics.IsDwelling)
            {
                logistics.DwellTicksRemaining--;
                entity.MarkDirty();

                if (logistics.DwellTicksRemaining <= 0)
                {
                    // Dwell complete — advance to next step
                    order.CurrentStepIndex++;
                    entity.Log.Info("Order", "Dwell complete — departing");
                    entity.Log.SetStatus("Departing");
                }
                else
                {
                    entity.Log.SetStatus(
                        $"{step.Action}ing... {logistics.DwellTicksRemaining} ticks remaining");
                }
                return;
            }

            var targetEntity = _universe.GetEntity(step.TargetEntityId);
            if (targetEntity == null)
            {
                entity.Log.Warning("Order",
                    $"Interact target {step.TargetEntityId} not found — skipping");
                order.CurrentStepIndex++;
                entity.MarkDirty();
                return;
            }

            // Ensure we're close enough to interact
            float dist = System.Numerics.Vector2.Distance(entity.Position, targetEntity.Position);
            if (dist > ArrivalThreshold * 2f)
            {
                entity.Log.Warning("Order",
                    $"Not close enough to interact with {targetEntity.Name} ({dist:F0}m) — moving");

                if (entity.ActiveRoute == null)
                {
                    var stats = entity.GetComponent<ShipStatsComponent>();
                    if (stats != null)
                    {
                        entity.ActiveRoute = _navigation.PlanRoute(
                            entity.Position, targetEntity.Position, stats,
                            null, targetEntity.Id);
                        entity.ActiveSegmentIndex    = 0;
                        entity.SegmentElapsedSeconds = 0;
                        entity.HasArrived            = false;
                        entity.MarkDirty();
                    }
                }
                return;
            }

            entity.ActiveRoute = null;
            entity.HasArrived  = false;

            // Parse step parameters
            step.Parameters ??= new Dictionary<string, string>();
            string resource = step.Parameters.GetValueOrDefault("resource", "");
            string jobId    = step.Parameters.GetValueOrDefault("jobId",    "");
            int    amount   = 0;
            int.TryParse(step.Parameters.GetValueOrDefault("amount", "0"), out amount);

            if (string.IsNullOrEmpty(resource) || amount <= 0)
            {
                entity.Log.Warning("Order", "Interact step missing resource/amount — skipping");
                order.CurrentStepIndex++;
                entity.MarkDirty();
                return;
            }

            switch (step.Action)
            {
                case "Load":
                    ExecuteLoad(entity, targetEntity, resource, amount, jobId, order);
                    break;

                case "Unload":
                    ExecuteUnload(entity, targetEntity, resource, amount, jobId, order);
                    break;

                default:
                    entity.Log.Warning("Order",
                        $"Unknown action '{step.Action}' — skipping");
                    order.CurrentStepIndex++;
                    entity.MarkDirty();
                    break;
            }
        }

        // ============================================================
        // LOAD — provider output bunker → entity cargo
        // Dwell: 1 macro tick per stack loaded (larger loads take longer)
        // ============================================================

        private void ExecuteLoad(
            Entity entity,
            Entity provider,
            string resource,
            int    requestedAmount,
            string jobId,
            Order  order)
        {
            var prod = provider.GetAllComponents()
                .OfType<ProductionComponent>()
                .FirstOrDefault(p =>
                    p.OutputBunkers.ContainsKey(resource) &&
                    p.OutputBunkers[resource] > 0);

            if (prod == null)
            {
                entity_log_and_skip(entity, provider, "Load", resource,
                    $"No output bunker for {resource} on {provider.Name}", order);

                if (!string.IsNullOrEmpty(jobId))
                    _trade.MarkJobFailed(jobId, $"No {resource} in output bunker");
                return;
            }

            int loaded = prod.TransferOut(resource, requestedAmount, entity);

            if (loaded > 0)
            {
                // Calculate dwell: 1 macro tick per stack
                int stackSize = 1;
                if (StarshipSimulation.Shared.Economy.ResourceRegistry
                    .TryGet(resource, out var resDef) && resDef != null)
                    stackSize = Math.Max(1, resDef.StackSize);

                int stacksLoaded = (int)Math.Ceiling((double)loaded / stackSize);
                var logistics = entity.GetComponent<LogisticsComponent>();
                if (logistics != null)
                    logistics.DwellTicksRemaining = stacksLoaded;

                // Don't advance step yet — dwell check at top of ProcessInteractStep will fire
                // Step advances when DwellTicksRemaining reaches 0
                // For now: advance immediately if dwell is 0 or no logistics component
                if (logistics == null || logistics.DwellTicksRemaining <= 0)
                    order.CurrentStepIndex++;

                entity.MarkDirty();
                provider.MarkDirty();

                entity.Log.Event("Order",
                    $"Loading {loaded} {resource} from {provider.Name} " +
                    $"({stacksLoaded} stack(s), {stacksLoaded} tick(s) dwell)");
                entity.Log.SetStatus($"Loading — {stacksLoaded} ticks remaining");
                provider.Log.Info("Order",
                    $"{entity.Name} loading {loaded} {resource}");

                if (!string.IsNullOrEmpty(jobId))
                    _trade.MarkJobInTransit(jobId);
            }
            else
            {
                entity_log_and_skip(entity, provider, "Load", resource,
                    $"Output bunker had 0 {resource}", order);

                if (!string.IsNullOrEmpty(jobId))
                    _trade.MarkJobFailed(jobId, $"Output bunker empty at pickup");
            }
        }

        // ============================================================
        // UNLOAD — entity cargo → requester input bunker
        // Dwell: 1 macro tick per stack unloaded
        // ============================================================

        private void ExecuteUnload(
            Entity entity,
            Entity requester,
            string resource,
            int    requestedAmount,
            string jobId,
            Order  order)
        {
            var prod = requester.GetAllComponents()
                .OfType<ProductionComponent>()
                .FirstOrDefault(p => p.InputBunkers.ContainsKey(resource));

            if (prod == null)
            {
                entity_log_and_skip(entity, requester, "Unload", resource,
                    $"No input bunker for {resource} on {requester.Name}", order);

                if (!string.IsNullOrEmpty(jobId))
                    _trade.MarkJobFailed(jobId, $"No input bunker for {resource}");
                return;
            }

            int unloaded = prod.TransferIn(resource, requestedAmount, entity);

            entity.MarkDirty();
            requester.MarkDirty();

            if (unloaded > 0)
            {
                // Dwell: 1 macro tick per stack
                int stackSize = 1;
                if (StarshipSimulation.Shared.Economy.ResourceRegistry
                    .TryGet(resource, out var resDef) && resDef != null)
                    stackSize = Math.Max(1, resDef.StackSize);

                int stacksUnloaded = (int)Math.Ceiling((double)unloaded / stackSize);
                var logistics = entity.GetComponent<LogisticsComponent>();
                if (logistics != null)
                    logistics.DwellTicksRemaining = stacksUnloaded;

                if (logistics == null || logistics.DwellTicksRemaining <= 0)
                    order.CurrentStepIndex++;

                entity.Log.Event("Order",
                    $"Unloading {unloaded} {resource} at {requester.Name} " +
                    $"({stacksUnloaded} stack(s), {stacksUnloaded} tick(s) dwell)");
                entity.Log.SetStatus($"Unloading — {stacksUnloaded} ticks remaining");
                requester.Log.Event("Order",
                    $"Receiving {unloaded} {resource} from {entity.Name}");

                if (!string.IsNullOrEmpty(jobId))
                    _trade.MarkJobCompleted(jobId);
            }
            else
            {
                entity.Log.Warning("Order",
                    $"Nothing unloaded at {requester.Name} — input bunker full");
                requester.Log.Warning("Order",
                    $"{entity.Name} attempted delivery but input bunker full");
                order.CurrentStepIndex++;

                if (!string.IsNullOrEmpty(jobId))
                    _trade.MarkJobFailed(jobId, "Input bunker full at delivery");
            }
        }

        // ============================================================
        // ORDER COMPLETION
        // ============================================================

        private void CompleteOrder(Entity entity, Order order)
        {
            entity.CurrentOrder          = null;
            entity.ActiveRoute           = null;
            entity.ActiveSegmentIndex    = 0;
            entity.SegmentElapsedSeconds = 0;
            entity.HasArrived            = false;
            entity.Velocity              = System.Numerics.Vector2.Zero;
            entity.MarkDirty();

            entity.Log.Info("Order", "Order complete");
            entity.Log.SetIntent("");

            // Handle LogisticsComponent — clear job, handle rally point
            var logistics = entity.GetComponent<LogisticsComponent>();
            if (logistics != null)
            {
                logistics.ClearJob();

                if (logistics.HasRallyPoint && logistics.AcceptsTradeContracts)
                {
                    IssueMoveToRallyPoint(entity, logistics);
                }
                else
                {
                    entity.Log.SetStatus("Idle — awaiting trade contract");
                }
            }
            else
            {
                entity.Log.SetStatus("Idle");
            }
        }

        // ============================================================
        // RALLY POINT
        // ============================================================

        private void IssueMoveToRallyPoint(Entity entity, LogisticsComponent logistics)
        {
            Vector2? rallyPos = null;
            string   rallyDesc = "rally point";

            if (logistics.RallyPointEntityId != null)
            {
                var rallyEntity = _universe.GetEntity(logistics.RallyPointEntityId);
                if (rallyEntity != null)
                {
                    rallyPos  = rallyEntity.Position;
                    rallyDesc = rallyEntity.Name;
                }
            }

            if (rallyPos == null && logistics.RallyPointPosition.HasValue)
            {
                rallyPos  = logistics.RallyPointPosition.Value;
                rallyDesc = $"({rallyPos.Value.X:F0},{rallyPos.Value.Y:F0})";
            }

            if (rallyPos == null)
            {
                entity.Log.SetStatus("Idle — awaiting trade contract");
                return;
            }

            entity.CurrentOrder = new Order
            {
                Type  = OrderType.Move,
                Steps = new List<OrderStep>
                {
                    new() { StepType = StepType.MoveTo, TargetPosition = rallyPos }
                }
            };

            entity.Log.SetStatus($"Returning to {rallyDesc}");
            entity.Log.Info("Order", $"Moving to rally point: {rallyDesc}");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private Vector2? ResolvePosition(OrderStep step)
        {
            if (step.TargetEntityId != null)
            {
                var e = _universe.GetEntity(step.TargetEntityId);
                return e?.Position;
            }

            return step.TargetPosition;
        }

        private static void entity_log_and_skip(
            Entity ship, Entity target, string action, string resource,
            string reason, Order order)
        {
            ship.Log.Warning("Order",
                $"{action} {resource} at {target.Name} failed: {reason}");
            order.CurrentStepIndex++;
            ship.MarkDirty();
        }
    }
}
