using System.Collections.Generic;
using System.Linq;

namespace StarshipSimulation.Shared.Economy
{
    /// <summary>
    /// Defines a production recipe — inputs consumed and outputs produced
    /// per production cycle.
    ///
    /// ALL QUANTITIES ARE IN UNITS — not stacks.
    ///   "requires 300 iron tubes" means exactly 300 units consumed per cycle.
    ///   "produces 100 missiles" means exactly 100 units added to output bunker.
    ///
    /// StackSize on ResourceDefinition is a CARGO concept only — it determines
    /// how many units fit in one cargo slot. It has no role in recipe math.
    ///
    /// Empty Inputs = pure extractor (mining, solar collection, hydrogen scoops).
    /// </summary>
    public class ProductionRecipe
    {
        public string Name        { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";

        /// <summary>
        /// Resources consumed per cycle, in units.
        /// Empty = pure extractor.
        /// </summary>
        public Dictionary<string, int> Inputs { get; init; } = new();

        /// <summary>
        /// Resources produced per cycle, in units.
        /// </summary>
        public Dictionary<string, int> Outputs { get; init; } = new();

        /// <summary>Real-time seconds per production cycle.</summary>
        public float ProductionTimeSeconds { get; init; } = 5f;

        // ------------------------------------------------------------
        // Derived
        // ------------------------------------------------------------

        public bool IsExtractor => Inputs.Count == 0;

        public IEnumerable<string> AllResources =>
            Inputs.Keys.Concat(Outputs.Keys).Distinct();

        // ------------------------------------------------------------
        // Validation — called at startup by ProductionRecipeRegistry
        // ------------------------------------------------------------

        public void Validate()
        {
            foreach (var resource in AllResources)
            {
                if (!ResourceRegistry.Exists(resource))
                    throw new System.InvalidOperationException(
                        $"Recipe '{Name}' references unknown resource '{resource}'. " +
                        $"Register it in ResourceRegistry first.");
            }
        }
    }
}
