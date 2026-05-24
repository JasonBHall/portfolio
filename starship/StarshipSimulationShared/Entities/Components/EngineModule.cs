using System;
using System.Numerics;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// A physical engine module that snaps into an engine hardpoint.
    ///
    /// Handles all three drive types — Sublight, Warp, Jump — as well as
    /// dedicated ManeuverOnly modules. A ship may have multiple engine modules
    /// of different types. MovementSystem aggregates their capabilities.
    ///
    /// Design: see Core Truths — Movement, Energy and Fuel, Commitment and Consequence.
    ///
    /// Examples of concrete engine modules built on this:
    ///   "Dothraki Horse Drive"    — heavy sublight, massive thrust, drinks hydrogen
    ///   "Helios Warp Sled"        — efficient warp, modest dilithium use
    ///   "Quantum Jump Core"       — extreme range jump, catastrophic energy cost
    ///   "Gecko RCS Pack"          — maneuver thrusters only, high agility
    /// </summary>
    public class EngineModule : ModuleBase
    {
        // ------------------------------------------------------------
        // IModule — identity
        // ------------------------------------------------------------

        public override string Name        { get; }
        public override string DisplayName { get; }
        public override string Description { get; }
        public override string SlotType    => "engine";
        public override int    SlotSize    { get; }
        public override float  Mass        { get; }

        // ------------------------------------------------------------
        // Drive type
        // ------------------------------------------------------------

        /// <summary>
        /// What kind of propulsion this module provides.
        /// A ship needs at least one Sublight module to maneuver.
        /// Warp and Jump modules add those capabilities independently.
        /// </summary>
        public DriveType DriveType { get; }

        // ------------------------------------------------------------
        // Sublight capabilities
        // Relevant when DriveType == Sublight or ManeuverOnly
        // ------------------------------------------------------------

        /// <summary>
        /// Force applied per tick at full throttle, in simulation units.
        /// Higher = faster acceleration. Offset by entity mass.
        /// </summary>
        public float MaxThrust { get; }

        /// <summary>
        /// Terminal velocity — maximum speed achievable under sustained thrust.
        /// Multiple sublight engines take the highest MaxSpeed (not additive).
        /// </summary>
        public float MaxSpeed { get; }

        /// <summary>
        /// Rotation speed in radians per second at full maneuver thruster output.
        /// Capital ships: very low. Fighters: high.
        /// </summary>
        public float TurnRate { get; }

        /// <summary>
        /// Lateral translation rate (strafe) in units per second.
        /// How quickly maneuver thrusters can cancel sideways drift.
        /// Capital ships: negligible. Scouts: moderate.
        /// </summary>
        public float LateralAuthority { get; }

        /// <summary>
        /// Fraction of forward thrust delivered when the pilot commands
        /// reverse (S key, negative throttle). 0.5 = half power.
        ///
        /// Encourages "flip and burn" maneuvers over simple reverse crawl —
        /// if you want to stop quickly from cruise, rotate 180° and hit W
        /// at full power rather than hold S at half power.
        /// </summary>
        public float ReverseThrustFraction { get; }

        // ------------------------------------------------------------
        // Warp capabilities
        // Relevant when DriveType == Warp
        // ------------------------------------------------------------

        /// <summary>
        /// Speed during warp transit in units per second.
        /// Multiple warp modules are additive.
        /// </summary>
        public float WarpSpeed { get; }

        /// <summary>
        /// Seconds to spool up before entering warp.
        /// Ship is not moving during charge but is committed to the vector.
        /// Multiple warp modules: take the longest charge time (bottleneck).
        /// </summary>
        public float WarpChargeTime { get; }

        // ------------------------------------------------------------
        // Jump capabilities
        // Relevant when DriveType == Jump
        // ------------------------------------------------------------

        /// <summary>
        /// Maximum jump distance in simulation units.
        /// Multiple jump drives: take the longest range (not additive).
        /// </summary>
        public float JumpRange { get; }

        /// <summary>
        /// Seconds to charge from zero velocity before jump executes.
        /// Ship is stationary and vulnerable throughout.
        /// </summary>
        public float JumpChargeTime { get; }

        /// <summary>
        /// Energy units required to fire the jump drive.
        /// This is the severe energy cost referenced in Core Truths.
        /// After firing, the drive recharges at EnergyDrawPerTick rate.
        /// </summary>
        public float JumpEnergyCost { get; }

        /// <summary>
        /// Seconds before the jump drive can fire again after a jump.
        /// Ship must have energy and wait this duration — it cannot rush it.
        /// </summary>
        public float JumpRechargeTime { get; }

        // ------------------------------------------------------------
        // Energy
        // ------------------------------------------------------------

        /// <summary>
        /// Energy drawn from ship's power pool per tick while active.
        /// Higher capability = higher draw. This is the commitment cost.
        /// </summary>
        public float EnergyDrawPerTick { get; }

        // ------------------------------------------------------------
        // Fuel
        // ------------------------------------------------------------

        public bool     UsesFuel     { get; }
        public FuelType FuelType     { get; }
        public float    MaxFuel      { get; }
        public float    RechargeRate { get; }   // passive fuel regeneration per tick

        // ------------------------------------------------------------
        // Runtime state — managed by MovementSystem each tick
        // ------------------------------------------------------------

        /// <summary>Current fuel level. Depletes on use, recharges passively.</summary>
        public float CurrentFuel { get; set; }

        /// <summary>Current throttle. 0 = off, 1 = full thrust, -1 = full reverse.</summary>
        public float CurrentThrottle { get; set; }

        /// <summary>
        /// Current lateral strafe input. -1 = full left, 0 = none, +1 = full right.
        /// Strafe acceleration = LateralAuthority × CurrentLateralInput, applied
        /// perpendicular to entity.Heading by MovementSystem each tick.
        /// Set by CommandHandler from Q/E keys.
        /// </summary>
        public float CurrentLateralInput { get; set; }

        /// <summary>Current warp charge progress. 0→1. 1 = ready to engage warp.</summary>
        public float WarpChargeProgress { get; set; }

        /// <summary>Whether the ship is currently in warp transit.</summary>
        public bool IsWarping { get; set; }

        /// <summary>Current jump charge progress. 0→1. 1 = ready to jump.</summary>
        public float JumpChargeProgress { get; set; }

        /// <summary>Whether the jump drive is currently charging (ship must be at zero velocity).</summary>
        public bool IsJumpCharging { get; set; }

        /// <summary>
        /// Time remaining on jump recharge cooldown after firing.
        /// Drive cannot fire again until this reaches 0.
        /// </summary>
        public float JumpRechargeCooldown { get; set; }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public EngineModule(EngineModuleConfig config)
        {
            Name        = config.Name;
            DisplayName = config.DisplayName;
            Description = config.Description;
            SlotSize    = config.SlotSize;
            Mass        = config.Mass;
            DriveType   = config.DriveType;

            MaxThrust             = config.MaxThrust;
            MaxSpeed              = config.MaxSpeed;
            TurnRate              = config.TurnRate;
            LateralAuthority      = config.LateralAuthority;
            ReverseThrustFraction = config.ReverseThrustFraction;

            WarpSpeed       = config.WarpSpeed;
            WarpChargeTime  = config.WarpChargeTime;

            JumpRange        = config.JumpRange;
            JumpChargeTime   = config.JumpChargeTime;
            JumpEnergyCost   = config.JumpEnergyCost;
            JumpRechargeTime = config.JumpRechargeTime;

            EnergyDrawPerTick = config.EnergyDrawPerTick;

            UsesFuel     = config.UsesFuel;
            FuelType     = config.FuelType;
            MaxFuel      = config.MaxFuel;
            RechargeRate = config.RechargeRate;
            CurrentFuel  = config.MaxFuel; // start full
        }

        // ------------------------------------------------------------
        // Capability helpers — used by MovementSystem
        // ------------------------------------------------------------

        /// <summary>
        /// Effective thrust accounting for condition, throttle, and fuel.
        /// Returns 0 if not operational or out of fuel.
        /// </summary>
        public float GetEffectiveThrust()
        {
            if (!IsOperational) return 0f;
            if (UsesFuel && CurrentFuel <= 0f) return 0f;
            return MaxThrust * EffectiveFactor * Math.Abs(CurrentThrottle);
        }

        /// <summary>
        /// Effective max speed accounting for condition.
        /// </summary>
        public float GetEffectiveMaxSpeed() =>
            IsOperational ? MaxSpeed * EffectiveFactor : 0f;

        /// <summary>
        /// Effective turn rate accounting for condition.
        /// </summary>
        public float GetEffectiveTurnRate() =>
            IsOperational ? TurnRate * EffectiveFactor : 0f;

        /// <summary>
        /// Whether this module can currently provide warp capability.
        /// Requires: operational, fuel, not already warping, charged.
        /// </summary>
        public bool CanWarp =>
            IsOperational &&
            DriveType == DriveType.Warp &&
            CurrentFuel > 0f;

        /// <summary>
        /// Whether this module can initiate a jump.
        /// Requires: operational, fully charged, no cooldown remaining.
        /// Ship velocity = 0 is enforced by MovementSystem, not here.
        /// </summary>
        public bool CanJump =>
            IsOperational &&
            DriveType == DriveType.Jump &&
            JumpRechargeCooldown <= 0f &&
            !IsJumpCharging;

        // ------------------------------------------------------------
        // Fuel tick — called by MovementSystem each micro tick
        // ------------------------------------------------------------

        /// <summary>
        /// Passively regenerates fuel up to MaxFuel.
        /// Dilithium regenerates as long as any crystal remains.
        /// Hydrogen regenerates if collection equipment is available (handled externally).
        /// </summary>
        public void TickFuel(double deltaTime)
        {
            if (!UsesFuel || CurrentFuel >= MaxFuel) return;

            // Dilithium regenerates as long as any remains
            if (FuelType == FuelType.Dilithium && CurrentFuel <= 0f) return;

            CurrentFuel = Math.Min(MaxFuel, CurrentFuel + RechargeRate * (float)deltaTime);
        }

        /// <summary>
        /// Consumes fuel. Returns the amount actually consumed (may be less if tank is low).
        /// </summary>
        public float ConsumeFuel(float amount)
        {
            if (!UsesFuel) return 0f;
            float consumed = Math.Min(CurrentFuel, amount);
            CurrentFuel -= consumed;
            return consumed;
        }
    }

    // ------------------------------------------------------------
    // Config record — used to define engine module types
    // Lets us define named modules cleanly without subclassing
    // ------------------------------------------------------------

    /// <summary>
    /// Defines the stats for a specific engine module type.
    /// Create one of these per module definition (in ModuleDefinitions or a data file).
    /// Pass to EngineModule constructor to instantiate.
    /// </summary>
    public record EngineModuleConfig
    {
        // Identity
        public string Name        { get; init; } = "engine_module";
        public string DisplayName { get; init; } = "Engine Module";
        public string Description { get; init; } = string.Empty;
        public int    SlotSize    { get; init; } = 4;
        public float  Mass        { get; init; } = 10f;

        // Drive type
        public DriveType DriveType { get; init; } = DriveType.Sublight;

        // Sublight
        public float MaxThrust             { get; init; } = 100f;
        public float MaxSpeed              { get; init; } = 50f;
        public float TurnRate              { get; init; } = 1.0f;
        public float LateralAuthority      { get; init; } = 0.3f;
        public float ReverseThrustFraction { get; init; } = 0.5f;

        // Warp
        public float WarpSpeed      { get; init; } = 0f;
        public float WarpChargeTime { get; init; } = 0f;

        // Jump
        public float JumpRange        { get; init; } = 0f;
        public float JumpChargeTime   { get; init; } = 0f;
        public float JumpEnergyCost   { get; init; } = 0f;
        public float JumpRechargeTime { get; init; } = 0f;

        // Energy
        public float EnergyDrawPerTick { get; init; } = 1f;

        // Fuel
        public bool     UsesFuel     { get; init; } = true;
        public FuelType FuelType     { get; init; } = FuelType.Hydrogen;
        public float    MaxFuel      { get; init; } = 100f;
        public float    RechargeRate { get; init; } = 0f;
    }
}
