namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Named production module definitions.
    /// Each represents a physical factory unit with specific capacity and purpose.
    /// </summary>
    public static class ProductionComponentDefinitions
    {
        // ── Mining ─────────────────────────────────────────────────

        public static ProductionComponentConfig IronMiningDrill => new()
        {
            Name             = "iron_mining_drill",
            DisplayName      = "Iron Mining Drill",
            Description      = "Extracts raw iron ore from asteroid or planetary surface. No inputs required.",
            SlotSize         = 4,
            Mass             = 20f,
            InputBunkerSlots = 0,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "mineIronOre"
        };

        public static ProductionComponentConfig CopperMiningDrill => new()
        {
            Name             = "copper_mining_drill",
            DisplayName      = "Copper Mining Drill",
            Description      = "Extracts raw copper ore. No inputs required.",
            SlotSize         = 4,
            Mass             = 20f,
            InputBunkerSlots = 0,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "mineCopperOre"
        };

        // ── Smelting ───────────────────────────────────────────────

        public static ProductionComponentConfig IronSmelter => new()
        {
            Name             = "iron_smelter",
            DisplayName      = "Iron Smelter",
            Description      = "Converts iron ore into iron ingots. Requires iron ore input.",
            SlotSize         = 4,
            Mass             = 25f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "smeltIron"
        };

        public static ProductionComponentConfig CopperSmelter => new()
        {
            Name             = "copper_smelter",
            DisplayName      = "Copper Smelter",
            Description      = "Converts copper ore into copper ingots.",
            SlotSize         = 4,
            Mass             = 25f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "smeltCopper"
        };

        // ── Fabrication ────────────────────────────────────────────

        public static ProductionComponentConfig IronTubeFabricator => new()
        {
            Name             = "iron_tube_fabricator",
            DisplayName      = "Iron Tube Fabricator",
            Description      = "Manufactures iron tubes from ingots. High input demand.",
            SlotSize         = 4,
            Mass             = 18f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "fabricateIronTubes"
        };

        // ── Munitions ──────────────────────────────────────────────

        public static ProductionComponentConfig WarheadFactory => new()
        {
            Name              = "warhead_factory",
            DisplayName       = "Warhead Factory",
            Description       = "Manufactures photonic warheads from copper ingots.",
            SlotSize          = 4,
            Mass              = 22f,
            InputBunkerSlots  = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe     = "constructWarheads"
        };

        public static ProductionComponentConfig MissileConstructor => new()
        {
            Name             = "missile_constructor",
            DisplayName      = "Missile Constructor",
            Description      = "Assembles standard missiles from iron tubes.",
            SlotSize         = 6,
            Mass             = 22f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "constructMissiles"
        };

        public static ProductionComponentConfig NuclearMissileConstructor => new()
        {
            Name             = "nuclear_missile_constructor",
            DisplayName      = "Nuclear Missile Constructor",
            Description      = "Highly classified. Produces nuclear warheads from tubes and dilithium. " +
                               "Output rate is low. Input requirements are significant.",
            SlotSize         = 9,
            Mass             = 40f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe    = "constructNuclearMissiles"
        };

        // ── Generic (configurable at runtime) ─────────────────────

        /// <summary>
        /// General-purpose production module with no default recipe.
        /// Recipe set at runtime via SetRecipe(). Useful for player-built facilities.
        /// </summary>
        public static ProductionComponentConfig GeneralProductionModule => new()
        {
            Name             = "general_production_module",
            DisplayName      = "General Production Module",
            Description      = "A configurable production unit. Assign a recipe to begin operation.",
            SlotSize         = 4,
            Mass             = 15f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 10,
            DefaultRecipe    = ""
        };

        // ── Strategic Weapons ──────────────────────────────────────

        /// <summary>
        /// Stealth torpedo assembly facility.
        /// Four-input recipe: iron tubes, navigation systems,
        /// photonic warheads, space fuel.
        /// Produces one stealth torpedo per 10-second cycle.
        /// </summary>
        public static ProductionComponentConfig StealthTorpedoConstructor => new()
        {
            Name             = "stealth_torpedo_constructor",
            DisplayName      = "Stealth Torpedo Constructor",
            Description      = "A secure, shielded assembly bay for stealth torpedo construction. " +
                               "Requires four distinct input streams simultaneously. " +
                               "Output is a strategic asset.",
            SlotSize         = 9,
            Mass             = 50f,
            InputBunkerSlots = 10,
            OutputBunkerSlots = 5,
            DefaultRecipe    = "constructStealthTorpedo"
        };
    }
}
