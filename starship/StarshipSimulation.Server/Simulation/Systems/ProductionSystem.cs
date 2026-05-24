using System;
using System.Collections.Generic;
using System.Linq;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Ticks all ProductionComponents on all entities each macro tick.
    ///
    /// Responsibilities:
    ///   - Drive production progress on every entity with a ProductionComponent
    ///   - Log production events for debugging and future narrative events
    ///   - Provide a read-only view of all active producers for TradeSystem
    ///
    /// What it does NOT do:
    ///   - Manage resource transfers (OrderSystem responsibility)
    ///   - Schedule trade jobs (TradeSystem responsibility)
    ///   - Decide what gets produced (ProductionComponent + recipe responsibility)
    ///
    /// ProductionComponent.Tick() already calls entity.MarkDirty() on a
    /// successful cycle, so this system does not need to detect storage changes.
    /// The delta system picks up Version changes automatically.
    ///
    /// Runs on the macro tick — production is background simulation.
    /// Entities within observer range that are producing will appear to
    /// produce in real-time because the macro tick fires every 5 seconds
    /// and the client interpolates. This is correct behaviour.
    /// </summary>
    public class ProductionSystem
    {
        private readonly UniverseService _universe;

        public ProductionSystem(UniverseService universe)
        {
            _universe = universe;
        }

        // ------------------------------------------------------------
        // Tick — called by UniverseService.RunMacroTick()
        // ------------------------------------------------------------

        public void Tick(double deltaSeconds)
        {
            int cyclesCompleted = 0;

            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                var producers = entity.GetAllComponents()
                                      .OfType<ProductionComponent>()
                                      .Where(p => p.IsOperational && p.ActiveRecipe != null)
                                      .ToList();

                if (producers.Count == 0) continue;

                foreach (var producer in producers)
                {
                    var outputBefore = SnapshotStorage(producer.OutputBunkers);

                    producer.Tick(entity, deltaSeconds);

                    if (OutputChanged(outputBefore, producer.OutputBunkers))
                    {
                        cyclesCompleted++;
                        LogProduction(entity, producer);

                        var outputs = string.Join(", ",
                            producer.OutputBunkers
                                .Where(kv => kv.Value > 0)
                                .Select(kv => $"{kv.Value} {kv.Key}"));

                        entity.Log.Info("Production",
                            $"Cycle {producer.CyclesCompleted}: {outputs}");
                        entity.Log.SetStatus(
                            $"Producing: {producer.ActiveRecipe!.DisplayName} " +
                            $"(cycle {producer.CyclesCompleted})");
                    }
                    else if (producer.Progress >= producer.ActiveRecipe!.ProductionTimeSeconds)
                    {
                        // At 100% but blocked — starved or output full
                        var starvedOn = producer.ActiveRecipe.Inputs
                            .Where(kvp =>
                                producer.InputBunkers.GetValueOrDefault(kvp.Key, 0) < kvp.Value)
                            .Select(kvp => kvp.Key)
                            .FirstOrDefault();

                        if (starvedOn != null)
                        {
                            entity.Log.SetStatus($"Starved — waiting for {starvedOn}");

                            // Only log once per starvation event, not every tick
                            var last = entity.Log.Entries.LastOrDefault();
                            if (last?.Message.Contains("Starved") != true)
                                entity.Log.Warning("Production",
                                    $"Starved: {starvedOn} insufficient in input bunker");
                        }
                        else
                        {
                            entity.Log.SetStatus(
                                $"Output full — {producer.ActiveRecipe.DisplayName} waiting");
                        }
                    }
                    else
                    {
                        float pct = producer.ActiveRecipe.ProductionTimeSeconds > 0
                            ? producer.Progress / producer.ActiveRecipe.ProductionTimeSeconds * 100f
                            : 0f;
                        entity.Log.SetStatus(
                            $"Producing: {producer.ActiveRecipe.DisplayName} ({pct:F0}%)");
                    }
                }
            }

            if (cyclesCompleted > 0)
                Console.WriteLine(
                    $"[ProductionSystem] {cyclesCompleted} cycle(s) completed this tick.");
        }

        // ------------------------------------------------------------
        // Query — used by TradeSystem to find all active producers
        // Returns all entities that have at least one operational
        // ProductionComponent with a recipe set.
        // ------------------------------------------------------------

        public IEnumerable<(Entity entity, ProductionComponent producer)> GetAllProducers()
        {
            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                foreach (var producer in entity.GetAllComponents()
                                               .OfType<ProductionComponent>()
                                               .Where(p => p.IsOperational &&
                                                           p.ActiveRecipe != null))
                {
                    yield return (entity, producer);
                }
            }
        }

        // ------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------

        private static Dictionary<string, int> SnapshotStorage(
            Dictionary<string, int> storage)
        {
            return new Dictionary<string, int>(storage);
        }

        private static bool OutputChanged(
            Dictionary<string, int> before,
            Dictionary<string, int> after)
        {
            foreach (var (key, value) in after)
            {
                if (!before.TryGetValue(key, out var beforeValue) ||
                    beforeValue != value)
                    return true;
            }
            return false;
        }

        private static void LogProduction(Entity entity, ProductionComponent producer)
        {
            var outputs = string.Join(", ",
                producer.OutputBunkers
                    .Where(kv => kv.Value > 0)
                    .Select(kv => $"{kv.Value} {kv.Key}"));

            Console.WriteLine(
                $"[Production] {entity.Name} — {producer.ActiveRecipe!.DisplayName} " +
                $"→ output: [{outputs}]");
        }
    }
}
