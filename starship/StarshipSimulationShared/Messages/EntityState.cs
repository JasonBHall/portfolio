using System.Collections.Generic;
using System.Numerics;

namespace StarshipSimulation.Shared.Messages
{
    /// <summary>
    /// The wire representation of a single entity.
    /// Sent from server to all clients as part of tick updates and snapshots.
    ///
    /// Fidelity varies by entity class — the server populates only what
    /// the client needs for that kind of entity:
    ///
    ///   Ships / Stations  — full state
    ///   Swarms / Missiles — position + velocity + count only
    ///   Debris            — position only
    /// </summary>
    public class EntityState
    {
        // ------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------

        public string Id   { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>
        /// Classification hint for the client.
        /// e.g. "ship", "station", "missile_swarm", "fighter_squadron",
        ///       "asteroid", "debris", "beacon", "gate"
        /// </summary>
        public string Kind { get; set; } = "";

        /// <summary>
        /// Server tick this state was captured at.
        /// Used by client to order updates correctly.
        /// </summary>
        public long Tick { get; set; }

        /// <summary>
        /// Incremented by the server each time this entity changes.
        /// Client uses this to skip redundant updates.
        /// </summary>
        public long Version { get; set; }

        // ------------------------------------------------------------
        // Physics
        // ------------------------------------------------------------

        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public Vector2 Heading  { get; set; }

        // ------------------------------------------------------------
        // Swarm / attrition state
        // Populated only for swarm-class entities (kind = "missile_swarm" etc.)
        // ------------------------------------------------------------

        /// <summary>
        /// Current unit count. Null for non-swarm entities.
        /// </summary>
        public int? Count { get; set; }

        /// <summary>
        /// Visual spread radius of the swarm. Null for non-swarm entities.
        /// </summary>
        public float? Spread { get; set; }

        /// <summary>
        /// Id of the entity this swarm is targeting. Null if none.
        /// </summary>
        public string? TargetId { get; set; }

        // ------------------------------------------------------------
        // Bridge log — lightweight fields sent every tick
        // Full log entries are fetched on-demand via RequestEntityLog command
        // ------------------------------------------------------------

        /// <summary>
        /// What the entity is doing right now. One sentence.
        /// e.g. "Moving to Iron Mine 1 — 1,200m remaining"
        /// </summary>
        public string CurrentStatus { get; set; } = "idle";

        /// <summary>
        /// The broader goal — current job, patrol, standby.
        /// e.g. "Trade job abc123: deliver 5,000 ironOre → Iron Smelter 1"
        /// </summary>
        public string CurrentIntent { get; set; } = "";

        /// <summary>
        /// World-space destination of the current traversal plan.
        /// Null when idle. Used by the map to draw the route line.
        /// </summary>
        public Vector2? RouteDestination { get; set; }

        // ------------------------------------------------------------
        // Component summary
        // Lightweight key/value map — not full component state.
        // Used by client for HUD display and targeting decisions.
        // e.g. { "cargo": "40/50", "power": "80%", "shields": "100%" }
        // ------------------------------------------------------------

        public Dictionary<string, string> ComponentSummary { get; set; } = new();
    }
}
