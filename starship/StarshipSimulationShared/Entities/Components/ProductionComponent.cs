using System;
using System.Collections.Generic;
using System.Linq;
using StarshipSimulation.Shared.Economy;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// A physical production module — a factory, mine, smelter, constructor,
    /// or any device that transforms resources over time.
    ///
    /// Each ProductionComponent runs one recipe at a time. An entity may
    /// have multiple ProductionComponents running different recipes in parallel
    /// (a station with both a smelter and a missile constructor).
    ///
    /// Storage model — per-ingredient bunkers:
    ///   InputBunkers  — one slot per recipe input resource
    ///   OutputBunkers — one slot per recipe output resource
    ///
    /// Bunker capacity is in stack slots. Physical amounts are in units.
    /// Both are tracked — stacks for scheduling math, units for transfers.
    ///
    /// The ProductionSystem ticks this component. The TradeSystem reads
    /// needs and surpluses. The OrderSystem executes transfers.
    ///
    /// See Core Truths — Economy System.
    /// </summary>
    public class ProductionComponent : ModuleBase
    {
        // ------------------------------------------------------------
        // IModule — identity
        // ------------------------------------------------------------

        public override string Name        { get; }
        public override string DisplayName { get; }
        public override string Description { get; }
        public override string SlotType    => "production";
        public override int    SlotSize    { get; }
        public override float  Mass        { get; }

        // ------------------------------------------------------------
        // Recipe
        // ------------------------------------------------------------

        /// <summary>
        /// Currently active recipe. Null = idle.
        /// Setting a recipe initializes bunker storage keys.
        /// </summary>
        public ProductionRecipe? ActiveRecipe { get; private set; }

        /// <summary>
        /// Accumulated production progress in seconds.
        /// When >= ActiveRecipe.ProductionTimeSeconds → one cycle fires.
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// Total production cycles completed since this component was created.
        /// Never resets — useful for dashboard display and rate calculations.
        /// </summary>
        public int CyclesCompleted { get; private set; }

        // ------------------------------------------------------------
        // Storage — per-ingredient bunkers
        // Key = resource name, Value = units stored
        // ------------------------------------------------------------

        /// <summary>
        /// Input bunkers. One entry per recipe input resource.
        /// Initialized when recipe is set.
        /// Consumed each production cycle.
        /// </summary>
        public Dictionary<string, int> InputBunkers { get; } = new();

        /// <summary>
        /// Output bunkers. One entry per recipe output resource.
        /// Filled each production cycle.
        /// Drawn down by TradeSystem when ships load cargo.
        /// </summary>
        public Dictionary<string, int> OutputBunkers { get; } = new();

        // ------------------------------------------------------------
        // Bunker capacity — in stack slots
        // ------------------------------------------------------------

        /// <summary>
        /// Maximum stack slots per input bunker.
        /// All input bunkers share the same slot limit.
        /// </summary>
        public int InputBunkerSlots { get; }

        /// <summary>
        /// Maximum stack slots per output bunker.
        /// </summary>
        public int OutputBunkerSlots { get; }

        // ------------------------------------------------------------
        // Capacity in units (derived — uses ResourceRegistry)
        // ------------------------------------------------------------

        /// <summary>
        /// Maximum units this input bunker can hold for a given resource.
        /// </summary>
        public int GetInputCapacityUnits(string resource)
        {
            if (!ResourceRegistry.TryGet(resource, out var def) || def == null)
                return InputBunkerSlots * 100; // fallback
            return InputBunkerSlots * def.StackSize;
        }

        /// <summary>
        /// Maximum units this output bunker can hold for a given resource.
        /// </summary>
        public int GetOutputCapacityUnits(string resource)
        {
            if (!ResourceRegistry.TryGet(resource, out var def) || def == null)
                return OutputBunkerSlots * 100;
            return OutputBunkerSlots * def.StackSize;
        }

        // ------------------------------------------------------------
        // Need and surplus reporting
        // Used by TradeSystem scheduler each macro tick.
        // ------------------------------------------------------------

        /// <summary>
        /// Reports needs for each input bunker.
        /// needScore = (current + inTransit) / maxCapacity  (all in units)
        /// Lower score = higher urgency.
        /// </summary>
        public IEnumerable<ResourceNeed> GetNeeds(
            string ownerEntityId,
            Dictionary<string, int> inTransitByResource)
        {
            if (ActiveRecipe == null || ActiveRecipe.IsExtractor)
                yield break;

            foreach (var (resource, _) in ActiveRecipe.Inputs)
            {
                int currentUnits = InputBunkers.GetValueOrDefault(resource, 0);
                int maxUnits     = GetInputCapacityUnits(resource);
                if (maxUnits <= 0) continue;

                int inTransit = inTransitByResource.GetValueOrDefault(resource, 0);
                int freeUnits = Math.Max(0, maxUnits - currentUnits - inTransit);
                if (freeUnits <= 0) continue;

                float needScore = (float)(currentUnits + inTransit) / maxUnits;

                yield return new ResourceNeed
                {
                    EntityId    = ownerEntityId,
                    Resource    = resource,
                    AmountUnits = freeUnits,
                    NeedScore   = needScore
                };
            }
        }

        /// <summary>
        /// Reports surplus for each output bunker.
        /// Surplus = units in output bunker minus units already spoken for
        /// by active trade jobs heading away from this entity.
        /// </summary>
        public IEnumerable<ResourceSurplus> GetSurpluses(
            string ownerEntityId,
            Dictionary<string, int> spokenForByResource)
        {
            if (ActiveRecipe == null) yield break;

            foreach (var (resource, _) in ActiveRecipe.Outputs)
            {
                int available  = OutputBunkers.GetValueOrDefault(resource, 0);
                int spokenFor  = spokenForByResource.GetValueOrDefault(resource, 0);
                int netSurplus = Math.Max(0, available - spokenFor);

                if (netSurplus <= 0) continue;

                yield return new ResourceSurplus
                {
                    EntityId    = ownerEntityId,
                    Resource    = resource,
                    AmountUnits = netSurplus
                };
            }
        }

        // ------------------------------------------------------------
        // Recipe management
        // ------------------------------------------------------------

        /// <summary>
        /// Sets the active recipe and initializes bunker storage entries.
        /// Resets production progress.
        /// Throws if recipe name is not registered.
        /// </summary>
        public void SetRecipe(string recipeName)
        {
            var recipe = ProductionRecipeRegistry.Get(recipeName);
            SetRecipe(recipe);
        }

        public void SetRecipe(ProductionRecipe recipe)
        {
            ActiveRecipe = recipe;
            Progress     = 0f;

            // Initialize input bunker keys
            foreach (var resource in recipe.Inputs.Keys)
                if (!InputBunkers.ContainsKey(resource))
                    InputBunkers[resource] = 0;

            // Initialize output bunker keys
            foreach (var resource in recipe.Outputs.Keys)
                if (!OutputBunkers.ContainsKey(resource))
                    OutputBunkers[resource] = 0;
        }

        // ------------------------------------------------------------
        // Production tick — called by ProductionSystem
        // ------------------------------------------------------------

        public override void Tick(Entity owner, double deltaTime)
        {
            if (!IsOperational || ActiveRecipe == null) return;

            // --- For non-extractors: only tick if minimum inputs are present ---
            // Progress doesn't accumulate while starved — factory is idle, not running.
            // This avoids misleading progress bars and reduces wire traffic.
            if (!ActiveRecipe.IsExtractor)
            {
                bool hasMinimumInputs = true;
                foreach (var (resource, unitsRequired) in ActiveRecipe.Inputs)
                {
                    int available = InputBunkers.GetValueOrDefault(resource, 0);
                    if (available < unitsRequired)
                    {
                        hasMinimumInputs = false;
                        break;
                    }
                }

                if (!hasMinimumInputs)
                {
                    // Starved — do not tick progress, mark dirty only if status changes
                    return;
                }
            }

            // --- Accumulate progress ---
            if (Progress < ActiveRecipe.ProductionTimeSeconds)
            {
                Progress += (float)deltaTime;
                Progress  = System.Math.Min(Progress, ActiveRecipe.ProductionTimeSeconds);
                owner.MarkDirty();
            }

            if (Progress < ActiveRecipe.ProductionTimeSeconds) return;

            // --- Check output space ---
            foreach (var (resource, unitsProduced) in ActiveRecipe.Outputs)
            {
                int current  = OutputBunkers.GetValueOrDefault(resource, 0);
                int capacity = GetOutputCapacityUnits(resource);
                if (current + unitsProduced > capacity) return; // output full — hold
            }

            // --- All checks passed — execute the cycle ---
            Progress = 0f;

            if (!ActiveRecipe.IsExtractor)
            {
                foreach (var (resource, unitsRequired) in ActiveRecipe.Inputs)
                    InputBunkers[resource] -= unitsRequired;
            }

            foreach (var (resource, unitsProduced) in ActiveRecipe.Outputs)
            {
                int current = OutputBunkers.GetValueOrDefault(resource, 0);
                OutputBunkers[resource] = current + unitsProduced;
            }

            CyclesCompleted++;
            owner.MarkDirty();
        }

        // ------------------------------------------------------------
        // Transfer helpers — called by OrderSystem
        // ------------------------------------------------------------

        /// <summary>
        /// Loads up to the requested units from OutputBunker → ship cargo.
        /// Returns actual units loaded.
        /// Called by OrderSystem when a ship arrives to collect.
        public int TransferOut(string resource, int requestedUnits, Entity carrier)
        {
            int available = OutputBunkers.GetValueOrDefault(resource, 0);
            if (available <= 0) return 0;

            int toTransfer = Math.Min(available, requestedUnits);

            int loaded = 0;
            foreach (var cargo in carrier.GetAllComponents()
                                         .OfType<CargoModule>()
                                         .Where(c => c.CanAccept(resource) && c.IsOperational))
            {
                int given = cargo.Load(resource, toTransfer - loaded);
                loaded += given;
                if (loaded >= toTransfer) break;
            }

            if (loaded > 0)
            {
                OutputBunkers[resource] -= loaded;
                // Carrier's cargo stats must recalculate so FreeStackSlots is fresh
                // for the next TradeSystem availability check
                carrier.GetComponent<ShipStatsComponent>()?.Invalidate();
            }

            return loaded;
        }

        public int TransferIn(string resource, int requestedUnits, Entity carrier)
        {
            int inputCapacity = GetInputCapacityUnits(resource);
            int currentInput  = InputBunkers.GetValueOrDefault(resource, 0);
            int freeSpace     = Math.Max(0, inputCapacity - currentInput);
            if (freeSpace <= 0) return 0;

            int toTransfer = Math.Min(requestedUnits, freeSpace);
            int unloaded   = 0;

            foreach (var cargo in carrier.GetAllComponents()
                                         .OfType<CargoModule>()
                                         .Where(c => c.CanAccept(resource) && c.IsOperational))
            {
                int taken = cargo.Unload(resource, toTransfer - unloaded);
                unloaded += taken;
                if (unloaded >= toTransfer) break;
            }

            if (unloaded > 0)
            {
                InputBunkers[resource] = currentInput + unloaded;
                // Carrier's cargo stats must recalculate so FreeStackSlots reflects
                // the emptied hold — entity becomes available for next trade contract
                carrier.GetComponent<ShipStatsComponent>()?.Invalidate();
            }

            return unloaded;
        }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public ProductionComponent(ProductionComponentConfig config)
        {
            Name               = config.Name;
            DisplayName        = config.DisplayName;
            Description        = config.Description;
            SlotSize           = config.SlotSize;
            Mass               = config.Mass;
            InputBunkerSlots   = config.InputBunkerSlots;
            OutputBunkerSlots  = config.OutputBunkerSlots;

            if (!string.IsNullOrEmpty(config.DefaultRecipe))
                SetRecipe(config.DefaultRecipe);
        }
    }

    // ------------------------------------------------------------
    // Need and surplus report types — used by TradeSystem
    // ------------------------------------------------------------

    public class ResourceNeed
    {
        public string EntityId    { get; set; } = "";
        public string Resource    { get; set; } = "";
        public int    AmountUnits { get; set; }
        /// <summary>Lower = more urgent. 0 = completely empty.</summary>
        public float  NeedScore   { get; set; }
    }

    public class ResourceSurplus
    {
        public string EntityId    { get; set; } = "";
        public string Resource    { get; set; } = "";
        public int    AmountUnits { get; set; }
    }

    // ------------------------------------------------------------
    // Config record
    // ------------------------------------------------------------

    public record ProductionComponentConfig
    {
        public string Name              { get; init; } = "production_module";
        public string DisplayName       { get; init; } = "Production Module";
        public string Description       { get; init; } = "";
        public int    SlotSize          { get; init; } = 4;
        public float  Mass              { get; init; } = 12f;
        public int    InputBunkerSlots  { get; init; } = 10;
        public int    OutputBunkerSlots { get; init; } = 10;
        public string DefaultRecipe     { get; init; } = "";
    }
}
