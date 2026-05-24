namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Named cargo module definitions.
    /// Capacity is expressed in stack slots — one slot holds one stack of any resource.
    /// Generic modules accept anything. Specialised modules have higher slot counts
    /// but only accept their designated resource type.
    /// </summary>
    public static class CargoModuleDefinitions
    {
        // ── Generic cargo ──────────────────────────────────────────

        public static CargoModuleConfig SmallCargoPod => new()
        {
            Name         = "small_cargo_pod",
            DisplayName  = "Small Cargo Pod",
            Description  = "A compact general-purpose container. Good for scouts and light traders.",
            SlotSize     = 2,
            Mass         = 1f,
            StackSlots   = 10,
            ResourceType = "generic"
        };

        public static CargoModuleConfig StandardCargoContainer => new()
        {
            Name         = "standard_cargo_container",
            DisplayName  = "Standard Cargo Container",
            Description  = "The universal unit of freight. Fits almost anywhere.",
            SlotSize     = 4,
            Mass         = 2f,
            StackSlots   = 50,
            ResourceType = "generic"
        };

        public static CargoModuleConfig BulkCargoHold => new()
        {
            Name         = "bulk_cargo_hold",
            DisplayName  = "Bulk Cargo Hold",
            Description  = "A large pressurised hold for serious hauling. Eats hardpoint space.",
            SlotSize     = 9,
            Mass         = 5f,
            StackSlots   = 200,
            ResourceType = "generic"
        };

        // ── Specialised cargo ──────────────────────────────────────

        /// <summary>
        /// Higher slot count than generic for raw ore.
        /// Only accepts resources with category "ore".
        /// </summary>
        public static CargoModuleConfig OreHopper => new()
        {
            Name         = "ore_hopper",
            DisplayName  = "Ore Hopper",
            Description  = "Reinforced bins designed for dense raw ore. Carries more ore than an equivalent generic container.",
            SlotSize     = 4,
            Mass         = 3f,
            StackSlots   = 80,
            ResourceType = "ore"
        };

        /// <summary>
        /// Shielded, temperature-controlled dilithium storage.
        /// </summary>
        public static CargoModuleConfig DilithiumVault => new()
        {
            Name         = "dilithium_vault",
            DisplayName  = "Dilithium Crystal Vault",
            Description  = "Magnetically shielded crystal storage. Expensive. Do not let it take a direct hit.",
            SlotSize     = 2,
            Mass         = 4f,
            StackSlots   = 20,
            ResourceType = "dilithium"
        };

        /// <summary>
        /// Armoured magazine with auto-feed to weapon hardpoints.
        /// Required for ships carrying missile or projectile weapons.
        /// </summary>
        public static CargoModuleConfig AmmunitionMagazine => new()
        {
            Name         = "ammunition_magazine",
            DisplayName  = "Ammunition Magazine",
            Description  = "Armoured magazine with auto-feed systems. When it is empty, your weapons go silent.",
            SlotSize     = 2,
            Mass         = 4f,
            StackSlots   = 40,
            ResourceType = "ammunition"
        };

        /// <summary>
        /// Liquid hydrogen extended fuel storage.
        /// Supplements engine fuel reserves for long-range operations.
        /// </summary>
        public static CargoModuleConfig HydrogenFuelTank => new()
        {
            Name         = "hydrogen_fuel_tank",
            DisplayName  = "Hydrogen Fuel Tank",
            Description  = "Extended hydrogen storage. A tender ship with three of these can refuel a small fleet.",
            SlotSize     = 4,
            Mass         = 2f,
            StackSlots   = 60,
            ResourceType = "fuel"
        };
    }
}
