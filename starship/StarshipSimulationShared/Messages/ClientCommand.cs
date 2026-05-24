using System.Collections.Generic;
using System.Numerics;

namespace StarshipSimulation.Shared.Messages
{
    /// <summary>
    /// A command sent from client to server on the /ws/commands channel.
    ///
    /// All client input flows through this contract. The server validates
    /// and applies commands — clients never mutate simulation state directly.
    ///
    /// Command kinds:
    ///   "login"                — player identifying themselves
    ///   "thrust"               — W/S   → EngineModule.CurrentThrottle
    ///   "lateral"              — Q/E   → EngineModule.CurrentLateralInput
    ///   "rotate"               — A/D   → entity.Heading
    ///   "toggle_dampeners"     — Z     → PlayerControlledComponent.DampenersOn
    ///   "fire"                 — SPACE → WeaponModule.WantsToFire
    ///   "set_target"           — T     → PlayerControlledComponent.CurrentTargetId
    ///   "set_order"            — (stub) autonomous order assignment
    ///   "cancel_order"         — (stub)
    ///   "request_entity_log"   — inspector log pull
    ///   "spawn_entity"         — entity-editor "Place" button
    ///   "add_component"        — entity editor "add module"
    ///   "remove_component"     — entity editor "remove module"
    /// </summary>
    public class ClientCommand
    {
        // ------------------------------------------------------------
        // Envelope
        // ------------------------------------------------------------

        public string Kind     { get; set; } = "";
        public string PlayerId { get; set; } = "";
        public string EntityId { get; set; } = "";

        // ------------------------------------------------------------
        // Login
        // ------------------------------------------------------------

        public string? ClientId   { get; set; }
        public string? PlayerName { get; set; }

        // ------------------------------------------------------------
        // Movement
        // ------------------------------------------------------------

        /// <summary>Forward/reverse throttle — 0.0 .. ±1.0.</summary>
        public float? ThrustAmount { get; set; }

        /// <summary>Lateral strafe — 0.0 .. ±1.0. +1 = right, -1 = left.</summary>
        public float? LateralAmount { get; set; }

        /// <summary>Desired heading as a normalised 2D vector.</summary>
        public Vector2? DesiredHeading { get; set; }

        /// <summary>Explicit dampener state. If null, server toggles.</summary>
        public bool? DampenersOn { get; set; }

        // ------------------------------------------------------------
        // Combat / targeting
        // ------------------------------------------------------------

        public string?  WeaponId       { get; set; }
        public string?  TargetId       { get; set; }
        public Vector2? TargetPosition { get; set; }

        // ------------------------------------------------------------
        // Orders (stubs)
        // ------------------------------------------------------------

        public string? OrderType   { get; set; }
        public string? OrderTarget { get; set; }

        // ------------------------------------------------------------
        // Debug / UI
        // ------------------------------------------------------------

        public int? LogEntryCount { get; set; }

        // ------------------------------------------------------------
        // Entity editor — spawn_entity
        //
        // SpawnModules format: list of "category:name" strings parsed by
        // CommandHandler.BuildModuleFromSpec. Example:
        //   [ "engine:gecko_sublight",
        //     "power:mini_fusion",
        //     "cargo:small_pod",
        //     "weapon:laser_cannon" ]
        // ------------------------------------------------------------

        public string?       SpawnName     { get; set; }
        public string?       SpawnKind     { get; set; }   // "ship", "station", "gate"
        public Vector2?      SpawnPosition { get; set; }
        public List<string>? SpawnModules  { get; set; }

        // ------------------------------------------------------------
        // Entity editor — add/remove component
        // ------------------------------------------------------------

        /// <summary>"category:name" string for add_component.</summary>
        public string? ComponentSpec { get; set; }

        /// <summary>ComponentId to target for remove_component.</summary>
        public string? ComponentId { get; set; }
    }
}
