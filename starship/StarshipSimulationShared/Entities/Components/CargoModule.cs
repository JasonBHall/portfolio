using System;
using System.Collections.Generic;
using System.Linq;
using StarshipSimulation.Shared.Economy;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// A physical cargo container. Capacity is measured in stack slots.
    ///
    /// One stack slot holds one stack of any resource.
    /// How many units that represents depends on the resource's StackSize:
    ///
    ///   Iron ore (StackSize 200):  50 slots → 10,000 units max
    ///   Torpedo  (StackSize 1):    50 slots → 50 units max
    ///   Mixed:   25 slots iron ore (5,000 units)
    ///            25 slots torpedoes (25 units)
    ///
    /// Slot consumption rule: ceil(units / StackSize)
    ///   1 unit of iron ore = 1 slot (not 0.005 slots)
    ///   200 units of iron ore = 1 slot
    ///   201 units of iron ore = 2 slots
    ///
    /// A ship with no cargo modules cannot accept trade contracts.
    /// ShipStatsComponent aggregates TotalStackSlots across all modules.
    ///
    /// See Core Truths — Cargo, Stack Slot Model.
    /// </summary>
    public class CargoModule : ModuleBase
    {
        // ------------------------------------------------------------
        // IModule — identity
        // ------------------------------------------------------------

        public override string Name        { get; }
        public override string DisplayName { get; }
        public override string Description { get; }
        public override string SlotType    => "cargo";
        public override int    SlotSize    { get; }
        public override float  Mass        { get; }

        // ------------------------------------------------------------
        // Cargo capacity
        // ------------------------------------------------------------

        /// <summary>
        /// Physical slot capacity of this module.
        /// One slot = one stack of any resource.
        /// </summary>
        public int StackSlots { get; }

        /// <summary>
        /// Resource this module is specialised for.
        /// "generic" accepts any resource.
        /// Specialised modules only accept their designated type.
        /// </summary>
        public string ResourceType { get; }

        // ------------------------------------------------------------
        // Contents — authoritative storage
        // Key = resource name, Value = units stored
        // ------------------------------------------------------------

        public Dictionary<string, int> Contents { get; } = new();

        // ------------------------------------------------------------
        // Derived slot state
        // ------------------------------------------------------------

        /// <summary>
        /// Total slots consumed by current contents.
        /// Each resource occupies ceil(units / StackSize) slots.
        /// </summary>
        public int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (var (resource, units) in Contents)
                {
                    if (units <= 0) continue;
                    int stackSize = GetStackSize(resource);
                    used += (int)Math.Ceiling((double)units / stackSize);
                }
                return used;
            }
        }

        /// <summary>Available slots for new cargo.</summary>
        public int FreeSlots => Math.Max(0, StackSlots - UsedSlots);

        /// <summary>0.0 empty → 1.0 full.</summary>
        public float FillFraction =>
            StackSlots > 0
                ? Math.Clamp((float)UsedSlots / StackSlots, 0f, 1f)
                : 0f;

        // ------------------------------------------------------------
        // Capacity helpers
        // ------------------------------------------------------------

        /// <summary>
        /// Maximum units of a specific resource this module can hold,
        /// given its current free slots.
        /// </summary>
        public int MaxUnitsForResource(string resource)
        {
            int stackSize = GetStackSize(resource);
            return FreeSlots * stackSize;
        }

        /// <summary>
        /// Whether this module can accept the given resource type.
        /// </summary>
        public bool CanAccept(string resourceType) =>
            ResourceType == "generic" || ResourceType == resourceType;

        /// <summary>Returns how many units of a resource are stored here.</summary>
        public int GetAmount(string resource) =>
            Contents.TryGetValue(resource, out var amount) ? amount : 0;

        /// <summary>
        /// How many slots are consumed by the stored units of one resource.
        /// </summary>
        public int SlotsUsedBy(string resource)
        {
            int units = GetAmount(resource);
            if (units <= 0) return 0;
            int stackSize = GetStackSize(resource);
            return (int)Math.Ceiling((double)units / stackSize);
        }

        // ------------------------------------------------------------
        // Load / Unload — called by OrderSystem / TradeSystem
        // ------------------------------------------------------------

        /// <summary>
        /// Loads up to the requested units of a resource.
        /// Slot constraint is enforced — will not exceed StackSlots.
        /// Returns actual units loaded (may be less than requested).
        /// </summary>
        public int Load(string resource, int requestedUnits)
        {
            if (!IsOperational)  return 0;
            if (!CanAccept(resource)) return 0;
            if (requestedUnits <= 0)  return 0;

            int stackSize = GetStackSize(resource);
            int freeSlots = FreeSlots;
            if (freeSlots <= 0) return 0;

            // How many units fit in remaining free slots?
            int unitCapacity = freeSlots * stackSize;
            int toLoad       = Math.Min(requestedUnits, unitCapacity);
            if (toLoad <= 0) return 0;

            Contents[resource] = GetAmount(resource) + toLoad;
            return toLoad;
        }

        /// <summary>
        /// Unloads up to the requested units of a resource.
        /// Returns actual units unloaded.
        /// </summary>
        public int Unload(string resource, int requestedUnits)
        {
            if (requestedUnits <= 0) return 0;

            int available = GetAmount(resource);
            int toUnload  = Math.Min(requestedUnits, available);
            if (toUnload <= 0) return 0;

            int remaining = available - toUnload;
            if (remaining <= 0)
                Contents.Remove(resource);
            else
                Contents[resource] = remaining;

            return toUnload;
        }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public CargoModule(CargoModuleConfig config)
        {
            Name         = config.Name;
            DisplayName  = config.DisplayName;
            Description  = config.Description;
            SlotSize     = config.SlotSize;
            Mass         = config.Mass;
            StackSlots   = config.StackSlots;
            ResourceType = config.ResourceType;
        }

        // ------------------------------------------------------------
        // Tick — no passive behaviour
        // ------------------------------------------------------------

        public override void Tick(Entity owner, double deltaTime) { }

        // ------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------

        private static int GetStackSize(string resource)
        {
            if (ResourceRegistry.TryGet(resource, out var def) && def != null)
                return Math.Max(1, def.StackSize);
            return 1; // unknown resource → 1 unit per slot (safe fallback)
        }
    }

    // ------------------------------------------------------------
    // Config record
    // ------------------------------------------------------------

    public record CargoModuleConfig
    {
        public string Name         { get; init; } = "cargo_module";
        public string DisplayName  { get; init; } = "Cargo Module";
        public string Description  { get; init; } = string.Empty;
        public int    SlotSize     { get; init; } = 4;
        public float  Mass         { get; init; } = 2f;
        public int    StackSlots   { get; init; } = 50;
        public string ResourceType { get; init; } = "generic";
    }
}
