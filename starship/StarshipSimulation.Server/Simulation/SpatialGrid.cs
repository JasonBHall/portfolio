using System;
using System.Collections.Generic;
using System.Numerics;
using StarshipSimulation.Shared.Entities;

namespace StarshipSimulation.Server.Simulation
{
    /// <summary>
    /// Spatial hash grid for fast range queries against the entity set.
    ///
    /// Divides the simulation plane into fixed-size cells. Entities register
    /// into the cell that contains their position. Range queries only check
    /// cells that overlap the query radius — O(1) average case vs O(n) linear scan.
    ///
    /// Used by:
    ///   - Observer system   (what can this entity see?)
    ///   - Combat system     (what is in weapon range?)
    ///   - Trade system      (what cargo ships are near this facility?)
    ///   - Collision system  (what is near this missile?)
    ///
    /// Call UpdateEntity() each tick for any entity that has moved.
    /// Call Remove() when an entity dies.
    /// </summary>
    public class SpatialGrid
    {
        // ------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------

        /// <summary>
        /// Size of each grid cell in world units.
        /// Should be set to roughly the largest common query radius.
        /// Smaller = more cells, less work per query.
        /// Larger = fewer cells, more entities per cell.
        /// </summary>
        private readonly float _cellSize;
        private readonly float _inverseCellSize;

        // ------------------------------------------------------------
        // Storage
        // ------------------------------------------------------------

        /// <summary>
        /// Maps grid cell key → set of entities in that cell.
        /// </summary>
        private readonly Dictionary<long, HashSet<Entity>> _cells = new();

        /// <summary>
        /// Tracks which cell each entity is currently in.
        /// Used to efficiently move entities between cells on update.
        /// </summary>
        private readonly Dictionary<string, long> _entityCell = new();

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        /// <param name="cellSize">
        /// World-unit size of each grid cell.
        /// A value matching your typical sensor/weapon range works well.
        /// Example: 1000f for a universe measured in thousands of units.
        /// </param>
        public SpatialGrid(float cellSize = 1000f)
        {
            _cellSize = cellSize;
            _inverseCellSize = 1f / cellSize;
        }

        // ------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------

        /// <summary>
        /// Inserts a new entity into the grid.
        /// Call once when an entity is spawned.
        /// </summary>
        public void Insert(Entity entity)
        {
            var key = GetCellKey(entity.Position);
            GetOrCreateCell(key).Add(entity);
            _entityCell[entity.Id] = key;
        }

        /// <summary>
        /// Updates an entity's grid position after it has moved.
        /// Only performs work if the entity has crossed a cell boundary.
        /// Call every tick for moving entities.
        /// </summary>
        public void UpdateEntity(Entity entity)
        {
            var newKey = GetCellKey(entity.Position);

            if (_entityCell.TryGetValue(entity.Id, out var oldKey) && oldKey == newKey)
                return; // Still in the same cell — nothing to do

            // Move to new cell
            if (_cells.TryGetValue(oldKey, out var oldCell))
                oldCell.Remove(entity);

            GetOrCreateCell(newKey).Add(entity);
            _entityCell[entity.Id] = newKey;
        }

        /// <summary>
        /// Removes an entity from the grid.
        /// Call when an entity dies or is despawned.
        /// </summary>
        public void Remove(Entity entity)
        {
            if (!_entityCell.TryGetValue(entity.Id, out var key))
                return;

            if (_cells.TryGetValue(key, out var cell))
                cell.Remove(entity);

            _entityCell.Remove(entity.Id);
        }

        /// <summary>
        /// Returns all entities within radius of the given position.
        /// Results include entities at the exact boundary.
        /// Does not allocate — writes into the provided results list.
        /// </summary>
        public void QueryRadius(Vector2 center, float radius, List<Entity> results)
        {
            results.Clear();

            var radiusSq = radius * radius;

            // Calculate the cell range that could overlap the query circle
            int minCellX = WorldToCell(center.X - radius);
            int maxCellX = WorldToCell(center.X + radius);
            int minCellY = WorldToCell(center.Y - radius);
            int maxCellY = WorldToCell(center.Y + radius);

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    var key = MakeCellKey(cx, cy);

                    if (!_cells.TryGetValue(key, out var cell))
                        continue;

                    foreach (var entity in cell)
                    {
                        if (!entity.IsAlive)
                            continue;

                        var dx = entity.Position.X - center.X;
                        var dy = entity.Position.Y - center.Y;

                        if (dx * dx + dy * dy <= radiusSq)
                            results.Add(entity);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the nearest entity within radius, or null if none found.
        /// Useful for point defense targeting and missile homing.
        /// </summary>
        public Entity? QueryNearest(Vector2 center, float radius, Func<Entity, bool>? filter = null)
        {
            var radiusSq = radius * radius;
            Entity? nearest = null;
            float nearestDistSq = float.MaxValue;

            int minCellX = WorldToCell(center.X - radius);
            int maxCellX = WorldToCell(center.X + radius);
            int minCellY = WorldToCell(center.Y - radius);
            int maxCellY = WorldToCell(center.Y + radius);

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    var key = MakeCellKey(cx, cy);

                    if (!_cells.TryGetValue(key, out var cell))
                        continue;

                    foreach (var entity in cell)
                    {
                        if (!entity.IsAlive)
                            continue;

                        if (filter != null && !filter(entity))
                            continue;

                        var dx = entity.Position.X - center.X;
                        var dy = entity.Position.Y - center.Y;
                        var distSq = dx * dx + dy * dy;

                        if (distSq <= radiusSq && distSq < nearestDistSq)
                        {
                            nearestDistSq = distSq;
                            nearest = entity;
                        }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Total number of entities currently registered in the grid.
        /// </summary>
        public int Count => _entityCell.Count;

        // ------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------

        private HashSet<Entity> GetOrCreateCell(long key)
        {
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = new HashSet<Entity>();
                _cells[key] = cell;
            }
            return cell;
        }

        private long GetCellKey(Vector2 position)
            => MakeCellKey(WorldToCell(position.X), WorldToCell(position.Y));

        private int WorldToCell(float worldCoord)
            => (int)MathF.Floor(worldCoord * _inverseCellSize);

        /// <summary>
        /// Packs two 32-bit cell coordinates into one 64-bit key.
        /// Avoids string allocations for dictionary lookups.
        /// </summary>
        private static long MakeCellKey(int cx, int cy)
            => ((long)(uint)cx << 32) | (uint)cy;
    }
}
