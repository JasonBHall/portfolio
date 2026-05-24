using System;
using System.Linq;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StarshipSimulation.Server.Simulation;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;
using StarshipSimulation.Shared.Economy;
using StarshipSimulation.Shared.Messages;
using StarshipSimulation.Shared.Players;
// Disambiguate from System.IO.DriveType which implicit usings may pull in.
using DriveType = StarshipSimulation.Shared.Entities.Components.DriveType;

namespace StarshipSimulation.Server.Networking
{
    /// <summary>
    /// Handles the /ws/commands WebSocket channel.
    ///
    /// STRICT ROLE: translate client messages into component writes.
    /// This class does not contain game logic. Systems drive behaviour;
    /// this handler just sets flags and values on components.
    ///
    /// Responsibilities:
    ///   - Login / session establishment (spawns the player's ship with
    ///     a starter loadout, attaches PlayerControlledComponent)
    ///   - Translate input commands to component field writes
    ///   - Dispatch entity-editor commands (spawn / add-component /
    ///     remove-component) to UniverseService helpers
    /// </summary>
    public class CommandHandler
    {
        // ------------------------------------------------------------
        // Dependencies
        // ------------------------------------------------------------

        private readonly UniverseService    _universe;
        private readonly PlayerSessionStore _sessions;

        // ------------------------------------------------------------
        // Per-connection state
        // ------------------------------------------------------------

        private PlayerSession? _session;

        // ------------------------------------------------------------
        // Serialisation — IncludeFields is critical for Vector2
        // ------------------------------------------------------------

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            IncludeFields               = true
        };

        public CommandHandler(UniverseService universe, PlayerSessionStore sessions)
        {
            _universe = universe;
            _sessions = sessions;
        }

        // ------------------------------------------------------------
        // Connection handler
        // ------------------------------------------------------------

