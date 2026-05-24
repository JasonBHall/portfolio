using System.Collections.Generic;

namespace StarshipSimulation.Shared.Messages
{
    /// <summary>
    /// The primary outbound message from server to client.
    /// Sent on the /ws/universe channel at the micro tick rate (~20Hz).
    ///
    /// Contains only what has changed since the last update sent to
    /// this specific client — delta compressed, observer culled.
    ///
    /// Message types:
    ///   "snapshot" — full state, sent on first connect or reconnect
    ///   "delta"    — only changed/new/removed entities since last tick
    /// </summary>
    public class TickUpdate
    {
        /// <summary>
        /// "snapshot" or "delta"
        /// </summary>
        public string Type { get; set; } = "delta";

        /// <summary>
        /// Server macro tick counter at time of send.
        /// Client uses this to detect missed updates and request a resync.
        /// </summary>
        public long Tick { get; set; }

        // ------------------------------------------------------------
        // Entity sets
        // Populated based on Type:
        //   snapshot → Entities contains ALL visible entities
        //   delta    → Entities contains only CHANGED entities
        // ------------------------------------------------------------

        /// <summary>
        /// Entities that are new or have changed since the last update.
        /// Keyed by entity Id for fast client-side lookup and merge.
        /// </summary>
        public Dictionary<string, EntityState> Entities { get; set; } = new();

        /// <summary>
        /// Ids of entities that have been destroyed or left observer range.
        /// Client should despawn these immediately.
        /// </summary>
        public List<string> Removed { get; set; } = new();
    }
}
