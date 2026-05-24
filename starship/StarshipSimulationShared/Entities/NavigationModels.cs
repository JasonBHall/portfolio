using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace StarshipSimulation.Shared.Entities
{
    // ------------------------------------------------------------
    // Movement mode — what kind of travel a segment represents
    // ------------------------------------------------------------

    public enum MovementMode
    {
        Sublight         = 0,  // Newtonian thrust, velocity persists
        Warp             = 1,  // Constant speed straight line, entry velocity preserved on exit
        JumpCharging     = 2,  // Ship holds position, accumulates charge — vulnerable
        JumpExecuting    = 3,  // Instantaneous transit, arrives at zero velocity
        GateTransfer     = 4,  // Instantaneous, paired gate, zero velocity on exit
        WormholeTransfer = 5,  // Instantaneous, slightly unpredictable, zero velocity
        StargateTransfer = 6   // Instantaneous, network gate, zero velocity
    }

    // ------------------------------------------------------------
    // A single leg of a route
    // ------------------------------------------------------------

    /// <summary>
    /// One step in a NavigationResult. MovementSystem executes these
    /// in sequence from ActiveSegmentIndex.
    ///
    /// DurationSeconds for JumpCharging = the charge time the ship must hold.
    /// DurationSeconds for instantaneous modes (Jump, Gate, etc.) = 0.
    /// DurationSeconds for Sublight/Warp = estimated travel time (actual
    /// time varies with Newtonian physics — this is the planning estimate).
    /// </summary>
    public class MovementSegment
    {
        public MovementMode Mode            { get; set; }
        public Vector2      From            { get; set; }
        public Vector2      To              { get; set; }
        public double       DurationSeconds { get; set; }

        public bool IsInstantaneous =>
            Mode is MovementMode.JumpExecuting
                 or MovementMode.GateTransfer
                 or MovementMode.WormholeTransfer
                 or MovementMode.StargateTransfer;
    }

    // ------------------------------------------------------------
    // The result of a route plan
    // ------------------------------------------------------------

    /// <summary>
    /// Output of NavigationSystem.PlanRoute() and EstimateTravelTime().
    ///
    /// TotalSeconds is the planning estimate — the sum of all segment durations.
    /// Actual travel time varies slightly due to Newtonian physics, but
    /// TotalSeconds is accurate enough for trade bidding comparisons.
    ///
    /// Segments are executed in order by MovementSystem.
    /// Jump edges are expanded into JumpCharging + JumpExecuting pairs.
    ///
    /// NavigationResult is cached by NavigationSystem keyed by
    /// entity pair + capability profile. Both TotalSeconds and Segments
    /// are stored so PlanRoute benefits from cache hits too.
    /// </summary>
    public class NavigationResult
    {
        public double                TotalSeconds { get; set; }
        public List<MovementSegment> Segments     { get; set; } = new();

        /// <summary>True if a valid route was found.</summary>
        public bool IsReachable => !double.IsInfinity(TotalSeconds) && TotalSeconds >= 0;

        // ------------------------------------------------------------
        // Factory helpers
        // ------------------------------------------------------------

        /// <summary>No valid route exists.</summary>
        public static NavigationResult Unreachable() => new()
        {
            TotalSeconds = double.PositiveInfinity,
            Segments     = new()
        };

        /// <summary>Already at destination — zero cost, single zero-duration segment.</summary>
        public static NavigationResult Immediate(Vector2 position) => new()
        {
            TotalSeconds = 0,
            Segments = new()
            {
                new MovementSegment
                {
                    Mode            = MovementMode.Sublight,
                    From            = position,
                    To              = position,
                    DurationSeconds = 0
                }
            }
        };
    }
}
