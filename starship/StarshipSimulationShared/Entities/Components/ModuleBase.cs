using System;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Base class for all physical modules.
    /// Provides default implementations of IModule boilerplate.
    /// Concrete modules extend this and override what they need.
    /// </summary>
    public abstract class ModuleBase : ComponentBase, IModule
    {
        // ------------------------------------------------------------
        // IComponent — identity
        // ------------------------------------------------------------

        // ComponentId and IsActive inherited from ComponentBase

        /// <summary>
        /// Internal system name. Override in derived class.
        /// e.g. "sublight_engine", "warp_drive", "cargo_pod"
        /// </summary>
        public abstract override string Name { get; }

        // ------------------------------------------------------------
        // IModule — player-facing identity
        // ------------------------------------------------------------

        /// <summary>
        /// In-game display name. Override in derived class.
        /// e.g. "Dothraki Horse Drive Mk.II"
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Flavour description for the inventory UI.
        /// Override in derived class.
        /// </summary>
        public virtual string Description => string.Empty;

        // ------------------------------------------------------------
        // IModule — physical properties
        // ------------------------------------------------------------

        public abstract string SlotType { get; }
        public abstract int    SlotSize { get; }
        public abstract float  Mass     { get; }

        // ------------------------------------------------------------
        // IModule — installation state
        // ------------------------------------------------------------

        public bool    IsInstalled         { get; set; } = false;
        public string? InstalledInHardpoint { get; set; } = null;

        // ------------------------------------------------------------
        // IModule — condition
        // ------------------------------------------------------------

        public float Condition { get; set; } = 1.0f;
        public bool  IsOnline  { get; set; } = true;

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        /// <summary>
        /// Effective output multiplier combining condition and online state.
        /// Systems multiply their output values by this factor.
        /// 0 = offline or destroyed, 1 = perfect condition.
        /// </summary>
        public float EffectiveFactor =>
            (!IsOnline || Condition <= 0f) ? 0f : Condition;

        /// <summary>
        /// Whether this module is contributing to the simulation.
        /// False if offline, destroyed, or not installed.
        /// </summary>
        public bool IsOperational =>
            IsInstalled && IsOnline && Condition > 0f && IsActive;

        // ------------------------------------------------------------
        // IComponent — tick
        // Modules that need per-tick behaviour override this.
        // Default: no-op (most modules are pure data read by systems).
        // ------------------------------------------------------------

        public override void Tick(Entity owner, double deltaTime) { }
    }
}
