using System;
using StarshipSimulation.Shared.Entities.Components;

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Sweeps the entity list each micro tick and marks expired projectiles
    /// dead. Cleanup is handled centrally by UniverseService.CleanupDeadEntities
    /// on the next macro tick — this system just sets the IsAlive flag.
    ///
    /// Scaffolding. Subsumed by LifetimeSystem (backlog) once that exists.
    /// </summary>
    public class ProjectileSystem
    {
        private readonly UniverseService _universe;

        public ProjectileSystem(UniverseService universe)
        {
            _universe = universe;
        }

        public void Tick(double deltaTime)
        {
            var now = DateTime.UtcNow;

            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                var proj = entity.GetComponent<ProjectileComponent>();
                if (proj == null) continue;

                if ((now - proj.SpawnedAtUtc).TotalSeconds >= proj.LifetimeSeconds)
                    _universe.DestroyEntity(entity.Id);
            }
        }
    }
}
