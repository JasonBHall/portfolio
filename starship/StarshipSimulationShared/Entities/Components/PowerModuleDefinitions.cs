namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Library of named power module definitions.
    /// Covers all power generation and storage types:
    ///   Fusion reactors, fission reactors, solar arrays, battery banks.
    ///
    /// Balance values are placeholder — tune during playtesting.
    /// </summary>
    public static class PowerModuleDefinitions
    {
        // ============================================================
        // FUSION REACTORS — hydrogen fuel, reliable, scalable
        // ============================================================

        /// <summary>
        /// The 1-point reactor. Cheap, tiny, almost no output.
        /// The valid-but-slow build from Core Truths — pair with a
        /// jump drive and you'll get there eventually.
        /// </summary>
        public static PowerModuleConfig MiniFusionPlant => new()
        {
            Name             = "mini_fusion_plant",
            DisplayName      = "Mini Fusion Plant",
            Description      = "Barely a flicker. Cheap, light, and reliable. " +
                               "Pair it with a jump drive and you'll get there eventually. " +
                               "Nobody said how long.",
            SlotSize         = 1,
            Mass             = 3f,
            OutputPerTick    = 1f,
            StorageCapacity  = 20f,
            UsesFuel         = true,
            FuelType         = FuelType.Hydrogen,
            MaxFuel          = 50f,
            FuelCostPerTick  = 0.01f
        };

        /// <summary>
        /// Standard civilian fusion reactor.
        /// The workhorse for freighters and mid-size vessels.
        /// </summary>
        public static PowerModuleConfig CivilianFusionCore => new()
        {
            Name             = "civilian_fusion_core",
            DisplayName      = "Civilian Fusion Core",
            Description      = "Dependable. Not exciting. Powers your engines, " +
                               "runs your sensors, won't blow up. Usually.",
            SlotSize         = 2,
            Mass             = 10f,
            OutputPerTick    = 8f,
            StorageCapacity  = 80f,
            UsesFuel         = true,
            FuelType         = FuelType.Hydrogen,
            MaxFuel          = 200f,
            FuelCostPerTick  = 0.05f
        };

        /// <summary>
        /// Military-grade fusion reactor.
        /// Needed to run weapons, shields, and jump drive simultaneously.
        /// </summary>
        public static PowerModuleConfig MilitaryFusionCore => new()
        {
            Name             = "military_fusion_core",
            DisplayName      = "Military Fusion Core",
            Description      = "Hot, loud, and powerful. Runs your weapons, " +
                               "shields, and jump drive — simultaneously if you're brave. " +
                               "Drinks hydrogen like a dreadnought.",
            SlotSize         = 4,
            Mass             = 22f,
            OutputPerTick    = 25f,
            StorageCapacity  = 200f,
            UsesFuel         = true,
            FuelType         = FuelType.Hydrogen,
            MaxFuel          = 500f,
            FuelCostPerTick  = 0.2f
        };

        /// <summary>
        /// Capital-grade fusion core. Massive output for massive ships.
        /// </summary>
        public static PowerModuleConfig CapitalFusionCore => new()
        {
            Name             = "capital_fusion_core",
            DisplayName      = "Capital Fusion Core",
            Description      = "Built for ships measured in kilometres. " +
                               "Output that makes military reactors look like flashlights. " +
                               "Requires a dedicated engineering crew to maintain.",
            SlotSize         = 9,
            Mass             = 80f,
            OutputPerTick    = 80f,
            StorageCapacity  = 800f,
            UsesFuel         = true,
            FuelType         = FuelType.Hydrogen,
            MaxFuel          = 2000f,
            FuelCostPerTick  = 0.8f
        };

        // ============================================================
        // FISSION REACTORS — fissile material, very high output, waste
        // ============================================================

        /// <summary>
        /// Fission reactor. Very high output, produces waste byproduct.
        /// Waste disposal is a logistics concern — adds to the supply chain.
        /// </summary>
        public static PowerModuleConfig FissionReactor => new()
        {
            Name             = "fission_reactor",
            DisplayName      = "Fission Reactor",
            Description      = "More power than you probably need. " +
                               "The waste handling alone is a full-time job. " +
                               "Worth it for capital ships that need everything running at once.",
            SlotSize         = 4,
            Mass             = 30f,
            OutputPerTick    = 40f,
            StorageCapacity  = 300f,
            UsesFuel         = true,
            FuelType         = FuelType.FissileMaterial,
            MaxFuel          = 100f,
            FuelCostPerTick  = 0.02f
        };

        // ============================================================
        // SOLAR ARRAYS — zero fuel, slow output, ideal for stations
        // ============================================================

        /// <summary>
        /// Small solar array. Free energy, very low output.
        /// Not useful for ships. Ideal for deep-space relay stations
        /// stockpiling charge over days for jump ship recharging.
        /// This is the pony express station power source from Core Truths.
        /// </summary>
        public static PowerModuleConfig SmallSolarArray => new()
        {
            Name             = "small_solar_array",
            DisplayName      = "Small Solar Array",
            Description      = "Free energy. Slow energy. Leave it running for a week " +
                               "and you'll have enough to recharge a frigate's jump drive. " +
                               "Perfect for deep-space relay stations nobody visits often.",
            SlotSize         = 2,
            Mass             = 4f,
            OutputPerTick    = 0.5f,
            StorageCapacity  = 500f,   // large buffer — stocks up over time
            UsesFuel         = false,
            FuelType         = FuelType.None,
            MaxFuel          = 0f,
            FuelCostPerTick  = 0f
        };

        /// <summary>
        /// Large station solar array. Higher capacity, still zero fuel.
        /// Multiple units make a station into a serious energy bank.
        /// </summary>
        public static PowerModuleConfig LargeSolarArray => new()
        {
            Name             = "large_solar_array",
            DisplayName      = "Large Solar Array",
            Description      = "A wall of panels. Charges slowly, stores massively. " +
                               "A forward operating base running three of these can " +
                               "refuel a jump fleet — given enough time.",
            SlotSize         = 6,
            Mass             = 12f,
            OutputPerTick    = 2f,
            StorageCapacity  = 5000f,
            UsesFuel         = false,
            FuelType         = FuelType.None,
            MaxFuel          = 0f,
            FuelCostPerTick  = 0f
        };

        // ============================================================
        // BATTERY BANKS — storage only, no generation
        // ============================================================

        /// <summary>
        /// Standard battery bank. No generation — pure energy storage.
        /// Buffers surplus from reactors and releases during peak demand.
        /// A jump ship arriving at a solar station draws from the battery,
        /// not the solar array directly.
        /// </summary>
        public static PowerModuleConfig StandardBatteryBank => new()
        {
            Name             = "standard_battery_bank",
            DisplayName      = "Standard Battery Bank",
            Description      = "Stores what your reactor produces. Releases when you need it. " +
                               "Essential for ships with spiky power demands — " +
                               "weapons, jump drives, shield bursts.",
            SlotSize         = 2,
            Mass             = 8f,
            OutputPerTick    = 0f,      // no generation
            StorageCapacity  = 1000f,   // pure storage
            UsesFuel         = false,
            FuelType         = FuelType.None,
            MaxFuel          = 0f,
            FuelCostPerTick  = 0f
        };

        /// <summary>
        /// Heavy battery bank. Enormous storage for stations and capital ships.
        /// The backbone of a jump recharge station.
        /// </summary>
        public static PowerModuleConfig HeavyBatteryBank => new()
        {
            Name             = "heavy_battery_bank",
            DisplayName      = "Heavy Battery Bank",
            Description      = "An enormous energy reservoir. Heavy, expensive, indispensable. " +
                               "A station with one of these and a solar array becomes a " +
                               "legitimate deep-space charging point for jump-capable vessels.",
            SlotSize         = 6,
            Mass             = 35f,
            OutputPerTick    = 0f,
            StorageCapacity  = 20000f,
            UsesFuel         = false,
            FuelType         = FuelType.None,
            MaxFuel          = 0f,
            FuelCostPerTick  = 0f
        };
    }
}
