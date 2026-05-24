namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// A physical power generation module. Produces energy per tick.
    ///
    /// PowerModule is the parent concept for all energy sources:
    ///   - Fusion reactors (hydrogen fuel, high output)
    ///   - Fission reactors (fissile material, very high output)
    ///   - Solar arrays (no fuel, low output, ideal for stations)
    ///   - Battery banks (no generation, pure storage — releases on demand)
    ///
    /// Multiple power modules stack additively — total output is the sum
    /// of all operational power modules. This is correct: two reactors
    /// genuinely produce twice the power.
    ///
    /// Everything that draws energy depends on power modules being installed.
    /// Without any power modules, energy-dependent systems do not operate:
    ///   - Jump drive cannot charge
    ///   - Weapons cannot fire
    ///   - Shields cannot hold
    ///   - Sensors run at minimum capacity
    ///
    /// Engines have their own fuel (hydrogen/dilithium) and can run without
    /// a reactor — but their energy draw reduces what's available to other systems.
    ///
    /// See Core Truths — Energy and Fuel, Commitment and Consequence.
    /// </summary>
    public class PowerModule : ModuleBase
    {
        // ------------------------------------------------------------
        // IModule — identity
        // ------------------------------------------------------------

        public override string Name        { get; }
        public override string DisplayName { get; }
        public override string Description { get; }
        public override string SlotType    => "power";
        public override int    SlotSize    { get; }
        public override float  Mass        { get; }

        // ------------------------------------------------------------
        // Power generation
        // ------------------------------------------------------------

        /// <summary>
        /// Energy units produced per tick at full health and online.
        /// Reduced proportionally by Condition (damaged module = less output).
        /// Battery banks set this to 0 — they store, not generate.
        /// </summary>
        public float OutputPerTick { get; }

        /// <summary>
        /// Maximum energy this module can store internally.
        /// Reactors: small buffer to smooth spiky demand.
        /// Battery banks: large capacity — this is their primary purpose.
        /// Solar arrays: large buffer — stocks up over time for burst release.
        /// </summary>
        public float StorageCapacity { get; }

        /// <summary>
        /// Current stored energy. Filled by generation, drained by ship systems.
        /// </summary>
        public float StoredEnergy { get; set; }

        // ------------------------------------------------------------
        // Fuel (optional)
        // Solar arrays and batteries set UsesFuel = false.
        // ------------------------------------------------------------

        public bool     UsesFuel        { get; }
        public FuelType FuelType        { get; }
        public float    MaxFuel         { get; }
        public float    FuelCostPerTick { get; }  // consumed per tick of operation
        public float    RechargeRate    { get; }  // passive fuel refill per tick
        public float    CurrentFuel     { get; set; }

        // ------------------------------------------------------------
        // Effective output
        // ------------------------------------------------------------

        /// <summary>
        /// Actual energy output this tick, accounting for condition,
        /// online state, and fuel availability.
        /// </summary>
        public float GetEffectiveOutput()
        {
            if (!IsOperational) return 0f;
            if (UsesFuel && CurrentFuel <= 0f) return 0f;
            return OutputPerTick * EffectiveFactor;
        }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public PowerModule(PowerModuleConfig config)
        {
            Name             = config.Name;
            DisplayName      = config.DisplayName;
            Description      = config.Description;
            SlotSize         = config.SlotSize;
            Mass             = config.Mass;

            OutputPerTick    = config.OutputPerTick;
            StorageCapacity  = config.StorageCapacity;
            StoredEnergy     = config.StorageCapacity; // start full

            UsesFuel         = config.UsesFuel;
            FuelType         = config.FuelType;
            MaxFuel          = config.MaxFuel;
            FuelCostPerTick  = config.FuelCostPerTick;
            RechargeRate     = config.RechargeRate;
            CurrentFuel      = config.MaxFuel;         // start full
        }

        // ------------------------------------------------------------
        // Tick — fuel consumption
        // Energy output is read by PowerSystem when written.
        // ShipStatsComponent reads GetEffectiveOutput() directly.
        // ------------------------------------------------------------

        public override void Tick(Entity owner, double deltaTime)
        {
            if (!IsOperational) return;

            float dt = (float)deltaTime;

            // Consume fuel per tick
            if (UsesFuel && FuelCostPerTick > 0f)
            {
                float cost = FuelCostPerTick * dt;

                if (CurrentFuel >= cost)
                {
                    CurrentFuel -= cost;
                }
                else
                {
                    // Fuel exhausted — module produces zero output
                    // Does not auto-shutdown — player restarts manually
                    CurrentFuel = 0f;
                }
            }

            // Passive fuel recharge (solar topping up a buffer etc.)
            if (UsesFuel && RechargeRate > 0f && CurrentFuel < MaxFuel)
            {
                CurrentFuel = System.Math.Min(
                    MaxFuel,
                    CurrentFuel + RechargeRate * dt
                );
            }

            // Invalidate ship stats when fuel state changes materially
            owner.GetComponent<ShipStatsComponent>()?.Invalidate();
        }
    }

    // ------------------------------------------------------------
    // Config record
    // ------------------------------------------------------------

    /// <summary>
    /// Defines the stats for a specific power module type.
    /// Create one per module definition in PowerModuleDefinitions.
    /// </summary>
    public record PowerModuleConfig
    {
        // Identity
        public string Name        { get; init; } = "power_module";
        public string DisplayName { get; init; } = "Power Module";
        public string Description { get; init; } = string.Empty;
        public int    SlotSize    { get; init; } = 2;
        public float  Mass        { get; init; } = 8f;

        // Power
        public float OutputPerTick   { get; init; } = 5f;
        public float StorageCapacity { get; init; } = 50f;

        // Fuel
        public bool     UsesFuel        { get; init; } = false;
        public FuelType FuelType        { get; init; } = FuelType.None;
        public float    MaxFuel         { get; init; } = 0f;
        public float    FuelCostPerTick { get; init; } = 0f;
        public float    RechargeRate    { get; init; } = 0f;
    }
}
