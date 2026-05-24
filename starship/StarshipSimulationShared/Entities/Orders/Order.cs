using System;
using System.Collections.Generic;

namespace StarshipSimulation.Shared.Entities.Orders
{
    /// <summary>
    /// An autonomous instruction assigned to an entity.
    /// Consists of an ordered list of steps the entity executes in sequence.
    /// Null CurrentOrder means the entity is idle.
    /// </summary>
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// High-level category — used by OrderSystem to route processing.
        /// </summary>
        public OrderType Type { get; set; }

        /// <summary>
        /// Ordered steps to execute.
        /// </summary>
        public List<OrderStep> Steps { get; set; } = new();

        /// <summary>
        /// Index of the step currently being executed.
        /// </summary>
        public int CurrentStepIndex { get; set; } = 0;

        /// <summary>
        /// Convenience — returns the active step, or null if complete.
        /// </summary>
        public OrderStep? CurrentStep =>
            CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;
    }
}
