using System;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Optional base class for components.
    /// Provides default implementations and common boilerplate.
    /// Components do not have to extend this — implementing IComponent directly is fine.
    /// </summary>
    public abstract class ComponentBase : IComponent
    {
        // ------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------

        /// <summary>
        /// Unique identifier for this component instance.
        /// Generated on construction — stable for the lifetime of the component.
        /// </summary>
        public string ComponentId { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display name. Override in derived classes.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Whether this component is active.
        /// Inactive components are skipped by systems.
        /// Default: true.
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ------------------------------------------------------------
        // Tick
        // ------------------------------------------------------------

        /// <summary>
        /// Called each simulation tick by the relevant system.
        /// Override to implement per-tick behaviour.
        /// Base implementation is a no-op — safe to not call base.
        /// </summary>
        public virtual void Tick(Entity owner, double deltaTime) { }
    }
}
