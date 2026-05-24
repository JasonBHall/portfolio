namespace StarshipSimulation.Shared.Economy
{
    /// <summary>
    /// Defines a resource type in the simulation economy.
    ///
    /// Resources are open-ended — any name is valid. New resource types
    /// are created by adding entries to ResourceRegistry. No code changes
    /// required to add a new commodity to the economy.
    ///
    /// StackSize is the number of physical units per stack slot.
    /// All cargo capacity, production quantities, and trade bids are
    /// expressed in fractional stacks. Physical transfers use whole units.
    ///
    /// See Core Truths — Economy System, Stack Model.
    /// </summary>
    public class ResourceDefinition
    {
        /// <summary>
        /// Unique internal key. Used in all storage dictionaries and recipes.
        /// Convention: camelCase — "copperOre", "nuclearMissile"
        /// </summary>
        public string Name { get; init; } = "";

        /// <summary>Player-facing display name.</summary>
        public string DisplayName { get; init; } = "";

        /// <summary>
        /// Units per stack slot. Varies dramatically by resource type.
        /// High value = bulk commodity. Low value = precious or hazardous.
        /// </summary>
        public int StackSize { get; init; } = 100;

        /// <summary>
        /// Mass per stack slot in simulation units.
        /// Affects cargo weight calculations when mass matters.
        /// </summary>
        public float MassPerStack { get; init; } = 1f;

        /// <summary>
        /// Broad category for UI grouping and filter purposes.
        /// e.g. "ore", "ingot", "component", "food", "munition", "fuel", "precious"
        /// </summary>
        public string Category { get; init; } = "misc";

        /// <summary>
        /// Whether this resource requires specialized cargo handling.
        /// Specialized resources may only be stored in matching cargo modules.
        /// e.g. dilithium requires a DilithiumVault, not a generic pod.
        /// </summary>
        public bool RequiresSpecializedStorage { get; init; } = false;

        // ------------------------------------------------------------
        // Conversion helpers
        // ------------------------------------------------------------

        /// <summary>Converts a unit count to fractional stacks.</summary>
        public float UnitsToStacks(int units) => (float)units / StackSize;

        /// <summary>Converts fractional stacks to whole units (floors).</summary>
        public int StacksToUnits(float stacks) => (int)(stacks * StackSize);

        /// <summary>
        /// Maximum units that fit in a given number of stack slots.
        /// </summary>
        public int UnitsPerSlots(int slots) => slots * StackSize;
    }
}
