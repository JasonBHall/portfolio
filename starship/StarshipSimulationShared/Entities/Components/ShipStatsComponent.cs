using System;
using System.Collections.Generic;
using System.Linq;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Aggregate cache of an entity's current capabilities.
    /// Derived from all installed modules. Updated on demand via dirty flag.
    ///
    /// 99.99% of ticks: systems read cached values directly — no iteration.
    /// On module change: IsDirty is set, cache rebuilds on next read.
    ///
    /// Also used by the fitting UI to preview the effect of installing
    /// or removing a module before committing the change.
    ///
    /// See Core Truths — Section 16.
    /// </summary>
    public class ShipStatsComponent : ComponentBase
    {
        public override string Name => "ship_stats";

        // ------------------------------------------------------------
        // Dirty flag
        // ------------------------------------------------------------

        private bool _isDirty = true;

        /// <summary>
        /// Mark the cache as stale. Call when any module changes state.
        /// The cache rebuilds lazily on the next property read.
        /// </summary>
        public void Invalidate() => _isDirty = true;

        /// <summary>
        /// Force an immediate recalculate from the owner entity.
        /// Used by MovementSystem on first tick and after invalidation.
        /// </summary>
        public void Recalculate(Entity owner)
        {
            var modules = owner.GetAllComponents()
                               .OfType<ModuleBase>()
                               .Where(m => m.IsOperational)
                               .Cast<IModule>()
                               .ToList();

            RecalculateMovement(modules);
            RecalculatePower(modules);
            RecalculateFuel(modules);
            RecalculateCargo(modules);
            RecalculateCombat(modules);

            // Base mass is the sum of all installed module mass
            // Hull mass would be set separately on entity creation
            TotalModuleMass = modules.OfType<ModuleBase>()
                                     .Sum(m => m.Mass);

            _isDirty = false;
        }

        // ------------------------------------------------------------
        // Movement stats
        // ------------------------------------------------------------

        public float TotalThrust         { get; private set; }
        public float MaxSpeed            { get; private set; }
        public float TurnRate            { get; private set; }
        public float LateralAuthority    { get; private set; }
        public float TotalModuleMass     { get; private set; }

        // Travel capability
        public bool  CanSublight         { get; private set; }
        public bool  CanWarp             { get; private set; }
        public bool  CanJump             { get; private set; }
        public float WarpSpeed           { get; private set; }
        public float WarpChargeTime      { get; private set; }  // slowest warp module
        public float JumpRange           { get; private set; }
        public float JumpChargeTime      { get; private set; }  // slowest jump module
        public float JumpEnergyCost      { get; private set; }  // most expensive jump module
        public float JumpRechargeTime    { get; private set; }  // slowest jump recharge

        // ------------------------------------------------------------
        // Power stats
        // ------------------------------------------------------------

        public float TotalEnergyOutput   { get; private set; }  // from reactors per tick
        public float TotalEnergyDraw     { get; private set; }  // from all active modules
        public float NetEnergyPerTick    { get; private set; }  // output - draw
        public float TotalEnergyCapacity { get; private set; }  // battery banks

        // ------------------------------------------------------------
        // Fuel stats
        // ------------------------------------------------------------

        public float TotalHydrogenCapacity   { get; private set; }
        public float TotalDilithiumCapacity  { get; private set; }
        public float CurrentHydrogen         { get; private set; }
        public float CurrentDilithium        { get; private set; }

        // ------------------------------------------------------------
        // Cargo stats — slot-based
        // ------------------------------------------------------------

        /// <summary>Total stack slots across all cargo modules.</summary>
        public int TotalStackSlots      { get; private set; }

        /// <summary>Slots currently occupied (whole slots, ceil rule).</summary>
        public int UsedStackSlots       { get; private set; }

        /// <summary>Slots available for new cargo.</summary>
        public int FreeStackSlots       => Math.Max(0, TotalStackSlots - UsedStackSlots);

        /// <summary>0.0 empty → 1.0 full.</summary>
        public float CargoFillFraction  =>
            TotalStackSlots > 0
                ? Math.Clamp((float)UsedStackSlots / TotalStackSlots, 0f, 1f)
                : 0f;

        // ------------------------------------------------------------
        // Combat stats
        // ------------------------------------------------------------

        public float TotalWeaponDPS      { get; private set; }
        public float TotalShielding      { get; private set; }
        public float HullIntegrity       { get; private set; } = 1.0f;

        // ------------------------------------------------------------
        // Convenience
        // ------------------------------------------------------------

        /// <summary>
        /// Whether this entity can currently move at all.
        /// </summary>
        public bool CanMove => CanSublight || CanWarp || CanJump;

        /// <summary>
        /// Current fuel level as a 0-1 fraction for HUD display.
        /// Based on hydrogen (primary movement fuel).
        /// </summary>
        public float HydrogenFraction =>
            TotalHydrogenCapacity > 0
                ? Math.Clamp(CurrentHydrogen / TotalHydrogenCapacity, 0f, 1f)
                : 0f;

        public float DilithiumFraction =>
            TotalDilithiumCapacity > 0
                ? Math.Clamp(CurrentDilithium / TotalDilithiumCapacity, 0f, 1f)
                : 0f;

        // ------------------------------------------------------------
        // Tick — checks dirty flag, recalculates if needed
        // Called by MovementSystem before reading values each tick
        // ------------------------------------------------------------

        public override void Tick(Entity owner, double deltaTime)
        {
            if (_isDirty)
                Recalculate(owner);
        }

        // ------------------------------------------------------------
        // Fitting preview helper
        // Returns a new ShipStatsComponent as if a module were added/removed.
        // Does not modify the entity. Used by fitting UI only.
        // ------------------------------------------------------------

        public static ShipStatsComponent Preview(
            Entity owner,
            IModule? moduleToAdd    = null,
            IModule? moduleToRemove = null)
        {
            var preview = new ShipStatsComponent();

            // Build a hypothetical module list
            var modules = owner.GetAllComponents()
                               .OfType<ModuleBase>()
                               .Where(m => m.IsOperational)
                               .Cast<IModule>()
                               .ToList();

            if (moduleToRemove != null)
                modules.Remove(moduleToRemove);

            if (moduleToAdd != null)
                modules.Add(moduleToAdd);

            preview.RecalculateMovement(modules);
            preview.RecalculatePower(modules);
            preview.RecalculateFuel(modules);
            preview.RecalculateCargo(modules);
            preview.RecalculateCombat(modules);
            preview.TotalModuleMass = modules.OfType<ModuleBase>().Sum(m => m.Mass);
            preview._isDirty = false;

            return preview;
        }

        // ------------------------------------------------------------
        // Private recalculate passes
        // ------------------------------------------------------------

        private void RecalculateMovement(List<IModule> modules)
        {
            var engines = modules.OfType<EngineModule>().ToList();

            TotalThrust      = 0f;
            MaxSpeed         = 0f;
            TurnRate         = 0f;
            LateralAuthority = 0f;
            CanSublight      = false;
            CanWarp          = false;
            CanJump          = false;
            WarpSpeed        = 0f;
            WarpChargeTime   = 0f;
            JumpRange        = 0f;
            JumpChargeTime   = 0f;
            JumpEnergyCost   = 0f;
            JumpRechargeTime = 0f;

            foreach (var e in engines)
            {
                switch (e.DriveType)
                {
                    case DriveType.Sublight:
                        CanSublight      =  true;
                        // Store MAX CAPABILITY — throttle is applied by MovementSystem per tick
                        // GetEffectiveThrust() includes CurrentThrottle which defaults to 0
                        TotalThrust     += e.MaxThrust * e.EffectiveFactor;
                        MaxSpeed         =  Math.Max(MaxSpeed, e.MaxSpeed * e.EffectiveFactor);
                        TurnRate        += e.TurnRate * e.EffectiveFactor;
                        LateralAuthority += e.LateralAuthority * e.EffectiveFactor;
                        break;

                    case DriveType.ManeuverOnly:
                        TurnRate         += e.GetEffectiveTurnRate();
                        LateralAuthority += e.LateralAuthority * e.EffectiveFactor;
                        break;

                    case DriveType.Warp:
                        if (e.CanWarp)
                        {
                            CanWarp       = true;
                            WarpSpeed    += e.WarpSpeed * e.EffectiveFactor;
                            // Slowest charge time is the bottleneck
                            WarpChargeTime = WarpChargeTime == 0f
                                ? e.WarpChargeTime
                                : Math.Max(WarpChargeTime, e.WarpChargeTime);
                        }
                        break;

                    case DriveType.Jump:
                        if (e.CanJump)
                        {
                            CanJump          = true;
                            JumpRange        = Math.Max(JumpRange, e.JumpRange);
                            JumpChargeTime   = JumpChargeTime == 0f
                                ? e.JumpChargeTime
                                : Math.Max(JumpChargeTime, e.JumpChargeTime);
                            JumpEnergyCost   = Math.Max(JumpEnergyCost, e.JumpEnergyCost);
                            JumpRechargeTime = Math.Max(JumpRechargeTime, e.JumpRechargeTime);
                        }
                        break;
                }
            }
        }

        private void RecalculatePower(List<IModule> modules)
        {
            TotalEnergyOutput   = modules.OfType<PowerModule>()
                                         .Sum(p => p.GetEffectiveOutput());

            TotalEnergyCapacity = modules.OfType<PowerModule>()
                                         .Sum(p => p.StorageCapacity);

            TotalEnergyDraw     = modules.OfType<EngineModule>()
                                         .Sum(e => e.EnergyDrawPerTick);
            // WeaponModule, SensorModule etc. draw added as written

            NetEnergyPerTick    = TotalEnergyOutput - TotalEnergyDraw;
        }

        private void RecalculateFuel(List<IModule> modules)
        {
            var engines = modules.OfType<EngineModule>().ToList();

            TotalHydrogenCapacity  = engines
                .Where(e => e.UsesFuel && e.FuelType == FuelType.Hydrogen)
                .Sum(e => e.MaxFuel);

            TotalDilithiumCapacity = engines
                .Where(e => e.UsesFuel && e.FuelType == FuelType.Dilithium)
                .Sum(e => e.MaxFuel);

            CurrentHydrogen  = engines
                .Where(e => e.UsesFuel && e.FuelType == FuelType.Hydrogen)
                .Sum(e => e.CurrentFuel);

            CurrentDilithium = engines
                .Where(e => e.UsesFuel && e.FuelType == FuelType.Dilithium)
                .Sum(e => e.CurrentFuel);
        }

        private void RecalculateCargo(List<IModule> modules)
        {
            var cargo = modules.OfType<CargoModule>().ToList();
            TotalStackSlots = cargo.Sum(c => c.StackSlots);
            UsedStackSlots  = cargo.Sum(c => c.UsedSlots);
        }

        private void RecalculateCombat(List<IModule> modules)
        {
            // Populated when WeaponModule and ShieldModule are written
            TotalWeaponDPS = 0f;
            TotalShielding = 0f;
        }
    }
}
