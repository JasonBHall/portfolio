namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// The type of propulsion an engine module provides.
    /// Each drive type has distinct physics, costs, and vulnerabilities.
    /// See Core Truths — Movement for full design specification.
    /// </summary>
    public enum DriveType
    {
        /// <summary>
        /// Newtonian thrust along heading vector.
        /// Full acceleration/deceleration. Maneuver thrusters for rotation
        /// and lateral translation. Contextual drag when no plan is active.
        /// Fuel: hydrogen.
        /// </summary>
        Sublight,

        /// <summary>
        /// Straight-line high-speed transit along entry heading.
        /// Cannot maneuver during transit. Preserves entry velocity on exit.
        /// Requires dilithium fuel. Cannot warp without it.
        /// </summary>
        Warp,

        /// <summary>
        /// Instantaneous long-range transit. Requires full stop to initiate.
        /// Arrives at zero velocity. Severe energy cost — long recharge after jump.
        /// Ship is vulnerable at both departure and arrival.
        /// </summary>
        Jump,

        /// <summary>
        /// Lateral and rotational authority only. No forward thrust.
        /// Ships may have dedicated maneuver thruster modules for fine control.
        /// Fuel: hydrogen (combat use consumes significantly more than cruise).
        /// </summary>
        ManeuverOnly
    }

    /// <summary>
    /// Fuel types consumed by engine and power modules.
    /// Each type has a distinct source, scarcity, and consumer set.
    /// </summary>
    public enum FuelType
    {
        /// <summary>
        /// Standard sublight and maneuver thruster fuel.
        /// Collectable from space — hydrogen scoops, gas giants, nebulae.
        /// Combat maneuvers consume dramatically more than cruise travel.
        /// </summary>
        Hydrogen,

        /// <summary>
        /// Warp drive fuel. High energy density.
        /// Produces warp power specifically — warp drives cannot substitute hydrogen.
        /// Regenerates from the crystal as long as any remains (even 0.01 units).
        /// Replenishable at starbases or via tender ships.
        /// </summary>
        Dilithium,

        /// <summary>
        /// Fissile material for fission reactors.
        /// Moderate output, produces waste products as a byproduct.
        /// </summary>
        FissileMaterial,

        /// <summary>
        /// No fuel required. Module runs on energy alone.
        /// </summary>
        None
    }
}
