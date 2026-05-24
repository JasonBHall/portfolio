namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Marker component — identifies an entity as under direct player control.
    ///
    /// Movable between entities: removing it from ship A and adding it to ship B
    /// transfers player control. The PlayerSession tracks which entity is being
    /// controlled via its EntityId; this component mirrors that linkage on the
    /// entity side and carries transient per-player state (target, dampeners).
    ///
    /// MovementSystem checks for this component each tick to decide which
    /// movement path to run:
    ///   PRESENT → Newtonian physics driven by input fields on modules
    ///             (EngineModule.CurrentThrottle, CurrentLateralInput) +
    ///             DampenersOn for drag behaviour.
    ///   ABSENT  → Autonomous route following via NavigationSystem.
    ///
    /// See Core Truths — Movement (player vs NPC paths).
    /// </summary>
    public class PlayerControlledComponent : ComponentBase
    {
        public override string Name => "player_controlled";

        /// <summary>
        /// PlayerSession.PlayerId owning this entity. Enables entity → session lookup.
        /// Empty string allowed transiently (attached before session is resolved).
        /// </summary>
        public string PlayerId { get; set; } = "";

        /// <summary>
        /// Currently locked target entity id. Set by HandleSetTarget from the
        /// client's T-cycle. Read by WeaponSystem when firing to resolve who
        /// the laser is pointed at.
        /// Null = no lock → weapons fire straight ahead along heading.
        /// </summary>
        public string? CurrentTargetId { get; set; }

        /// <summary>
        /// Inertial dampeners (Space Engineers style).
        ///   TRUE  (default) — when pilot releases controls, maneuver thrusters
        ///                     auto-brake the ship back to zero velocity.
        ///   FALSE           — true Newtonian drift. Ship keeps its velocity
        ///                     forever until pilot applies input or hits something.
        /// Toggled by the Z key.
        /// </summary>
        public bool DampenersOn { get; set; } = true;

        public PlayerControlledComponent() { }

        public PlayerControlledComponent(string playerId)
        {
            PlayerId = playerId;
        }

        public override void Tick(Entity owner, double deltaTime) { }
    }
}
