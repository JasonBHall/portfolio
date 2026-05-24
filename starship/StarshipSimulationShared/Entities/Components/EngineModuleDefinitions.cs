namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Library of named engine module definitions.
    /// Each is a pre-configured EngineModuleConfig that can be instantiated
    /// into an EngineModule and attached to any entity.
    ///
    /// Add new engine types here. The UI reads these for the spawn/fitting panel.
    /// Balance values are placeholders — tune during playtesting.
    ///
    /// Naming convention: evocative in-universe names, not technical specs.
    /// </summary>
    public static class EngineModuleDefinitions
    {
        // ------------------------------------------------------------
        // SUBLIGHT DRIVES
        // ------------------------------------------------------------

        /// <summary>
        /// Light freighter / scout drive. Fast to accelerate, low fuel use.
        /// Good agility. The workhorse of small independent operators.
        /// </summary>
        public static EngineModuleConfig GeckoSublightDrive => new()
        {
            Name        = "gecko_sublight",
            DisplayName = "Gecko Sublight Drive",
            Description = "A nimble, fuel-efficient drive favoured by scouts and light traders. Won't win a race but won't leave you stranded either.",
            SlotSize    = 2,
            Mass        = 5f,
            DriveType   = DriveType.Sublight,

            MaxThrust        = 80f,
            MaxSpeed         = 60f,
            TurnRate         = 1.8f,
            LateralAuthority = 0.6f,

            EnergyDrawPerTick = 0.5f,
            UsesFuel          = true,
            FuelType          = FuelType.Hydrogen,
            MaxFuel           = 80f,
            RechargeRate      = 0f
        };

        /// <summary>
        /// Heavy freighter drive. High thrust for moving mass. Slow to turn.
        /// The kind of engine that drags ore barges across the system.
        /// </summary>
        public static EngineModuleConfig DothrakiHorseDrive => new()
        {
            Name        = "dothraki_horse_drive",
            DisplayName = "Dothraki Horse Drive",
            Description = "Brute force. No finesse. Legendary among hauler captains for reliability and raw push. Turns like a barge, accelerates like a stampede.",
            SlotSize    = 4,
            Mass        = 18f,
            DriveType   = DriveType.Sublight,

            MaxThrust        = 280f,
            MaxSpeed         = 45f,
            TurnRate         = 0.4f,
            LateralAuthority = 0.1f,

            EnergyDrawPerTick = 1.8f,
            UsesFuel          = true,
            FuelType          = FuelType.Hydrogen,
            MaxFuel           = 200f,
            RechargeRate      = 0f
        };

        /// <summary>
        /// Capital ship drive. Enormous thrust for moving enormous mass.
        /// Barely rotates. Maneuver thrusters sold separately.
        /// </summary>
        public static EngineModuleConfig TitanSpineDrive => new()
        {
            Name        = "titan_spine_drive",
            DisplayName = "Titan Spine Drive",
            Description = "You don't steer it. You point it and commit. Capital class vessels only.",
            SlotSize    = 9,
            Mass        = 80f,
            DriveType   = DriveType.Sublight,

            MaxThrust        = 1200f,
            MaxSpeed         = 30f,
            TurnRate         = 0.05f,
            LateralAuthority = 0.02f,

            EnergyDrawPerTick = 6f,
            UsesFuel          = true,
            FuelType          = FuelType.Hydrogen,
            MaxFuel           = 800f,
            RechargeRate      = 0f
        };

        // ------------------------------------------------------------
        // MANEUVER THRUSTERS
        // ------------------------------------------------------------

        /// <summary>
        /// High-authority maneuver thruster pack. No forward thrust.
        /// Fighters and interceptors use these for agility.
        /// </summary>
        public static EngineModuleConfig HummingbirdRcsPack => new()
        {
            Name        = "hummingbird_rcs",
            DisplayName = "Hummingbird Maneuver Pack",
            Description = "Pure agility. No forward drive. Combined with a sublight engine, transforms any ship into something that corners like a thought.",
            SlotSize    = 1,
            Mass        = 2f,
            DriveType   = DriveType.ManeuverOnly,

            TurnRate         = 3.5f,
            LateralAuthority = 1.2f,

            EnergyDrawPerTick = 0.3f,
            UsesFuel          = true,
            FuelType          = FuelType.Hydrogen,
            MaxFuel           = 40f,
            RechargeRate      = 0f
        };

        /// <summary>
        /// Compact warp pod for small courier vessels.
        /// Lower speed than Helios but fits in a 2-slot hardpoint.
        /// </summary>
        public static EngineModuleConfig MicroWarpPod => new()
        {
            Name        = "micro_warp_pod",
            DisplayName = "Micro Warp Pod",
            Description = "Small, light, surprisingly capable. Single-stack couriers and scout ships use this to skip across the system faster than anything their size has a right to.",
            SlotSize    = 2,
            Mass        = 8f,
            DriveType   = DriveType.Warp,

            WarpSpeed      = 350f,
            WarpChargeTime = 3f,

            EnergyDrawPerTick = 1.2f,
            UsesFuel          = true,
            FuelType          = FuelType.Dilithium,
            MaxFuel           = 50f,
            RechargeRate      = 0.025f
        };

        // ------------------------------------------------------------
        // WARP DRIVES
        // ------------------------------------------------------------

        /// <summary>
        /// Standard civilian warp drive. Reliable, economical.
        /// The backbone of inter-system trade.
        /// </summary>
        public static EngineModuleConfig HeliosWarpSled => new()
        {
            Name        = "helios_warp_sled",
            DisplayName = "Helios Warp Sled",
            Description = "Nothing exciting. Gets you there at warp speed, doesn't drink too much dilithium, doesn't complain. Industry standard for a reason.",
            SlotSize    = 4,
            Mass        = 15f,
            DriveType   = DriveType.Warp,

            WarpSpeed      = 500f,
            WarpChargeTime = 4f,

            EnergyDrawPerTick = 2f,
            UsesFuel          = true,
            FuelType          = FuelType.Dilithium,
            MaxFuel           = 100f,
            RechargeRate      = 0.02f  // dilithium crystal regeneration
        };

        /// <summary>
        /// Military-grade warp drive. Faster charge, higher speed.
        /// Burns dilithium at a premium rate.
        /// </summary>
        public static EngineModuleConfig StrikeWarpCore => new()
        {
            Name        = "strike_warp_core",
            DisplayName = "Strike Warp Core",
            Description = "Military spec. Fast to spool, fast in transit. Your dilithium budget will suffer. Your enemies won't be able to catch you.",
            SlotSize    = 4,
            Mass        = 20f,
            DriveType   = DriveType.Warp,

            WarpSpeed      = 900f,
            WarpChargeTime = 2f,

            EnergyDrawPerTick = 4f,
            UsesFuel          = true,
            FuelType          = FuelType.Dilithium,
            MaxFuel           = 80f,
            RechargeRate      = 0.015f
        };

        // ------------------------------------------------------------
        // JUMP DRIVES
        // ------------------------------------------------------------

        /// <summary>
        /// Long-range jump drive. Extreme energy cost. Long recharge.
        /// The only way to cross the deep between systems quickly.
        /// A ship with a 1-point reactor can still use this — it just
        /// takes a very long time to charge. That is a valid build.
        /// </summary>
        public static EngineModuleConfig QuantumJumpCore => new()
        {
            Name        = "quantum_jump_core",
            DisplayName = "Quantum Jump Core",
            Description = "Rip a hole in space. Arrive somewhere else. The energy bill will ruin you. The recharge window will terrify you. Worth it.",
            SlotSize    = 6,
            Mass        = 35f,
            DriveType   = DriveType.Jump,

            JumpRange        = 100000f,
            JumpChargeTime   = 15f,
            JumpEnergyCost   = 10000f,
            JumpRechargeTime = 120f,   // 2 minutes before it can fire again

            EnergyDrawPerTick = 0.5f,  // idle draw while installed
            UsesFuel          = false,
            FuelType          = FuelType.None,
            MaxFuel           = 0f,
            RechargeRate      = 0f
        };

        /// <summary>
        /// Short-range jump drive. Less energy. Shorter recharge.
        /// Tactical repositioning within a system. Limited range.
        /// </summary>
        public static EngineModuleConfig SpringJumpUnit => new()
        {
            Name        = "spring_jump_unit",
            DisplayName = "Spring Jump Unit",
            Description = "Short legs, quick recovery. Won't get you across the sector but will get you out of a bad spot. Popular with raiders and fast attack vessels.",
            SlotSize    = 4,
            Mass        = 22f,
            DriveType   = DriveType.Jump,

            JumpRange        = 20000f,
            JumpChargeTime   = 8f,
            JumpEnergyCost   = 2500f,
            JumpRechargeTime = 45f,

            EnergyDrawPerTick = 0.3f,
            UsesFuel          = false,
            FuelType          = FuelType.None,
            MaxFuel           = 0f,
            RechargeRate      = 0f
        };
    }
}
