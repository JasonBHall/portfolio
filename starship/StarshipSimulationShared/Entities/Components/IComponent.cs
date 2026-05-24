using System.Collections.Generic;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Contract for all components in the simulation.
    /// Components attach to Entities and define their capabilities.
    /// Systems iterate components to drive all simulation behaviour.
    ///
    /// Rules:
    ///   - Components own their own state.
    ///   - Components do not reference other entities directly — they
    ///     communicate via the entity they are attached to.
    ///   - Tick is called by systems, not by the entity itself.
    /// </summary>
    public interface IComponent
    {
        /// <summary>
        /// Unique identifier for this component instance.
        /// Useful for targeting a specific hardpoint component by reference.
        /// </summary>
        string ComponentId { get; }

        /// <summary>
        /// Display name — used for UI, logging, and debugging.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether this component is currently active.
        /// Inactive components are skipped by systems.
        /// Allows disabling without removing (e.g. powered down, damaged).
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// Called each simulation tick by the relevant system.
        /// Owner is always an Entity — no casting required.
        /// deltaTime is in seconds.
        /// </summary>
        void Tick(Entity owner, double deltaTime);
    }
}
