using System;
using System.Collections.Generic;
using System.Numerics;
using StarshipSimulation.Shared.Entities.Components;
using StarshipSimulation.Shared.Entities.Orders;

namespace StarshipSimulation.Shared.Entities
{
    /// <summary>
    /// The fundamental unit of the simulation.
    /// Everything in the universe is an Entity — ships, stations, missiles,
    /// asteroids, beacons, swarms. Behaviour comes entirely from attached components.
    /// </summary>
    public class Entity
    {
        // ------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------

        /// <summary>
        /// Unique identifier. Stable for the lifetime of the entity.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Human-readable name. Optional — not all entities need one.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Classification tag — used by systems and clients to decide
        /// how to treat this entity (e.g. "ship", "station", "swarm", "asteroid").
        /// Not behaviour — just a hint. Behaviour comes from components.
        /// </summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Incremented every time state changes. Used by the delta system
        /// to determine what needs to be sent over the wire.
        /// </summary>
        public long Version { get; set; } = 0;

        /// <summary>
        /// When false, entity is inactive and will be cleaned up next tick.
        /// Use this instead of removing from collections mid-simulation.
        /// </summary>
        public bool IsAlive { get; set; } = true;

        // ------------------------------------------------------------
        // Physics state
        // ------------------------------------------------------------

        /// <summary>
        /// World position on the 2D simulation plane.
        /// </summary>
        public Vector2 Position { get; set; } = Vector2.Zero;

        /// <summary>
        /// Current velocity vector — units per second.
        /// Decoupled from heading. A ship can be pointing one way and moving another.
        /// </summary>
        public Vector2 Velocity { get; set; } = Vector2.Zero;

        /// <summary>
        /// The direction this entity is facing.
        /// Normalised vector. Independent of velocity.
        /// </summary>
        public Vector2 Heading { get; set; } = Vector2.UnitY;

        // ------------------------------------------------------------
        // Components
        // ------------------------------------------------------------

        /// <summary>
        /// Free-floating components attached to this entity.
        /// Systems iterate these to drive behaviour.
        /// </summary>
        public List<IComponent> Components { get; } = new();

        /// <summary>
        /// Named hardpoints — constrained attachment slots.
        /// Examples: "engine", "cargo-1", "weapon-port", "sensor".
        /// Components in hardpoints behave identically to free components.
        /// </summary>
        public Dictionary<string, IComponent?> Hardpoints { get; } = new();

        // ------------------------------------------------------------
        // Orders
        // ------------------------------------------------------------

        /// <summary>The current order this entity is executing. Null if idle.</summary>
        public Order? CurrentOrder { get; set; }

        // ------------------------------------------------------------
        // Traversal plan — pre-computed flight arc (no physics needed)
        // ------------------------------------------------------------

        /// <summary>
        /// Pre-computed flight plan for the current MoveTo leg.
        /// Evaluated each tick instead of running Newtonian physics.
        /// Null when: idle, observed by player (full physics takes over),
        /// or invalidated by damage/new order/route change.
        /// See Core Truths — Traversal Plan.
        /// </summary>
        public TraversalPlan? TraversalPlan { get; set; }

        /// <summary>
        /// Current movement profile — controls thrust fraction, cruise speed,
        /// and arrival behaviour. Defaults to Standard.
        /// TradeSystem uses Standard. CombatSystem can switch to Military.
        /// Low fuel auto-downgrades to Economy via TraversalPlanner.
        /// See Core Truths — Traversal Plan, Movement Profiles.
        /// </summary>
        public MovementProfile CurrentProfile { get; set; } = MovementProfile.Standard;

        // ------------------------------------------------------------
        // Route execution — written by OrderSystem, read by MovementSystem
        // ------------------------------------------------------------

        /// <summary>
        /// The active route being executed. Set by OrderSystem when a job
        /// is assigned. MovementSystem follows the segments in sequence.
        /// Null when idle or between orders.
        /// </summary>
        public NavigationResult? ActiveRoute { get; set; }

        /// <summary>
        /// Index of the segment currently being executed in ActiveRoute.
        /// Incremented by MovementSystem as each segment completes.
        /// Reset to 0 when a new route is assigned.
        /// </summary>
        public int ActiveSegmentIndex { get; set; }

        /// <summary>
        /// Elapsed seconds in the current timed segment (e.g. JumpCharging).
        /// Reset to 0 when the segment advances.
        /// </summary>
        public double SegmentElapsedSeconds { get; set; }

        /// <summary>
        /// Set to true by MovementSystem when the last segment of ActiveRoute
        /// completes. OrderSystem reads this on the next macro tick and
        /// advances the order step. Cleared by OrderSystem after reading.
        /// </summary>
        public bool HasArrived { get; set; }

        // ------------------------------------------------------------
        // Bridge log — always present, written by all systems
        // ------------------------------------------------------------

        /// <summary>
        /// Rolling log of what this entity is doing, why, and what
        /// just happened. Written by every simulation system.
        /// See Core Truths — Entity Reporting.
        /// </summary>
        public EntityLog Log { get; } = new();

        // ------------------------------------------------------------
        // Component access
        // ------------------------------------------------------------

        /// <summary>
        /// Iterates all components — both free and hardpoint-mounted.
        /// Use this in systems rather than accessing Components and Hardpoints separately.
        /// </summary>
        public IEnumerable<IComponent> GetAllComponents()
        {
            foreach (var c in Components)
                if (c != null)
                    yield return c;

            foreach (var kv in Hardpoints)
                if (kv.Value != null)
                    yield return kv.Value;
        }

        /// <summary>
        /// Returns the first component of type T, or null if none found.
        /// Searches both free components and hardpoints.
        /// </summary>
        public T? GetComponent<T>() where T : class, IComponent
        {
            foreach (var c in GetAllComponents())
                if (c is T match)
                    return match;

            return null;
        }

        /// <summary>
        /// Returns true if this entity has at least one component of type T.
        /// </summary>
        public bool HasComponent<T>() where T : class, IComponent
            => GetComponent<T>() != null;

        // ------------------------------------------------------------
        // Versioning helper
        // ------------------------------------------------------------

        /// <summary>
        /// Call this whenever simulation state changes on this entity.
        /// The delta system uses Version to detect what has changed.
        /// </summary>
        public void MarkDirty() => Version++;
    }
}
