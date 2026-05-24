using System.Collections.Generic;
using System.Numerics;

namespace StarshipSimulation.Shared.Messages
{
    /// <summary>
    /// A pre-calculated combat or simulation event sent to the client
    /// for visual playback.
    ///
    /// The server has already resolved the outcome. The client plays it
    /// out over Duration seconds for the player.
    ///
    /// Sent on the /ws/events channel immediately when the outcome is
    /// resolved — not tick-bound.
    ///
    /// Duration is always set by the server. The client never decides
    /// how long an event takes to play out.
    /// </summary>
    public class NarrativeEvent
    {
        // ------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------

        public string Id   { get; set; } = "";

        /// <summary>
        /// Event classification. Drives which visual sequence the client plays.
        ///
        /// Known kinds:
        ///   "swarm_attrition"     — point defense/fighters reducing a swarm
        ///   "entity_destroyed"    — a discrete entity was killed
        ///   "weapon_fired"        — a weapon discharged
        ///   "torpedo_impact"      — torpedo hit a target
        ///   "asteroid_damaged"    — asteroid took damage, fragments spawned
        ///   "carrier_launch"      — carrier deployed a squadron
        ///   "jump_initiated"      — entity began a jump sequence
        ///   "wormhole_collapse"   — a wormhole became unstable and closed
        /// </summary>
        public string Kind { get; set; } = "";

        /// <summary>
        /// Server tick at which this event occurred.
        /// </summary>
        public long Tick { get; set; }

        // ------------------------------------------------------------
        // Playback
        // ------------------------------------------------------------

        /// <summary>
        /// How long the client should take to play out this event, in seconds.
        /// Set by the server. Client does not override this.
        ///
        /// Guidance:
        ///   0.0 – 0.5s  →  hit flash, single shot, small explosion
        ///   0.5 – 3.0s  →  attrition sequence, dogfight burst
        ///   3.0 – 10s   →  carrier launch, asteroid breakup, large battle
        /// </summary>
        public float Duration { get; set; }

        // ------------------------------------------------------------
        // Source and target
        // ------------------------------------------------------------

        /// <summary>
        /// The entity that caused the event (attacker, launcher, etc.)
        /// Null for environmental events.
        /// </summary>
        public string? SourceId { get; set; }

        /// <summary>
        /// The entity the event is happening to.
        /// Null for untargeted events (e.g. area explosions).
        /// </summary>
        public string? TargetId { get; set; }

        /// <summary>
        /// World position where the event occurs.
        /// Used when source/target positions are insufficient
        /// (e.g. midpoint of a dogfight, impact location).
        /// </summary>
        public Vector2? Position { get; set; }

        // ------------------------------------------------------------
        // Outcome data
        // Populated based on Kind — not all fields apply to all events.
        // ------------------------------------------------------------

        /// <summary>
        /// For attrition events: count before the event.
        /// </summary>
        public int? StartCount { get; set; }

        /// <summary>
        /// For attrition events: count after the event.
        /// </summary>
        public int? EndCount { get; set; }

        /// <summary>
        /// For attrition events: number of units lost.
        /// </summary>
        public int? Losses { get; set; }

        /// <summary>
        /// What caused this event.
        /// e.g. "point_defense", "fighter_intercept", "flak", "ecm"
        /// </summary>
        public string? Cause { get; set; }

        /// <summary>
        /// Flexible payload for event-specific data that doesn't fit
        /// the standard fields. Client reads this for specialised VFX.
        /// e.g. { "fragmentCount": "12", "asteroidSize": "large" }
        /// </summary>
        public Dictionary<string, string> Data { get; set; } = new();
    }
}
