using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StarshipSimulation.Server.Simulation;
using StarshipSimulation.Shared.Economy;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;
using StarshipSimulation.Shared.Messages;

namespace StarshipSimulation.Server.Networking
{
    /// <summary>
    /// Handles the /ws/universe WebSocket channel.
    ///
    /// Each connecting client gets its own handler instance with its own
    /// version tracking — delta state is per-client, not global.
    ///
    /// On connect:   sends a full snapshot of all visible entities
    /// Each tick:    sends a delta of only what has changed since last send
    /// On remove:    sends entity ids the client should despawn
    ///
    /// Observer culling is applied here — only entities within the
    /// player's observer range are included in snapshots and deltas.
    /// (Observer range query uses SpatialGrid — stub for now, full
    ///  implementation added when ObserverSystem is written.)
    /// </summary>
    public class UniverseStream
    {
        // ------------------------------------------------------------
        // Dependencies
        // ------------------------------------------------------------

        private readonly UniverseService _universe;

        // ------------------------------------------------------------
        // Per-client delta tracking
        // Key = entity Id, Value = last version sent to THIS client
        // This is what was broken in the legacy — one global dictionary
        // caused clients to interfere with each other's delta state.
        // ------------------------------------------------------------

        private readonly Dictionary<string, long> _lastSeenVersions = new();

        // ------------------------------------------------------------
        // Serialisation
        // ------------------------------------------------------------

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields        = true   // Vector2 stores X/Y as fields not properties
        };

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public UniverseStream(UniverseService universe)
        {
            _universe = universe;
        }

        // ------------------------------------------------------------
        // Connection handler
        // Called once per connecting client from Program.cs
        // ------------------------------------------------------------

