using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Plans routes between any two points in the universe.
    ///
    /// Two public methods for two callers:
    ///
    ///   EstimateTravelTimeBetweenEntities  → seconds only, entity-keyed, CACHED
    ///   EstimateTravelTime                 → seconds only, position-based, fresh
    ///   PlanRoute                          → full NavigationResult with segments, CACHED
    ///
    /// TradeSystem calls EstimateTravelTimeBetweenEntities thousands of times
    /// per scheduler tick. OrderSystem calls PlanRoute once per job assignment.
    ///
    /// Algorithm: Dijkstra on a unified graph.
    ///   Nodes  — ship/target position + all structure entities
    ///   Edges  — sublight (all-to-all), warp, jump, gate/wormhole/stargate
    ///   Cost   — seconds of travel time
    ///
    /// Cache key: "{fromEntityId}:{toEntityId}:{capabilityKey}"
    /// Capability key encodes the ship's movement profile as a short
    /// deterministic string (e.g. "s3w4", "s2jxsc1") so ships that
    /// would make the same routing decisions share cached results.
    ///
    /// See Core Truths — Navigation System.
    /// </summary>
    public class NavigationSystem
    {
        private readonly UniverseService _universe;

        // ------------------------------------------------------------
        // Route cache — full NavigationResult keyed by entity pair + capability
        // ------------------------------------------------------------

        private readonly Dictionary<string, NavigationResult> _routeCache = new();
        private readonly Queue<string>                        _cacheOrder  = new();
        private const    int                                  MaxCacheSize = 10_000;

        // ------------------------------------------------------------
        // Gate edge cache — rebuilt on invalidation, stable otherwise
        // ------------------------------------------------------------

        private record GateEdge(
            string       FromId,
            string       ToId,
            Vector2      FromPos,
            Vector2      ToPos,
            double       CostSeconds,
            MovementMode Mode);

        private List<GateEdge>? _gateEdgeCache;

        // ------------------------------------------------------------
        // Graph constants
        // ------------------------------------------------------------

        private const float  WarpMinDistance = 1_000f;
        private const float  JumpMinDistance = 5_000f;

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public NavigationSystem(UniverseService universe)
        {
            _universe = universe;
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Estimates travel time between two known static entities.
        /// Result is cached — identical calls return instantly.
        ///
        /// Use for TradeSystem provider→requester leg evaluation.
        /// Both entities must exist in the universe.
        /// </summary>
        public double EstimateTravelTimeBetweenEntities(
            string            fromEntityId,
            string            toEntityId,
            ShipStatsComponent stats)
        {
            var fromEntity = _universe.GetEntity(fromEntityId);
            var toEntity   = _universe.GetEntity(toEntityId);

            if (fromEntity == null || toEntity == null)
                return double.PositiveInfinity;

            string cacheKey = BuildCacheKey(fromEntityId, toEntityId, stats);

            if (_routeCache.TryGetValue(cacheKey, out var cached))
                return cached.TotalSeconds;

            var result = ComputeRoute(
                fromEntity.Position, fromEntityId,
                toEntity.Position,   toEntityId,
                stats);

            StoreInCache(cacheKey, result);
            return result.TotalSeconds;
        }

        /// <summary>
        /// Estimates travel time from an arbitrary position to a target position.
        /// NOT cached — ship position changes every tick.
        ///
        /// Use for TradeSystem ship→provider leg evaluation.
        /// </summary>
        public double EstimateTravelTime(
            Vector2            from,
            Vector2            to,
            ShipStatsComponent stats)
        {
            float dist = Vector2.Distance(from, to);
            if (dist < 0.001f) return 0;

            var result = ComputeRoute(from, null, to, null, stats);
            return result.TotalSeconds;
        }

        /// <summary>
        /// Plans a full route with movement segments from a position to a target.
        /// Returns cached result when both endpoints are known entities.
        ///
        /// Called by OrderSystem when assigning a trade job.
        /// The result is stored on entity.ActiveRoute for MovementSystem to execute.
        /// </summary>
        public NavigationResult PlanRoute(
            Vector2            from,
            Vector2            to,
            ShipStatsComponent stats,
            string?            fromEntityId = null,
            string?            toEntityId   = null)
        {
            float dist = Vector2.Distance(from, to);
            if (dist < 0.001f)
                return NavigationResult.Immediate(from);

            // Check cache when both entity IDs are known
            if (fromEntityId != null && toEntityId != null)
            {
                string cacheKey = BuildCacheKey(fromEntityId, toEntityId, stats);
                if (_routeCache.TryGetValue(cacheKey, out var cached))
                    return cached;

                var result = ComputeRoute(from, fromEntityId, to, toEntityId, stats);
                StoreInCache(cacheKey, result);
                return result;
            }

            return ComputeRoute(from, fromEntityId, to, toEntityId, stats);
        }

        // ------------------------------------------------------------
        // Cache invalidation
        // ------------------------------------------------------------

        /// <summary>
        /// Clears all cached routes that involve a specific entity.
        /// Call when a gate or structure changes position or state.
        /// </summary>
        public void InvalidateRoutesContaining(string entityId)
        {
            var toRemove = _routeCache.Keys
                .Where(k => k.Contains(entityId))
                .ToList();

            foreach (var key in toRemove)
                _routeCache.Remove(key);

            Console.WriteLine(
                $"[Navigation] Invalidated {toRemove.Count} cached routes for entity {entityId}.");
        }

        /// <summary>Flushes the entire route cache.</summary>
        public void InvalidateAllRoutes()
        {
            _routeCache.Clear();
            _cacheOrder.Clear();
            Console.WriteLine("[Navigation] Full route cache cleared.");
        }

        /// <summary>
        /// Rebuilds the gate edge list on next graph construction.
        /// Call when a gate/wormhole/stargate entity is created, destroyed, or toggled.
        /// </summary>
        public void InvalidateGateCache()
        {
            _gateEdgeCache = null;
            Console.WriteLine("[Navigation] Gate edge cache invalidated.");
        }

        // ============================================================
        // CORE COMPUTATION
        // ============================================================

        private NavigationResult ComputeRoute(
            Vector2  from,       string? fromId,
            Vector2  to,         string? toId,
            ShipStatsComponent stats)
        {
            if (!stats.CanMove)
                return NavigationResult.Unreachable();

            // Build the graph
            var (nodes, edges, fromNode, toNode) =
                BuildGraph(from, fromId, to, toId, stats);

            // Run Dijkstra
            var path = Dijkstra(fromNode, toNode, nodes, edges);

            if (path.Count == 0)
            {
                // No path found via graph — fallback to direct sublight if capable
                if (stats.CanSublight && stats.MaxSpeed > 0.001f)
                {
                    float  dist = Vector2.Distance(from, to);
                    double eta  = dist / stats.MaxSpeed;
                    return new NavigationResult
                    {
                        TotalSeconds = eta,
                        Segments = new List<MovementSegment>
                        {
                            new()
                            {
                                Mode            = MovementMode.Sublight,
                                From            = from,
                                To              = to,
                                DurationSeconds = eta
                            }
                        }
                    };
                }

                return NavigationResult.Unreachable();
            }

            return BuildRouteFromPath(path);
        }

        // ============================================================
        // GRAPH CONSTRUCTION
        // ============================================================

        private (List<NavNode>  nodes,
                 List<NavEdge>  edges,
                 NavNode        fromNode,
                 NavNode        toNode)
            BuildGraph(
                Vector2  from, string? fromId,
                Vector2  to,   string? toId,
                ShipStatsComponent stats)
        {
            var nodes = new List<NavNode>();
            var edges = new List<NavEdge>();

            // Core nodes
            var fromNode = new NavNode { Id = fromId ?? "_from", Position = from };
            var toNode   = new NavNode { Id = toId   ?? "_to",   Position = to   };
            nodes.Add(fromNode);
            nodes.Add(toNode);

            // Station/structure nodes — all non-mobile entities
            var structureNodes = new Dictionary<string, NavNode>();

            foreach (var (id, entity) in _universe.Entities)
            {
                if (!entity.IsAlive)     continue;
                if (id == fromId || id == toId) continue;  // already added

                // Skip mobile entities — ships and swarms move, not valid as waypoints
                if (entity.Kind == "ship"  ||
                    entity.Kind == "swarm" ||
                    entity.Kind == "missile") continue;

                var node = new NavNode { Id = id, Position = entity.Position };
                nodes.Add(node);
                structureNodes[id] = node;
            }

            // Helper: add directed edge
            void AddEdge(NavNode a, NavNode b, double cost, MovementMode mode)
            {
                if (a.Id == b.Id || cost < 0) return;
                edges.Add(new NavEdge
                {
                    From        = a,
                    To          = b,
                    CostSeconds = cost,
                    Mode        = mode
                });
            }

            // ---------------------------------------------------------
            // 1. Sublight edges — all-to-all
            // ---------------------------------------------------------
            if (stats.CanSublight && stats.MaxSpeed > 0.001f)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (i == j) continue;

                        float  d   = Vector2.Distance(nodes[i].Position, nodes[j].Position);
                        if (d  < 0.001f) continue;

                        double t = d / stats.MaxSpeed;
                        AddEdge(nodes[i], nodes[j], t, MovementMode.Sublight);
                    }
                }
            }

            // ---------------------------------------------------------
            // 2. Warp edges — all-to-all where distance > WarpMinDistance
            //
            // Cost includes WarpChargeTime once per hop. Without this the
            // scheduler was routing ships through unrelated nearby stations
            // as "warp pivots" — e.g. Iron Mine → Gate Alpha → Iron Smelter
            // when the direct sublight leg (424 units) beat two short warps
            // once charge time was included. See Core Truths §9 — warp has
            // a commitment cost, and the planner has to see it.
            // ---------------------------------------------------------
            if (stats.CanWarp && stats.WarpSpeed > 0.001f)
            {
                double warpOverhead = stats.WarpChargeTime;  // 0 if the drive has no charge time

                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (i == j) continue;

                        float d = Vector2.Distance(nodes[i].Position, nodes[j].Position);
                        if (d < WarpMinDistance) continue;

                        double t = (d / stats.WarpSpeed) + warpOverhead;
                        // Only add warp if faster than sublight for this leg
                        if (stats.CanSublight && t >= d / stats.MaxSpeed) continue;

                        AddEdge(nodes[i], nodes[j], t, MovementMode.Warp);
                    }
                }
            }

            // ---------------------------------------------------------
            // 3. Jump edges — within range, above min distance
            //    Cost = charge time (jump itself is instantaneous)
            //    Compared to direct sublight / warp-with-chargetime so
            //    jump only wins when it's genuinely faster end-to-end.
            // ---------------------------------------------------------
            if (stats.CanJump && stats.JumpRange > 0.001f)
            {
                double chargeCost = stats.JumpChargeTime;

                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (i == j) continue;

                        float d = Vector2.Distance(nodes[i].Position, nodes[j].Position);
                        if (d < JumpMinDistance) continue;
                        if (d > stats.JumpRange)  continue;

                        // Only add jump if faster than sublight/warp for this leg
                        double bestDirect = double.PositiveInfinity;
                        if (stats.CanSublight && stats.MaxSpeed > 0.001f)
                            bestDirect = Math.Min(bestDirect, d / stats.MaxSpeed);
                        if (stats.CanWarp && stats.WarpSpeed > 0.001f && d >= WarpMinDistance)
                            bestDirect = Math.Min(bestDirect,
                                (d / stats.WarpSpeed) + stats.WarpChargeTime);

                        if (chargeCost >= bestDirect) continue;

                        AddEdge(nodes[i], nodes[j], chargeCost, MovementMode.JumpExecuting);
                    }
                }
            }

            // ---------------------------------------------------------
            // 4. Gate/wormhole/stargate edges
            // ---------------------------------------------------------
            foreach (var gate in GetGateEdges())
            {
                NavNode? gFrom = null;
                NavNode? gTo   = null;

                // Resolve from node — might be a structure node or one of our core nodes
                if (gate.FromId == fromNode.Id)      gFrom = fromNode;
                else if (gate.FromId == toNode.Id)   gFrom = toNode;
                else structureNodes.TryGetValue(gate.FromId, out gFrom);

                if (gate.ToId == fromNode.Id)        gTo = fromNode;
                else if (gate.ToId == toNode.Id)     gTo = toNode;
                else structureNodes.TryGetValue(gate.ToId, out gTo);

                if (gFrom != null && gTo != null)
                    AddEdge(gFrom, gTo, gate.CostSeconds, gate.Mode);
            }

            return (nodes, edges, fromNode, toNode);
        }

        // ============================================================
        // DIJKSTRA
        // Uses PriorityQueue<T, TPriority> — available since .NET 6
        // ============================================================

        private List<NavEdge> Dijkstra(
            NavNode        start,
            NavNode        goal,
            List<NavNode>  nodes,
            List<NavEdge>  edges)
        {
            var dist = new Dictionary<string, double>(nodes.Count);
            var prev = new Dictionary<string, NavEdge?>(nodes.Count);

            foreach (var n in nodes)
            {
                dist[n.Id] = double.PositiveInfinity;
                prev[n.Id] = null;
            }

            dist[start.Id] = 0.0;

            // Adjacency list for O(E) edge lookup instead of O(E) scan per node
            var adj = new Dictionary<string, List<NavEdge>>(nodes.Count);
            foreach (var n in nodes) adj[n.Id] = new List<NavEdge>();
            foreach (var e in edges)
            {
                if (adj.ContainsKey(e.From.Id))
                    adj[e.From.Id].Add(e);
            }

            var pq = new PriorityQueue<NavNode, double>();
            pq.Enqueue(start, 0.0);

            while (pq.Count > 0)
            {
                pq.TryDequeue(out var current, out var currentCost);
                if (current == null) break;
                if (current.Id == goal.Id) break;

                // Skip stale entries (node was relaxed after enqueue)
                if (currentCost > dist[current.Id]) continue;

                foreach (var edge in adj[current.Id])
                {
                    double alt = dist[current.Id] + edge.CostSeconds;
                    if (alt < dist[edge.To.Id])
                    {
                        dist[edge.To.Id] = alt;
                        prev[edge.To.Id] = edge;
                        pq.Enqueue(edge.To, alt);
                    }
                }
            }

            if (double.IsInfinity(dist[goal.Id]))
                return new List<NavEdge>();

            // Reconstruct path
            var path = new List<NavEdge>();
            var node = goal;

            while (node.Id != start.Id)
            {
                var edge = prev[node.Id];
                if (edge == null) break;
                path.Add(edge);
                node = edge.From;
            }

            path.Reverse();
            return path;
        }

        // ============================================================
        // PATH → MOVEMENT SEGMENTS
        // ============================================================

        private static NavigationResult BuildRouteFromPath(List<NavEdge> path)
        {
            var segments = new List<MovementSegment>();

            foreach (var edge in path)
            {
                if (edge.Mode == MovementMode.JumpExecuting)
                {
                    // Expand into charge + execute pair
                    segments.Add(new MovementSegment
                    {
                        Mode            = MovementMode.JumpCharging,
                        From            = edge.From.Position,
                        To              = edge.From.Position,   // hold position during charge
                        DurationSeconds = edge.CostSeconds
                    });
                    segments.Add(new MovementSegment
                    {
                        Mode            = MovementMode.JumpExecuting,
                        From            = edge.From.Position,
                        To              = edge.To.Position,
                        DurationSeconds = 0
                    });
                }
                else
                {
                    segments.Add(new MovementSegment
                    {
                        Mode            = edge.Mode,
                        From            = edge.From.Position,
                        To              = edge.To.Position,
                        DurationSeconds = edge.CostSeconds
                    });
                }
            }

            double total = segments.Sum(s => s.DurationSeconds);
            return new NavigationResult { TotalSeconds = total, Segments = segments };
        }

        // ============================================================
        // GATE EDGE CACHE
        // ============================================================

        private IReadOnlyList<GateEdge> GetGateEdges()
        {
            if (_gateEdgeCache != null) return _gateEdgeCache;

            var edges = new List<GateEdge>();

            foreach (var (id, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                // JumpGate — bidirectional pair
                var gate = entity.GetComponent<JumpGateComponent>();
                if (gate is { IsOperational: true } && gate.LinkedGateEntityId != null)
                {
                    var linked = _universe.GetEntity(gate.LinkedGateEntityId);
                    if (linked != null)
                    {
                        edges.Add(new GateEdge(
                            id, gate.LinkedGateEntityId,
                            entity.Position, linked.Position,
                            0.0, MovementMode.GateTransfer));

                        edges.Add(new GateEdge(
                            gate.LinkedGateEntityId, id,
                            linked.Position, entity.Position,
                            0.0, MovementMode.GateTransfer));
                    }
                }

                // Wormhole — bidirectional, slight risk penalty
                var wh = entity.GetComponent<WormholeComponent>();
                if (wh is { IsStable: true } && wh.LinkedWormholeEntityId != null)
                {
                    var linked = _universe.GetEntity(wh.LinkedWormholeEntityId);
                    if (linked != null)
                    {
                        double cost = 0.0 * wh.StabilityPenaltyMultiplier; // 0 * penalty = 0, placeholder for future mechanics
                        edges.Add(new GateEdge(
                            id, wh.LinkedWormholeEntityId,
                            entity.Position, linked.Position,
                            cost, MovementMode.WormholeTransfer));

                        edges.Add(new GateEdge(
                            wh.LinkedWormholeEntityId, id,
                            linked.Position, entity.Position,
                            cost, MovementMode.WormholeTransfer));
                    }
                }

                // Stargate — directed network connections
                var sg = entity.GetComponent<StargateComponent>();
                if (sg is { IsOperational: true })
                {
                    foreach (var destId in sg.NetworkGateEntityIds)
                    {
                        var dest = _universe.GetEntity(destId);
                        if (dest != null)
                        {
                            edges.Add(new GateEdge(
                                id, destId,
                                entity.Position, dest.Position,
                                0.0, MovementMode.StargateTransfer));
                        }
                    }
                }
            }

            _gateEdgeCache = edges;
            return _gateEdgeCache;
        }

        // ============================================================
        // CAPABILITY PROFILE KEY
        // ============================================================

        /// <summary>
        /// Produces a short deterministic string from a ship's movement stats.
        /// Ships that would make the same routing decisions share the same key
        /// and thus share cached route results.
        ///
        /// Format:  s{speed_bucket}[w{warp_bucket}][j{range_bucket}c{charge_bucket}]
        /// Example: "s3"        → sublight-only, moderate speed
        ///          "s3w4"      → sublight + warp
        ///          "s2jxsc1"   → lurch drive: slow sublight, extreme-short jump, fast charge
        ///          "s3w4jmc3"  → full-capability fleet vessel
        ///          "none"      → no movement capability
        /// </summary>
        private static string GetCapabilityKey(ShipStatsComponent stats)
        {
            var sb = new StringBuilder(16);

            if (stats.CanSublight)
                sb.Append('s').Append(BucketSpeed(stats.MaxSpeed));

            if (stats.CanWarp)
                sb.Append('w').Append(BucketSpeed(stats.WarpSpeed))
                  .Append('c').Append(BucketCharge(stats.WarpChargeTime));

            if (stats.CanJump)
                sb.Append('j').Append(BucketRange(stats.JumpRange))
                  .Append('c').Append(BucketCharge(stats.JumpChargeTime));

            return sb.Length > 0 ? sb.ToString() : "none";
        }

        /// <summary>
        /// Speed bucket — affects cost of sublight and warp legs.
        /// Coarse enough that minor speed variations share the same bucket.
        /// Granularity here determines how many distinct cache entries exist
        /// per entity pair per drive type.
        /// </summary>
        private static string BucketSpeed(float speed) => speed switch
        {
            < 30f  => "1",   // slow — deep haulers, capital ships
            < 60f  => "2",   // moderate — standard freighters
            < 120f => "3",   // fast — courier class
            < 250f => "4",   // very fast — interceptors
            _      => "5"    // extreme — experimental drives
        };

        /// <summary>
        /// Jump range bucket — affects graph topology (which jump edges exist).
        /// This must be granular enough that ships with meaningfully different
        /// ranges don't share routes that one of them can't actually fly.
        /// </summary>
        private static string BucketRange(float range) => range switch
        {
            < 10_000f  => "xs",  // extreme-short — lurch drive, short hops only
            < 25_000f  => "s",   // short range
            < 75_000f  => "m",   // medium range — standard jump frigate
            < 200_000f => "l",   // long range
            _          => "xl"   // extreme — capital jump drives
        };

        /// <summary>
        /// Jump charge time bucket — affects whether jumping is worth it
        /// vs sublight/warp for short distances.
        ///
        /// A fast-charge drive (lurch drive: 5s) makes short jumps attractive.
        /// A slow-charge drive (capital: 300s) only makes sense for very long legs.
        /// Two ships with the same range but very different charge times
        /// will choose different optimal routes.
        /// </summary>
        private static string BucketCharge(float seconds) => seconds switch
        {
            < 30f  => "1",   // fast charge — jump eagerly even for short legs
            < 120f => "2",   // moderate — jump for medium+ legs
            < 300f => "3",   // slow — only jump for long legs
            _      => "4"    // very slow — avoid jumping, prefer warp/sublight
        };

        // ============================================================
        // CACHE MANAGEMENT
        // ============================================================

        private static string BuildCacheKey(
            string fromId, string toId, ShipStatsComponent stats)
        {
            return $"{fromId}:{toId}:{GetCapabilityKey(stats)}";
        }

        private void StoreInCache(string key, NavigationResult result)
        {
            if (_routeCache.ContainsKey(key)) return;

            // Evict oldest entry if at capacity
            while (_routeCache.Count >= MaxCacheSize && _cacheOrder.Count > 0)
            {
                var oldest = _cacheOrder.Dequeue();
                _routeCache.Remove(oldest);
            }

            _routeCache[key] = result;
            _cacheOrder.Enqueue(key);
        }

        // ============================================================
        // INTERNAL GRAPH TYPES
        // ============================================================

        private class NavNode
        {
            public string  Id       { get; set; } = "";
            public Vector2 Position { get; set; }
        }

        private class NavEdge
        {
            public NavNode      From        { get; set; } = null!;
            public NavNode      To          { get; set; } = null!;
            public double       CostSeconds { get; set; }
            public MovementMode Mode        { get; set; }
        }
    }
}
