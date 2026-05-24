using System.Collections.Generic;

namespace StarshipSimulation.Shared.Messages
{
    /// <summary>
    /// Server response to a "request_entity_log" command.
    /// Sent once on-demand when a client selects an entity.
    /// Not included in the regular tick stream.
    ///
    /// The client identifies this message by the "type" = "entity_log"
    /// field and routes it to the inspector panel.
    /// </summary>
    public class EntityLogResponse
    {
        public string Type { get; set; } = "entity_log";

        public string EntityId   { get; set; } = "";
        public string EntityName { get; set; } = "";

        /// <summary>Current one-line status at time of request.</summary>
        public string CurrentStatus { get; set; } = "";

        /// <summary>Current intent (job, patrol, standby) at time of request.</summary>
        public string CurrentIntent { get; set; } = "";

        /// <summary>
        /// Log entries, newest first, pre-formatted as strings.
        /// e.g. "[14:23:01] [Trade] Awarded contract: 5,000 ironOre"
        /// </summary>
        public List<string> Entries { get; set; } = new();
    }
}