        public async Task HandleAsync(WebSocket socket, CancellationToken ct)
        {
            Console.WriteLine("[UniverseStream] Client connected.");

            try
            {
                // Send full snapshot on connect
                var snapshot = BuildSnapshot();
                await SendAsync(socket, snapshot, ct);

                // Stream deltas
                while (!ct.IsCancellationRequested &&
                       socket.State == WebSocketState.Open)
                {
                    var delta = BuildDelta();

                    if (delta.Entities.Count > 0 || delta.Removed.Count > 0)
                        await SendAsync(socket, delta, ct);

                    await Task.Delay(50, ct); // 20Hz
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[UniverseStream] Error: {ex.Message}");
            }
            finally
            {
                await CloseAsync(socket);
                Console.WriteLine("[UniverseStream] Client disconnected.");
            }
        }

        // ------------------------------------------------------------
        // Snapshot — full state, sent on first connect
        // ------------------------------------------------------------

        private TickUpdate BuildSnapshot()
        {
            var snapshot = new TickUpdate
            {
                Type = "snapshot",
                Tick = _universe.MacroTick,
                Entities = new Dictionary<string, EntityState>(),
                Removed  = new System.Collections.Generic.List<string>()
            };

            foreach (var (id, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                var state = MapEntityState(entity);
                snapshot.Entities[id] = state;
                _lastSeenVersions[id] = entity.Version;
            }

            Console.WriteLine($"[UniverseStream] Snapshot sent — {snapshot.Entities.Count} entities.");

            return snapshot;
        }

        // ------------------------------------------------------------
        // Delta — only what changed since last send to THIS client
        // ------------------------------------------------------------

        private TickUpdate BuildDelta()
        {
            var delta = new TickUpdate
            {
                Type = "delta",
                Tick = _universe.MacroTick,
                Entities = new Dictionary<string, EntityState>(),
                Removed  = new System.Collections.Generic.List<string>()
            };

            // Changed or new entities
            foreach (var (id, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                _lastSeenVersions.TryGetValue(id, out var lastVersion);

                if (entity.Version != lastVersion)
                {
                    delta.Entities[id] = MapEntityState(entity);
                    _lastSeenVersions[id] = entity.Version;
                }
            }

            // Removed entities — were known to this client, now gone
            foreach (var knownId in new List<string>(_lastSeenVersions.Keys))
            {
                if (!_universe.Entities.ContainsKey(knownId))
                {
                    delta.Removed.Add(knownId);
                    _lastSeenVersions.Remove(knownId);
                }
            }

            return delta;
        }

        // ------------------------------------------------------------
        // Entity → EntityState mapping
        // No DtoMapper — entity maps directly to its wire contract.
        // ComponentSummary is populated from components the entity has.
        // ------------------------------------------------------------

        private static EntityState MapEntityState(Entity entity)
        {
            var state = new EntityState
            {
                Id       = entity.Id,
                Name     = entity.Name,
                Kind     = entity.Kind,
                Version  = entity.Version,
                Position = entity.Position,
                Velocity = entity.Velocity,
                Heading  = entity.Heading,
            };

            // Swarm attrition state
            // var attrition = entity.GetComponent<AttritionComponent>();
            // if (attrition != null)
            // {
            //     state.Count  = attrition.Count;
            //     state.Spread = attrition.Cohesion;
            // }

            // Bridge log — status and intent only (entries are on-demand)
            state.CurrentStatus    = entity.Log.CurrentStatus;
            state.CurrentIntent    = entity.Log.CurrentIntent;
            state.RouteDestination = entity.TraversalPlan?.Destination;

            // Populate component summary from ShipStatsComponent
            var stats = entity.GetComponent<ShipStatsComponent>();
            if (stats != null)
            {
                // Power
                state.ComponentSummary["power_output"]  = stats.TotalEnergyOutput.ToString("F1");
                state.ComponentSummary["power_draw"]    = stats.TotalEnergyDraw.ToString("F1");
                state.ComponentSummary["power_net"]     = stats.NetEnergyPerTick.ToString("F1");
                state.ComponentSummary["power_storage"] = stats.TotalEnergyCapacity.ToString("F0");

                // Cargo — slot-based
                state.ComponentSummary["cargo_slots_used"]  = stats.UsedStackSlots.ToString();
                state.ComponentSummary["cargo_slots_total"] = stats.TotalStackSlots.ToString();
                state.ComponentSummary["cargo_slots_free"]  = stats.FreeStackSlots.ToString();
                state.ComponentSummary["cargo_fill"]        = stats.TotalStackSlots > 0
                    ? $"{stats.CargoFillFraction * 100f:F0}%"
                    : "—";

                // Per-resource cargo detail across all cargo modules
                var allCargo = new Dictionary<string, int>();
                foreach (var mod in entity.GetAllComponents().OfType<CargoModule>())
                    foreach (var (res, units) in mod.Contents)
                        allCargo[res] = allCargo.GetValueOrDefault(res, 0) + units;

                foreach (var (res, units) in allCargo)
                {
                    int stackSize = ResourceRegistry.TryGet(res, out var resDef) && resDef != null
                        ? resDef.StackSize : 1;
                    int slots = (int)Math.Ceiling((double)units / stackSize);
                    state.ComponentSummary[$"cargo_{res}"] = $"{units} units · {slots} slots";
                }

                // Movement
                // Ensure stats are fresh before reading — ConsumeFuel may have
                // invalidated the cache after the last MovementSystem tick.
                stats.Tick(entity, 0);
                state.ComponentSummary["thrust"]    = stats.TotalThrust.ToString("F1");
                state.ComponentSummary["max_speed"] = stats.MaxSpeed.ToString("F1");
                state.ComponentSummary["turn_rate"] = stats.TurnRate.ToString("F2");

                // Fuel
                if (stats.TotalHydrogenCapacity > 0)
                    state.ComponentSummary["hydrogen"] =
                        $"{stats.CurrentHydrogen:F0}/{stats.TotalHydrogenCapacity:F0}";

                if (stats.TotalDilithiumCapacity > 0)
                    state.ComponentSummary["dilithium"] =
                        $"{stats.CurrentDilithium:F1}/{stats.TotalDilithiumCapacity:F1}";
            }

            // Production data — one entry per ProductionComponent
            int prodIndex = 0;
            foreach (var prod in entity.GetAllComponents()
                                       .OfType<ProductionComponent>()
                                       .Where(p => p.ActiveRecipe != null))
            {
                string prefix    = $"prod{prodIndex}_";
                float  cycleTime = prod.ActiveRecipe!.ProductionTimeSeconds;

                state.ComponentSummary[$"{prefix}recipe"]     = prod.ActiveRecipe!.DisplayName;
                state.ComponentSummary[$"{prefix}progress"]   = $"{prod.Progress:F1}";
                state.ComponentSummary[$"{prefix}cycle_time"] = $"{cycleTime:F0}";
                state.ComponentSummary[$"{prefix}cycles"]     = prod.CyclesCompleted.ToString();

                // Starved = any input bunker below minimum requirement
                bool starved = !prod.ActiveRecipe.IsExtractor &&
                    prod.ActiveRecipe.Inputs.Any(kvp =>
                        prod.InputBunkers.GetValueOrDefault(kvp.Key, 0) < kvp.Value);
                state.ComponentSummary[$"{prefix}blocked"] = starved ? "starved" : "";

                // Recipe required amounts (static — for display in production cards)
                foreach (var (resource, required) in prod.ActiveRecipe.Inputs)
                    state.ComponentSummary[$"{prefix}req_in_{resource}"] = required.ToString();
                foreach (var (resource, produced) in prod.ActiveRecipe.Outputs)
                    state.ComponentSummary[$"{prefix}req_out_{resource}"] = produced.ToString();

                // Input bunkers — units only
                foreach (var (resource, units) in prod.InputBunkers)
                {
                    int cap = prod.GetInputCapacityUnits(resource);
                    state.ComponentSummary[$"{prefix}in_{resource}"] = $"{units}/{cap}";
                }

                // Output bunkers — units only
                foreach (var (resource, units) in prod.OutputBunkers)
                {
                    int cap = prod.GetOutputCapacityUnits(resource);
                    state.ComponentSummary[$"{prefix}out_{resource}"] = $"{units}/{cap}";
                }

                prodIndex++;
            }

            // Current order steps — shown in inspector
            if (entity.CurrentOrder != null)
            {
                var order = entity.CurrentOrder;
                state.ComponentSummary["order_step"]  = $"{order.CurrentStepIndex + 1}/{order.Steps.Count}";
                var step = order.CurrentStep;
                if (step != null)
                {
                    state.ComponentSummary["order_action"] = step.StepType.ToString();
                    if (step.TargetEntityId != null)
                        state.ComponentSummary["order_target"] = step.TargetEntityId;
                }
            }

            return state;
        }

        // ------------------------------------------------------------
        // Send helpers
        // ------------------------------------------------------------

        private static async Task SendAsync(WebSocket socket, TickUpdate message, CancellationToken ct)
        {
            var json  = JsonSerializer.Serialize(message, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                ct
            );
        }

        private static async Task CloseAsync(WebSocket socket)
        {
            try
            {
                if (socket.State == WebSocketState.Open ||
                    socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None
                    );
                }
            }
            catch { }
        }
    }
}