        public async Task HandleAsync(WebSocket socket, CancellationToken ct)
        {
            Console.WriteLine("[CommandHandler] Client connected.");
            var buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    try { result = await socket.ReceiveAsync(buffer, ct); }
                    catch { break; }

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var json    = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var command = Deserialize(json);
                    if (command != null)
                        await ApplyCommand(command, socket, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"[CommandHandler] Error: {ex.Message}"); }
            finally
            {
                if (_session != null)
                {
                    Console.WriteLine($"[CommandHandler] Player disconnected: {_session.PlayerId}");
                    _sessions.Remove(_session.PlayerId);
                }
                await CloseAsync(socket);
            }
        }

        // ------------------------------------------------------------
        // Dispatch
        // ------------------------------------------------------------

        private async Task ApplyCommand(ClientCommand c, WebSocket socket, CancellationToken ct)
        {
            switch (c.Kind)
            {
                case "login":              await HandleLogin(c, socket, ct); break;

                // Movement — pure component writes
                case "thrust":             HandleThrust(c);            break;
                case "lateral":            HandleLateral(c);           break;
                case "rotate":             HandleRotate(c);            break;
                case "toggle_dampeners":   HandleToggleDampeners(c);   break;

                // Combat — pure component writes
                case "fire":               HandleFire(c);              break;
                case "set_target":         HandleSetTarget(c);         break;

                // Orders (stubs)
                case "set_order":          HandleSetOrder(c);          break;
                case "cancel_order":       HandleCancelOrder(c);       break;

                // Entity editor
                case "spawn_entity":       HandleSpawnEntity(c);       break;
                case "add_component":      HandleAddComponent(c);      break;
                case "remove_component":   HandleRemoveComponent(c);   break;

                // Debug
                case "request_entity_log": await HandleRequestEntityLog(c, socket, ct); break;

                default:
                    Console.WriteLine($"[CommandHandler] Unknown command kind: {c.Kind}");
                    break;
            }
        }

        // ============================================================
        // LOGIN — spawns the player ship and attaches PlayerControlledComponent
        // ============================================================

        private async Task HandleLogin(ClientCommand c, WebSocket socket, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(c.ClientId))
            {
                Console.WriteLine("[CommandHandler] Login missing ClientId — ignored.");
                return;
            }

            _session = _sessions.TryResume(c.ClientId);

            if (_session == null)
            {
                var name = c.PlayerName ?? c.ClientId;

                // Small random spread so /jay and /bryan don't stack
                var rng = new Random();
                var spawn = new Vector2(
                    (float)(rng.NextDouble() * 200 - 100),
                    (float)(rng.NextDouble() * 200 - 100)
                );

                var entity = _universe.CreateEntity(name, "ship", spawn);

                // Starter loadout — engine, power, stats, laser cannon
                entity.Components.Add(new EngineModule(EngineModuleDefinitions.GeckoSublightDrive)
                {
                    IsInstalled = true
                });
                entity.Components.Add(new PowerModule(PowerModuleDefinitions.MiniFusionPlant)
                {
                    IsInstalled = true
                });
                entity.Components.Add(new WeaponModule(WeaponModuleDefinitions.LaserCannon)
                {
                    IsInstalled = true
                });
                entity.Components.Add(new ShipStatsComponent());

                _session = _sessions.CreateSession(c.ClientId, name, entity.Id);

                // Mark as player-controlled — MovementSystem switches paths on this
                entity.Components.Add(new PlayerControlledComponent(_session.PlayerId));
                entity.MarkDirty();

                Console.WriteLine(
                    $"[CommandHandler] Spawned player ship '{name}' ({_session.PlayerId}) at {spawn}");
            }

            var ack = new ClientCommand
            {
                Kind     = "login_result",
                PlayerId = _session.PlayerId,
                EntityId = _session.EntityId
            };
            await SendJson(socket, ack, ct);
        }

        // ============================================================
        // MOVEMENT — set flags on modules/components, nothing else
        // ============================================================

        private void HandleThrust(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            if (c.ThrustAmount == null) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            var throttle = Math.Clamp(c.ThrustAmount.Value, -1f, 1f);
            var engine   = GetSublightEngine(entity);
            if (engine == null) return;

            engine.CurrentThrottle = throttle;
            entity.MarkDirty();
        }

        private void HandleLateral(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            if (c.LateralAmount == null) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            var lateral = Math.Clamp(c.LateralAmount.Value, -1f, 1f);
            var engine  = GetSublightEngine(entity);
            if (engine == null) return;

            engine.CurrentLateralInput = lateral;
            entity.MarkDirty();
        }

        private void HandleRotate(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            if (c.DesiredHeading == null) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            var h = c.DesiredHeading.Value;
            if (h != Vector2.Zero)
            {
                entity.Heading = Vector2.Normalize(h);
                entity.MarkDirty();
            }
        }

        private void HandleToggleDampeners(ClientCommand c)
        {
            if (!ValidateSession(c)) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            var pc = entity.GetComponent<PlayerControlledComponent>();
            if (pc == null) return;

            pc.DampenersOn = c.DampenersOn ?? !pc.DampenersOn;
            entity.MarkDirty();

            Console.WriteLine($"[CommandHandler] {entity.Name} dampeners = {pc.DampenersOn}");
        }

        // ============================================================
        // COMBAT — flag-setting only. WeaponSystem does the firing.
        // ============================================================

        private void HandleFire(ClientCommand c)
        {
            if (!ValidateSession(c)) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            var weapons = entity.GetAllComponents().OfType<WeaponModule>().ToList();
            if (weapons.Count == 0) return;

            foreach (var w in weapons)
                if (w.IsOperational)
                    w.WantsToFire = true;

            entity.MarkDirty();
        }

        private void HandleSetTarget(ClientCommand c)
        {
            if (!ValidateSession(c)) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            var pc = entity.GetComponent<PlayerControlledComponent>();
            if (pc == null) return;

            if (string.IsNullOrWhiteSpace(c.TargetId))
            {
                pc.CurrentTargetId = null;
                entity.MarkDirty();
                return;
            }

            var target = _universe.GetEntity(c.TargetId);
            if (target == null || target.Id == entity.Id) return;

            pc.CurrentTargetId = target.Id;
            entity.MarkDirty();
        }

        // ============================================================
        // ORDERS (stubs)
        // ============================================================

        private void HandleSetOrder(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            Console.WriteLine($"[CommandHandler] SetOrder: type={c.OrderType} target={c.OrderTarget}");
        }

        private void HandleCancelOrder(ClientCommand c)
        {
            if (!ValidateSession(c)) return;

            var entity = _universe.GetEntity(_session!.EntityId);
            if (entity == null) return;

            entity.CurrentOrder = null;
            entity.MarkDirty();
        }

        // ============================================================
        // ENTITY EDITOR
        // ============================================================

        private void HandleSpawnEntity(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            if (c.SpawnPosition == null) return;

            var name = string.IsNullOrWhiteSpace(c.SpawnName) ? "Unnamed" : c.SpawnName!;
            var kind = string.IsNullOrWhiteSpace(c.SpawnKind) ? "ship"    : c.SpawnKind!;

            var entity = _universe.CreateEntity(name, kind, c.SpawnPosition.Value);

            if (c.SpawnModules != null)
            {
                foreach (var spec in c.SpawnModules)
                {
                    var module = BuildModuleFromSpec(spec);
                    if (module != null)
                        entity.Components.Add(module);
                }
            }

            // Ships get stats so MovementSystem works. Stations don't need it.
            if (kind == "ship" && !entity.HasComponent<ShipStatsComponent>())
                entity.Components.Add(new ShipStatsComponent());

            entity.MarkDirty();
            Console.WriteLine(
                $"[CommandHandler] Spawned {kind} '{name}' at {c.SpawnPosition.Value} " +
                $"with {c.SpawnModules?.Count ?? 0} modules");
        }

        private void HandleAddComponent(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            if (string.IsNullOrWhiteSpace(c.EntityId))   return;
            if (string.IsNullOrWhiteSpace(c.ComponentSpec)) return;

            var entity = _universe.GetEntity(c.EntityId);
            if (entity == null) return;

            var module = BuildModuleFromSpec(c.ComponentSpec!);
            if (module == null) return;

            entity.Components.Add(module);
            entity.GetComponent<ShipStatsComponent>()?.Invalidate();
            entity.MarkDirty();

            Console.WriteLine(
                $"[CommandHandler] Added {c.ComponentSpec} to {entity.Name}");
        }

        private void HandleRemoveComponent(ClientCommand c)
        {
            if (!ValidateSession(c)) return;
            if (string.IsNullOrWhiteSpace(c.EntityId))    return;
            if (string.IsNullOrWhiteSpace(c.ComponentId)) return;

            var entity = _universe.GetEntity(c.EntityId);
            if (entity == null) return;

            var target = entity.Components
                               .FirstOrDefault(co => co.ComponentId == c.ComponentId);
            if (target == null) return;

            entity.Components.Remove(target);
            entity.GetComponent<ShipStatsComponent>()?.Invalidate();
            entity.MarkDirty();

            Console.WriteLine(
                $"[CommandHandler] Removed {target.Name} from {entity.Name}");
        }

        /// <summary>
        /// Parse a module spec string into a module instance. Returns null
        /// for unknown specs.
        ///
        /// Spec formats:
        ///   "engine:<name>"        "power:<name>"
        ///   "cargo:<name>"         "weapon:<name>"
        ///   "logistics:trader"     — LogisticsComponent, trade-eligible
        ///   "logistics:private"    — LogisticsComponent, NOT trade-eligible
        ///   "production:<definitionName>"
        ///       Named production module, uses its DefaultRecipe.
        ///   "production:general:<recipeName>"
        ///       GeneralProductionModule + SetRecipe to the named recipe.
        ///       Use when you want to configure a factory at runtime.
        /// </summary>
        private static IComponent? BuildModuleFromSpec(string spec)
        {
            // Three-part spec "production:general:<recipe>" — handle first
            // since the generic split would swallow the middle ':'.
            var allParts = spec.Split(':');
            if (allParts.Length >= 3 &&
                string.Equals(allParts[0], "production", System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(allParts[1], "general",    System.StringComparison.OrdinalIgnoreCase))
            {
                var recipeName = string.Join(':', allParts, 2, allParts.Length - 2).Trim();
                if (string.IsNullOrWhiteSpace(recipeName)) return null;
                if (!ProductionRecipeRegistry.TryGet(recipeName, out var recipe) || recipe == null)
                {
                    Console.WriteLine($"[CommandHandler] Unknown recipe '{recipeName}' for production:general");
                    return null;
                }
                var prod = new ProductionComponent(ProductionComponentDefinitions.GeneralProductionModule)
                {
                    IsInstalled = true
                };
                prod.SetRecipe(recipe);
                return prod;
            }

            var parts = spec.Split(':', 2);
            if (parts.Length != 2) return null;

            var (cat, name) = (parts[0].Trim().ToLowerInvariant(),
                               parts[1].Trim().ToLowerInvariant());

            IComponent? mod = (cat, name) switch
            {
                ("engine", "gecko_sublight")    => new EngineModule(EngineModuleDefinitions.GeckoSublightDrive),
                ("engine", "dothraki_horse")    => new EngineModule(EngineModuleDefinitions.DothrakiHorseDrive),
                ("engine", "micro_warp")        => new EngineModule(EngineModuleDefinitions.MicroWarpPod),
                ("engine", "spring_jump")       => new EngineModule(EngineModuleDefinitions.SpringJumpUnit),

                ("power",  "mini_fusion")       => new PowerModule(PowerModuleDefinitions.MiniFusionPlant),
                ("power",  "civilian_fusion")   => new PowerModule(PowerModuleDefinitions.CivilianFusionCore),
                ("power",  "military_fusion")   => new PowerModule(PowerModuleDefinitions.MilitaryFusionCore),

                ("cargo",  "small_pod")          => new CargoModule(CargoModuleDefinitions.SmallCargoPod),
                ("cargo",  "standard_container") => new CargoModule(CargoModuleDefinitions.StandardCargoContainer),
                ("cargo",  "bulk_hold")          => new CargoModule(CargoModuleDefinitions.BulkCargoHold),

                ("weapon", "laser_cannon")       => new WeaponModule(WeaponModuleDefinitions.LaserCannon),
                ("weapon", "rail_gun")           => new WeaponModule(WeaponModuleDefinitions.RailGun),

                // Logistics — trader is the common case; "private" useful for
                // ships you want to keep out of the trade scheduler pool.
                ("logistics", "trader")          => new LogisticsComponent { AcceptsTradeContracts = true  },
                ("logistics", "private")         => new LogisticsComponent { AcceptsTradeContracts = false },

                // Named production modules use their DefaultRecipe.
                ("production", "iron_mine")           => BuildNamedProduction(ProductionComponentDefinitions.IronMiningDrill),
                ("production", "copper_mine")         => BuildNamedProduction(ProductionComponentDefinitions.CopperMiningDrill),
                ("production", "iron_smelter")        => BuildNamedProduction(ProductionComponentDefinitions.IronSmelter),
                ("production", "copper_smelter")      => BuildNamedProduction(ProductionComponentDefinitions.CopperSmelter),
                ("production", "iron_tube_fab")       => BuildNamedProduction(ProductionComponentDefinitions.IronTubeFabricator),
                ("production", "warhead_factory")     => BuildNamedProduction(ProductionComponentDefinitions.WarheadFactory),
                ("production", "missile_constructor") => BuildNamedProduction(ProductionComponentDefinitions.MissileConstructor),
                ("production", "nuclear_missile")     => BuildNamedProduction(ProductionComponentDefinitions.NuclearMissileConstructor),
                ("production", "stealth_torpedo")     => BuildNamedProduction(ProductionComponentDefinitions.StealthTorpedoConstructor),

                _ => null
            };

            if (mod is ModuleBase mb) mb.IsInstalled = true;
            return mod;
        }

        /// <summary>
        /// Helper — instantiate a named ProductionComponent and apply its
        /// default recipe. Returns null if the default recipe name is empty
        /// or not registered (the GeneralProductionModule path uses a
        /// different spec format and is handled in BuildModuleFromSpec).
        /// </summary>
        private static ProductionComponent? BuildNamedProduction(ProductionComponentConfig cfg)
        {
            var comp = new ProductionComponent(cfg) { IsInstalled = true };
            if (!string.IsNullOrWhiteSpace(cfg.DefaultRecipe) &&
                ProductionRecipeRegistry.TryGet(cfg.DefaultRecipe, out var recipe) &&
                recipe != null)
            {
                comp.SetRecipe(recipe);
            }
            return comp;
        }

        // ============================================================
        // ENTITY LOG
        // ============================================================

        private async Task HandleRequestEntityLog(ClientCommand c, WebSocket socket, CancellationToken ct)
        {
            var entity = _universe.GetEntity(c.EntityId);
            if (entity == null) return;

            int count   = c.LogEntryCount ?? 20;
            var entries = entity.Log.GetRecentFormatted(count);

            var response = new EntityLogResponse
            {
                EntityId      = entity.Id,
                EntityName    = entity.Name,
                CurrentStatus = entity.Log.CurrentStatus,
                CurrentIntent = entity.Log.CurrentIntent,
                Entries       = entries
            };

            await SendJson(socket, response, ct);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static EngineModule? GetSublightEngine(Entity e) =>
            e.GetAllComponents()
             .OfType<EngineModule>()
             .FirstOrDefault(m => m.IsOperational && m.DriveType == DriveType.Sublight);

        private bool ValidateSession(ClientCommand c)
        {
            if (_session == null)
            {
                Console.WriteLine($"[CommandHandler] Command '{c.Kind}' before login — ignored.");
                return false;
            }
            return true;
        }

        private static async Task SendJson(WebSocket socket, object payload, CancellationToken ct)
        {
            if (socket.State != WebSocketState.Open) return;
            var json  = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                ct);
        }

        private static ClientCommand? Deserialize(string json)
        {
            try { return JsonSerializer.Deserialize<ClientCommand>(json, JsonOptions); }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandHandler] Failed to deserialise: {ex.Message}");
                return null;
            }
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
                        CancellationToken.None);
                }
            }
            catch { }
        }
    }
}
