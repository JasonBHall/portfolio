using System;

namespace StarshipSimulation.Shared.Economy
{
    public enum TradeJobStatus
    {
        Pending   = 0,  // created, not yet assigned
        Assigned  = 1,  // ship assigned, en route to provider
        InTransit = 2,  // loaded, en route to requester
        Completed = 3,
        Failed    = 4
    }

    /// <summary>
    /// A single logistics contract — move a resource from a provider entity
    /// to a requester entity, via an assigned ship entity.
    ///
    /// Amounts are in UNITS (not stacks) — the scheduler converts from
    /// stacks to units when creating a job. Systems and storage always
    /// work in whole units for physical transfers.
    ///
    /// In-transit jobs count toward need calculations in the scheduler —
    /// a requester with 200 units in-transit is treated as already having
    /// those units for need scoring purposes. This prevents clustering.
    ///
    /// See Core Truths — Economy System, Trade Scheduling Algorithm.
    /// </summary>
    public class TradeJob
    {
        // ------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------

        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Incremented on status change. Used by delta system.</summary>
        public long Version { get; set; } = 0;

        // ------------------------------------------------------------
        // Contract details
        // ------------------------------------------------------------

        /// <summary>Resource name — matches ResourceRegistry key.</summary>
        public string Resource { get; set; } = "";

        /// <summary>Amount to deliver, in units.</summary>
        public int Amount { get; set; }

        /// <summary>Entity Id of the provider (has output surplus).</summary>
        public string FromEntityId { get; set; } = "";

        /// <summary>Entity Id of the requester (has input need).</summary>
        public string ToEntityId { get; set; } = "";

        // ------------------------------------------------------------
        // Assignment
        // ------------------------------------------------------------

        public TradeJobStatus Status       { get; set; } = TradeJobStatus.Pending;
        public string?        AssignedShipId { get; set; }

        // ------------------------------------------------------------
        // Tracking
        // ------------------------------------------------------------

        /// <summary>UTC time job was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time job was completed or failed.</summary>
        public DateTime? CompletedAt { get; set; }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        public bool IsActive => Status is TradeJobStatus.Pending
                                       or TradeJobStatus.Assigned
                                       or TradeJobStatus.InTransit;

        public void AdvanceStatus(TradeJobStatus newStatus)
        {
            Status = newStatus;
            Version++;

            if (newStatus is TradeJobStatus.Completed or TradeJobStatus.Failed)
                CompletedAt = DateTime.UtcNow;
        }
    }
}
