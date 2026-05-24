using System;
using System.Collections.Generic;
using System.Linq;

namespace StarshipSimulation.Shared.Economy
{
    /// <summary>
    /// Central registry of all resource types in the simulation.
    ///
    /// Resources are registered at startup. Recipes reference resources
    /// by name — the registry validates that referenced resources exist.
    ///
    /// Add new resources here. No other code changes needed to introduce
    /// a new commodity to the economy.
    /// </summary>
    public static class ResourceRegistry
    {
        private static readonly Dictionary<string, ResourceDefinition> _resources = new();

        // ------------------------------------------------------------
        // Static constructor — registers all known resources
        // ------------------------------------------------------------

        static ResourceRegistry()
        {
            // ── Raw Materials ──────────────────────────────────────
            Register(new ResourceDefinition
            {
                Name        = "ironOre",
                DisplayName = "Iron Ore",
                StackSize   = 1000,
                MassPerStack = 5f,
                Category    = "ore"
            });

            Register(new ResourceDefinition
            {
                Name        = "copperOre",
                DisplayName = "Copper Ore",
                StackSize   = 1000,
                MassPerStack = 5f,
                Category    = "ore"
            });

            Register(new ResourceDefinition
            {
                Name        = "hydrogen",
                DisplayName = "Hydrogen",
                StackSize   = 500,
                MassPerStack = 1f,
                Category    = "fuel"
            });

            Register(new ResourceDefinition
            {
                Name                     = "dilithium",
                DisplayName              = "Dilithium Crystal",
                StackSize                = 5,
                MassPerStack             = 2f,
                Category                 = "precious",
                RequiresSpecializedStorage = true
            });

            // ── Processed Materials ────────────────────────────────
            Register(new ResourceDefinition
            {
                Name        = "iron",
                DisplayName = "Iron Ingot",
                StackSize   = 500,
                MassPerStack = 4f,
                Category    = "ingot"
            });

            Register(new ResourceDefinition
            {
                Name        = "copper",
                DisplayName = "Copper Ingot",
                StackSize   = 500,
                MassPerStack = 4f,
                Category    = "ingot"
            });

            // ── Manufactured Components ────────────────────────────
            Register(new ResourceDefinition
            {
                Name        = "ironTube",
                DisplayName = "Iron Tube",
                StackSize   = 100,
                MassPerStack = 2f,
                Category    = "component"
            });

            // ── Munitions ──────────────────────────────────────────
            Register(new ResourceDefinition
            {
                Name        = "missile",
                DisplayName = "Standard Missile",
                StackSize   = 100,
                MassPerStack = 3f,
                Category    = "munition"
            });

            Register(new ResourceDefinition
            {
                Name        = "nuclearMissile",
                DisplayName = "Nuclear Missile",
                StackSize   = 10,
                MassPerStack = 8f,
                Category    = "munition"
            });

            // ── Food / Consumables (example) ───────────────────────
            Register(new ResourceDefinition
            {
                Name        = "bread",
                DisplayName = "Bread",
                StackSize   = 50,
                MassPerStack = 0.5f,
                Category    = "food"
            });

            Register(new ResourceDefinition
            {
                Name        = "chickenSalad",
                DisplayName = "Chicken Salad",
                StackSize   = 50,
                MassPerStack = 0.5f,
                Category    = "food"
            });

            Register(new ResourceDefinition
            {
                Name        = "chickenSaladSandwich",
                DisplayName = "Chicken Salad Sandwich",
                StackSize   = 50,
                MassPerStack = 0.8f,
                Category    = "food"
            });

            // ── Advanced Components ────────────────────────────────
            Register(new ResourceDefinition
            {
                Name        = "navigationSystem",
                DisplayName = "Navigation System",
                StackSize   = 5,
                MassPerStack = 3f,
                Category    = "component"
            });

            Register(new ResourceDefinition
            {
                Name        = "photonicWarhead",
                DisplayName = "Photonic Warhead",
                StackSize   = 10,
                MassPerStack = 8f,
                Category    = "munition"
            });

            Register(new ResourceDefinition
            {
                Name        = "spaceFuel",
                DisplayName = "Space Fuel",
                StackSize   = 200,
                MassPerStack = 1.5f,
                Category    = "fuel"
            });

            // ── Strategic Weapons ──────────────────────────────────
            Register(new ResourceDefinition
            {
                Name        = "stealthTorpedo",
                DisplayName = "Stealth Torpedo",
                StackSize   = 5,
                MassPerStack = 20f,
                Category    = "munition"
            });
        }

        // ------------------------------------------------------------
        // Registration
        // ------------------------------------------------------------

        public static void Register(ResourceDefinition resource)
        {
            if (string.IsNullOrWhiteSpace(resource.Name))
                throw new ArgumentException("Resource name cannot be empty.");

            if (_resources.ContainsKey(resource.Name))
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' is already registered.");

            _resources[resource.Name] = resource;
        }

        // ------------------------------------------------------------
        // Lookup
        // ------------------------------------------------------------

        public static ResourceDefinition Get(string name)
        {
            if (_resources.TryGetValue(name, out var def))
                return def;

            throw new KeyNotFoundException(
                $"Resource '{name}' is not registered. " +
                $"Add it to ResourceRegistry before referencing it in a recipe.");
        }

        public static bool TryGet(string name, out ResourceDefinition? def)
            => _resources.TryGetValue(name, out def);

        public static bool Exists(string name)
            => _resources.ContainsKey(name);

        public static IReadOnlyDictionary<string, ResourceDefinition> All
            => _resources;

        // ------------------------------------------------------------
        // Conversion helpers — delegate to resource definition
        // ------------------------------------------------------------

        public static float UnitsToStacks(string resourceName, int units)
            => Get(resourceName).UnitsToStacks(units);

        public static int StacksToUnits(string resourceName, float stacks)
            => Get(resourceName).StacksToUnits(stacks);
    }
}
