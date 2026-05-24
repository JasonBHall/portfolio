using System;
using System.Collections.Generic;
using System.Numerics;
using StarshipSimulation.Server.Simulation.Systems;
using StarshipSimulation.Shared.Entities;

namespace StarshipSimulation.Server.Simulation
{
    /// <summary>
    /// The simulation engine. Owns all entity state and drives all systems.
    ///
    /// Responsibilities:
    ///   - Maintain the authoritative entity set
    ///   - Maintain the spatial grid
    ///   - Run macro and micro tick passes
    ///   - Provide entity creation and removal
    ///
    /// Does not own timing — UniverseTicker controls when ticks fire.
    /// Does not own networking — UniverseStream reads state and pushes it.
    /// Does not own delta tracking — UniverseStream manages per-client versions.
    /// </summary>
    public class UniverseService
    {
        // ------------------------------------------------------------
        // Clock
        // ------------------------------------------------------------

        public UniverseClock Clock { get; }

        // ------------------------------------------------------------
        // Entity store
        // ------------------------------------------------------------

        /// <summary>
        /// All live entities in the simulation, keyed by Id.
        /// This is the single authoritative entity set.
        /// Ships, stations, swarms, asteroids — everything is here.
        /// </summary>
        private readonly Dictionary<string, Entity> _entities = new();

        /// <summary>
        /// Read-only view of the entity store for systems and networking.
        /// </summary>
        public IReadOnlyDictionary<string, Entity> Entities => _entities;

        // ------------------------------------------------------------
        // Spatial index
        // ------------------------------------------------------------

        /// <summary>
        /// Spatial hash grid for fast range queries.
        /// Updated each tick for entities that have moved.
        /// </summary>
        public SpatialGrid SpatialGrid { get; }

        // ------------------------------------------------------------
        // Entity pools
        // ------------------------------------------------------------

        // Pools are pre-allocated at startup. Add more as combat entity
        // types are defined. Capacity tuned from PeakActiveCount in logs.
        public EntityPool MissilePool  { get; }
        public EntityPool FighterPool  { get; }
        public EntityPool FragmentPool { get; }

        // ------------------------------------------------------------
        // Tick counter
        // ------------------------------------------------------------

        public long MacroTick { get; private set; }

        // ------------------------------------------------------------
        // Systems
        // (Uncomment as each system file is added to the project)
        // ------------------------------------------------------------

        public MovementSystem   MovementSystem   { get; }
        public ProductionSystem ProductionSystem { get; }
        public NavigationSystem NavigationSystem { get; }
        public TradeSystem      TradeSystem      { get; }
        public OrderSystem      OrderSystem      { get; }
        public ProjectileSystem ProjectileSystem { get; }
        public WeaponSystem     WeaponSystem     { get; }

        // public PowerSystem      PowerSystem      { get; }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public UniverseService()
        {
            Clock       = new UniverseClock(TimeSpan.FromSeconds(5));
            SpatialGrid = new SpatialGrid(cellSize: 1000f);

            MissilePool  = new EntityPool("Missile",  500,  () => new Entity { Kind = "missile"  });
            FighterPool  = new EntityPool("Fighter",  200,  () => new Entity { Kind = "fighter"  });
            FragmentPool = new EntityPool("Fragment", 1000, () => new Entity { Kind = "fragment" });

            MovementSystem   = new MovementSystem(this);
            ProductionSystem = new ProductionSystem(this);
            NavigationSystem = new NavigationSystem(this);
            TradeSystem      = new TradeSystem(this, NavigationSystem);
            OrderSystem      = new OrderSystem(this, TradeSystem, NavigationSystem);
            ProjectileSystem = new ProjectileSystem(this);
            WeaponSystem     = new WeaponSystem(this);
        }

        // ------------------------------------------------------------
        // Macro tick
        // Runs on a configured interval (default 5s).
        // Drives economy, production, trade, unobserved movement.
        // ------------------------------------------------------------

        public void RunMacroTick(double deltaSeconds)
        {
            MacroTick++;

            // Systems tick in dependency order:
            // Production generates resources
            // Order advances steps (reads HasArrived from MovementSystem)
            // Trade schedules new jobs for idle ships
            ProductionSystem.Tick(deltaSeconds);
            OrderSystem.Tick(deltaSeconds);
            TradeSystem.Tick(deltaSeconds);

            CleanupDeadEntities();
        }

        // ------------------------------------------------------------
        // Micro tick
        // Runs at ~20Hz. Drives observed entities and real-time physics.
        // ------------------------------------------------------------

        public void RunMicroTick(double deltaSeconds)
        {
            // Order: weapons fire first so newly-spawned projectiles get drifted
            // this same tick by MovementSystem. ProjectileSystem expires last so
            // shots spawned this tick always get at least one frame of life.
            WeaponSystem.Tick(deltaSeconds);
            MovementSystem.Tick(deltaSeconds);
            ProjectileSystem.Tick(deltaSeconds);
            UpdateSpatialGrid();
        }

        // ------------------------------------------------------------
        // Entity management
        // ------------------------------------------------------------

        /// <summary>
        /// Creates a new entity, registers it in the entity store and
        /// spatial grid, and returns it for further configuration.
        /// </summary>
        public Entity CreateEntity(string name, string kind, Vector2 position)
        {
            var entity = new Entity
            {
                Name     = name,
                Kind     = kind,
                Position = position
            };

            _entities[entity.Id] = entity;
            SpatialGrid.Insert(entity);

            Console.WriteLine($"[Universe] Created {kind} '{name}' at {position}");

            return entity;
        }

        /// <summary>
        /// Acquires a pooled entity, registers it in the simulation,
        /// and returns it ready for use.
        /// </summary>
        public Entity AcquirePooled(EntityPool pool, Vector2 position, string name = "")
        {
            var entity = pool.Acquire();
            entity.Name     = name;
            entity.Position = position;

            _entities[entity.Id] = entity;
            SpatialGrid.Insert(entity);

            return entity;
        }

        /// <summary>
        /// Marks an entity for removal. It will be cleaned up at end of
        /// the current macro tick. Never remove from _entities mid-tick.
        /// </summary>
        public void DestroyEntity(string entityId)
        {
            if (_entities.TryGetValue(entityId, out var entity))
                entity.IsAlive = false;
        }

        /// <summary>
        /// Finds an entity by Id. Returns null if not found.
        /// </summary>
        public Entity? GetEntity(string entityId)
            => _entities.TryGetValue(entityId, out var e) ? e : null;

        // ------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------

        /// <summary>
        /// Updates the spatial grid for any entity that has moved this tick.
        /// Called at the end of each micro tick.
        /// </summary>
        private void UpdateSpatialGrid()
        {
            foreach (var entity in _entities.Values)
            {
                if (entity.IsAlive && entity.Velocity != Vector2.Zero)
                    SpatialGrid.UpdateEntity(entity);
            }
        }

        /// <summary>
        /// Removes dead entities from the store and spatial grid.
        /// Called at the end of each macro tick to keep collections clean.
        /// </summary>
        private void CleanupDeadEntities()
        {
            var toRemove = new List<string>();

            foreach (var (id, entity) in _entities)
            {
                if (!entity.IsAlive)
                    toRemove.Add(id);
            }

            foreach (var id in toRemove)
            {
                SpatialGrid.Remove(_entities[id]);
                _entities.Remove(id);
            }

            if (toRemove.Count > 0)
                Console.WriteLine($"[Universe] Cleaned up {toRemove.Count} dead entities.");
        }
    }
}
