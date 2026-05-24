namespace StarshipSimulation.Shared.Economy
{
    /// <summary>
    /// Central registry of all production recipes.
    /// All quantities are in UNITS. StackSize is a cargo constraint only.
    ///
    /// Bunker capacity reference (InputBunkerSlots = 10 default):
    ///   ironOre   (StackSize 1000) → 10,000 units max
    ///   iron      (StackSize 500)  →  5,000 units max
    ///   ironTube  (StackSize 100)  →  1,000 units max
    ///   missile   (StackSize 100)  →  1,000 units max
    ///   dilithium (StackSize 5)    →     50 units max
    ///   spaceFuel (StackSize 200)  →  2,000 units max
    /// </summary>
    public static class ProductionRecipeRegistry
    {
        private static readonly System.Collections.Generic.Dictionary<string, ProductionRecipe>
            _recipes = new();

        static ProductionRecipeRegistry()
        {
            // ── Mining — pure extractors, no inputs ────────────────

            Register(new ProductionRecipe
            {
                Name                  = "mineIronOre",
                DisplayName           = "Mine Iron Ore",
                Description           = "Extracts raw iron ore. No inputs required.",
                Inputs                = new(),
                Outputs               = new() { ["ironOre"] = 100 },
                ProductionTimeSeconds = 5f
            });

            Register(new ProductionRecipe
            {
                Name                  = "mineCopperOre",
                DisplayName           = "Mine Copper Ore",
                Description           = "Extracts raw copper ore. No inputs required.",
                Inputs                = new(),
                Outputs               = new() { ["copperOre"] = 100 },
                ProductionTimeSeconds = 5f
            });

            Register(new ProductionRecipe
            {
                Name                  = "collectHydrogen",
                DisplayName           = "Collect Hydrogen",
                Description           = "Atmospheric hydrogen collection.",
                Inputs                = new(),
                Outputs               = new() { ["hydrogen"] = 200 },
                ProductionTimeSeconds = 5f
            });

            // ── Smelting ───────────────────────────────────────────

            Register(new ProductionRecipe
            {
                Name                  = "smeltIron",
                DisplayName           = "Smelt Iron",
                Description           = "500 iron ore → 200 iron ingots per cycle.",
                Inputs                = new() { ["ironOre"] = 500 },
                Outputs               = new() { ["iron"]    = 200 },
                ProductionTimeSeconds = 10f
            });

            Register(new ProductionRecipe
            {
                Name                  = "smeltCopper",
                DisplayName           = "Smelt Copper",
                Description           = "500 copper ore → 200 copper ingots per cycle.",
                Inputs                = new() { ["copperOre"] = 500 },
                Outputs               = new() { ["copper"]    = 200 },
                ProductionTimeSeconds = 10f
            });

            // ── Fabrication ────────────────────────────────────────

            Register(new ProductionRecipe
            {
                Name                  = "fabricateIronTubes",
                DisplayName           = "Fabricate Iron Tubes",
                Description           = "1000 iron ingots → 100 iron tubes per cycle.",
                Inputs                = new() { ["iron"]     = 1000 },
                Outputs               = new() { ["ironTube"] = 100  },
                ProductionTimeSeconds = 20f
            });

            // ── Munitions ──────────────────────────────────────────

            // ── Munitions ──────────────────────────────────────────────────

            Register(new ProductionRecipe
            {
                Name                  = "constructWarheads",
                DisplayName           = "Construct Warheads",
                Description           = "200 copper ingots → 10 photonic warheads per cycle.",
                Inputs                = new() { ["copper"] = 200 },
                Outputs               = new() { ["photonicWarhead"] = 10 },
                ProductionTimeSeconds = 20f
            });

            Register(new ProductionRecipe
            {
                Name                  = "constructMissiles",
                DisplayName           = "Construct Missiles",
                Description           = "100 iron tubes + 10 photonic warheads → 100 missiles per cycle.",
                Inputs                = new() { ["ironTube"] = 100, ["photonicWarhead"] = 10 },
                Outputs               = new() { ["missile"]  = 100 },
                ProductionTimeSeconds = 30f
            });

            Register(new ProductionRecipe
            {
                Name                  = "constructNuclearMissiles",
                DisplayName           = "Construct Nuclear Missiles",
                Description           = "500 iron tubes + 1 dilithium → 1 nuclear missile per cycle.",
                Inputs                = new() { ["ironTube"] = 500, ["dilithium"] = 1 },
                Outputs               = new() { ["nuclearMissile"] = 1 },
                ProductionTimeSeconds = 120f
            });

            // ── Strategic weapons ──────────────────────────────────

            Register(new ProductionRecipe
            {
                Name        = "constructStealthTorpedo",
                DisplayName = "Construct Stealth Torpedo",
                Description = "300 iron tubes + 5 navigation systems + 10 photonic warheads " +
                              "+ 1000 space fuel → 1 stealth torpedo per 10s cycle.",
                Inputs = new()
                {
                    ["ironTube"]         = 300,
                    ["navigationSystem"] = 5,
                    ["photonicWarhead"]  = 10,
                    ["spaceFuel"]        = 1000,
                },
                Outputs               = new() { ["stealthTorpedo"] = 1 },
                ProductionTimeSeconds = 10f
            });

            // ── Food ───────────────────────────────────────────────

            Register(new ProductionRecipe
            {
                Name                  = "makeChickenSaladSandwich",
                DisplayName           = "Make Chicken Salad Sandwich",
                Description           = "100 bread + 50 chicken salad → 50 sandwiches per cycle.",
                Inputs                = new() { ["bread"] = 100, ["chickenSalad"] = 50 },
                Outputs               = new() { ["chickenSaladSandwich"] = 50 },
                ProductionTimeSeconds = 10f
            });
        }

        // ------------------------------------------------------------
        // Registration
        // ------------------------------------------------------------

        public static void Register(ProductionRecipe recipe)
        {
            recipe.Validate();
            _recipes[recipe.Name] = recipe;
        }

        // ------------------------------------------------------------
        // Lookup
        // ------------------------------------------------------------

        public static ProductionRecipe Get(string name)
        {
            if (_recipes.TryGetValue(name, out var r)) return r;
            throw new System.Collections.Generic.KeyNotFoundException(
                $"Recipe '{name}' is not registered.");
        }

        public static bool TryGet(string name, out ProductionRecipe? recipe)
            => _recipes.TryGetValue(name, out recipe);

        public static System.Collections.Generic.IReadOnlyDictionary<string, ProductionRecipe>
            All => _recipes;
    }
}
