// StarshipSimulation.Shared/Entities/MovementProfile.cs

namespace StarshipSimulation.Shared.Entities
{
    /// <summary>
    /// Defines how an entity uses its engines during a traversal arc.
    ///
    /// Profiles are toggleable and available to all entity types — a freighter
    /// on a supply run, a warship approaching a fleet, a scout conserving fuel.
    /// The entity's current profile is stored on Entity.CurrentProfile and
    /// consulted by TraversalPlanner when building a flight plan.
    ///
    /// All profiles produce the same four-phase arc shape (Accel/Coast/Flip/Brake)
    /// with different scale factors, EXCEPT Military which omits the braking phase
    /// (arrives at combat speed) and Economy which reduces thrust and cruise speed.
    ///
    /// Future: energy-based drives use ThrustFraction identically to fuel drives.
    /// Future: profile UI via sliders — this record is the data contract.
    ///
    /// See Core Truths — Traversal Plan.
    /// </summary>
    public record MovementProfile
    {
        // ------------------------------------------------------------
        // Profile identity
        // ------------------------------------------------------------

        public string Name { get; init; } = "standard";

        // ------------------------------------------------------------
        // Engine usage fractions (0.0 – 1.0 of ship's maximum capability)
        // ------------------------------------------------------------

        /// <summary>
        /// Fraction of TotalThrust to apply during accel and braking phases.
        /// Economy = 0.4 (saves fuel, longer trip). Standard/Military = 1.0.
        /// Also applies to energy drives — lower fraction = less energy draw.
        /// </summary>
        public float ThrustFraction { get; init; } = 1.0f;

        /// <summary>
        /// Fraction of MaxSpeed to use as cruise speed.
        /// Economy = 0.7 (lower speed = dramatically less flip runway needed).
        /// Standard/Military = 1.0.
        /// </summary>
        public float SpeedFraction { get; init; } = 1.0f;

        // ------------------------------------------------------------
        // Arrival behaviour
        // ------------------------------------------------------------

        /// <summary>
        /// When true: standard docking approach — flip and brake to zero velocity.
        /// When false: military approach — skip braking phase, arrive at cruise speed.
        /// Military ships arrive in combat posture with velocity intact.
        /// Gate/wormhole/stargate transits always require full stop regardless.
        /// </summary>
        public bool BrakeOnArrival { get; init; } = true;

        // ------------------------------------------------------------
        // Fuel conservation
        // ------------------------------------------------------------

        /// <summary>
        /// When entity fuel falls below this fraction of maximum, automatically
        /// downgrade to Economy profile regardless of current assignment.
        /// 0 = never auto-downgrade. Default 0.15 = downgrade at 15% fuel.
        /// Logs a warning on the entity bridge log when triggered.
        /// </summary>
        public float FuelConservationThreshold { get; init; } = 0.15f;

        // ------------------------------------------------------------
        // Named presets
        // ------------------------------------------------------------

        /// <summary>
        /// Full thrust, full speed, docking arrival.
        /// Baseline for most NPC haulers and logistics ships.
        /// </summary>
        public static readonly MovementProfile Standard = new()
        {
            Name                     = "standard",
            ThrustFraction           = 1.0f,
            SpeedFraction            = 1.0f,
            BrakeOnArrival           = true,
            FuelConservationThreshold = 0.15f,
        };

        /// <summary>
        /// Reduced thrust and cruise speed. Longer trip, significantly less fuel.
        /// Ideal for: long-haul freighters, fuel-critical situations, patrol transit.
        /// At 0.4 thrust / 0.7 speed, fuel consumption roughly halves versus Standard.
        /// </summary>
        public static readonly MovementProfile Economy = new()
        {
            Name                     = "economy",
            ThrustFraction           = 0.4f,
            SpeedFraction            = 0.7f,
            BrakeOnArrival           = true,
            FuelConservationThreshold = 0.10f,
        };

        /// <summary>
        /// Full thrust, full speed. Same as Standard — reserved for future
        /// overburn/afterburner mechanics that push beyond rated MaxSpeed.
        /// </summary>
        public static readonly MovementProfile Speed = new()
        {
            Name                     = "speed",
            ThrustFraction           = 1.0f,
            SpeedFraction            = 1.0f,
            BrakeOnArrival           = true,
            FuelConservationThreshold = 0.20f,  // burns fuel fast, conserve earlier
        };

        /// <summary>
        /// Full thrust, full speed, NO braking phase.
        /// Arrives at cruise speed in combat posture — weapons hot, velocity intact.
        /// Inappropriate for docking or gate transit (those always require full stop).
        /// TraversalPlanner overrides to BrakeOnArrival=true for gate/wormhole steps.
        /// </summary>
        public static readonly MovementProfile Military = new()
        {
            Name                     = "military",
            ThrustFraction           = 1.0f,
            SpeedFraction            = 1.0f,
            BrakeOnArrival           = false,
            FuelConservationThreshold = 0.10f,
        };
    }
}
