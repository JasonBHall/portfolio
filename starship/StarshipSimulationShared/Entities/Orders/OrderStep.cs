using System.Collections.Generic;
using System.Numerics;

namespace StarshipSimulation.Shared.Entities.Orders
{
    /// <summary>
    /// A single step within an Order.
    /// Either a movement (MoveTo) or an interaction (Interact).
    /// </summary>
    public class OrderStep
    {
        public StepType StepType { get; set; }

        // ------------------------------------------------------------
        // MoveTo
        // ------------------------------------------------------------

        /// <summary>
        /// Entity to move toward — structure, gate, ship, etc.
        /// Takes priority over TargetPosition if both are set.
        /// </summary>
        public string? TargetEntityId { get; set; }

        /// <summary>
        /// World coordinate to move toward.
        /// Used when there is no specific target entity.
        /// </summary>
        public Vector2? TargetPosition { get; set; }

        // ------------------------------------------------------------
        // Interact
        // ------------------------------------------------------------

        /// <summary>
        /// Action to perform at destination.
        /// e.g. "Load", "Unload", "Jump", "Dock", "Attack"
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Parameters for the action — resource type, amount, etc.
        /// </summary>
        public Dictionary<string, string>? Parameters { get; set; }
    }
}
