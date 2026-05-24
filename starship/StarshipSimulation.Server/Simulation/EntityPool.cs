using System;
using System.Collections.Generic;
using StarshipSimulation.Shared.Entities;

namespace StarshipSimulation.Server.Simulation
{
    /// <summary>
    /// Pre-allocated object pool for short-lived combat entities.
    ///
    /// Prevents runtime allocation and GC pressure during intense combat
    /// scenarios — missile swarms, debris fields, fighter wings.
    ///
    /// Usage:
    ///   var pool = new EntityPool(capacity: 500, factory: () => new Entity());
    ///   var missile = pool.Acquire();     // get one from the pool
    ///   missile.IsAlive = true;
    ///   ...
    ///   pool.Release(missile);            // return it when done
    ///
    /// If the pool is exhausted, Acquire() expands it automatically.
    /// A warning is logged so you can tune capacity at startup.
    /// </summary>
    public class EntityPool
    {
        // ------------------------------------------------------------
        // State
        // ------------------------------------------------------------

        private readonly Stack<Entity> _available;
        private readonly Func<Entity> _factory;
        private readonly string _poolName;

        private int _totalCreated;
        private int _peakActive;
        private int _currentActive;

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        /// <param name="poolName">Used in log messages — e.g. "Missile", "Fighter", "Debris"</param>
        /// <param name="capacity">Pre-allocated count at startup.</param>
        /// <param name="factory">How to create a new Entity for this pool.</param>
        public EntityPool(string poolName, int capacity, Func<Entity> factory)
        {
            _poolName = poolName;
            _factory = factory;
            _available = new Stack<Entity>(capacity);
            _totalCreated = 0;

            Preallocate(capacity);
        }

        // ------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------

        /// <summary>
        /// Acquires an entity from the pool.
        /// Resets the entity to a clean state before returning it.
        /// If the pool is empty, a new entity is created and a warning logged.
        /// </summary>
        public Entity Acquire()
        {
            Entity entity;

            if (_available.Count > 0)
            {
                entity = _available.Pop();
            }
            else
            {
                // Pool exhausted — expand silently but log for tuning
                Console.WriteLine($"[EntityPool:{_poolName}] Pool exhausted — expanding. " +
                                  $"Consider increasing capacity (currently {_totalCreated}).");
                entity = CreateNew();
            }

            Reset(entity);

            _currentActive++;
            if (_currentActive > _peakActive)
                _peakActive = _currentActive;

            return entity;
        }

        /// <summary>
        /// Returns an entity to the pool.
        /// Marks it inactive so the simulation ignores it next tick.
        /// </summary>
        public void Release(Entity entity)
        {
            entity.IsAlive = false;
            _available.Push(entity);
            _currentActive--;
        }

        /// <summary>
        /// Current number of entities checked out of the pool.
        /// </summary>
        public int ActiveCount => _currentActive;

        /// <summary>
        /// Peak concurrent active count since startup.
        /// Use to tune initial capacity.
        /// </summary>
        public int PeakActiveCount => _peakActive;

        /// <summary>
        /// Total entities ever created by this pool (pre-allocated + expansions).
        /// </summary>
        public int TotalCreated => _totalCreated;

        // ------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------

        private void Preallocate(int count)
        {
            for (int i = 0; i < count; i++)
                _available.Push(CreateNew());
        }

        private Entity CreateNew()
        {
            _totalCreated++;
            return _factory();
        }

        /// <summary>
        /// Resets an entity to a clean baseline before it is re-used.
        /// Components are not cleared here — pools are type-specific and
        /// the factory is responsible for attaching the right components.
        /// Systems reset component state via their own initialisation logic.
        /// </summary>
        private static void Reset(Entity entity)
        {
            entity.IsAlive      = true;
            entity.Version      = 0;
            entity.Position     = System.Numerics.Vector2.Zero;
            entity.Velocity     = System.Numerics.Vector2.Zero;
            entity.Heading      = System.Numerics.Vector2.UnitY;
            entity.CurrentOrder = null;
        }
    }
}
