using System.Collections.Generic;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Marks a station entity as a jump gate.
    /// Paired with another entity's JumpGateComponent to form a two-way link.
    ///
    /// NavigationSystem reads this when building its gate edge cache.
    /// A jump gate provides a zero-cost traversal edge between the two paired entities.
    ///
    /// Stub — full gate interaction logic (approach, alignment, queue management)
    /// is added when the first gate network is designed.
    /// </summary>
    public class JumpGateComponent : ComponentBase
    {
        public override string Name => "jump_gate";

        /// <summary>Entity Id of the paired gate at the other end.</summary>
        public string? LinkedGateEntityId { get; set; }

        /// <summary>
        /// Whether this gate is currently accepting transit.
        /// A disabled gate is excluded from the NavigationSystem graph.
        /// Setting this to false triggers a gate edge cache invalidation.
        /// </summary>
        public bool IsOperational { get; set; } = true;

        public override void Tick(Entity owner, double deltaTime) { }
    }

    /// <summary>
    /// Marks a station entity as a wormhole transit point.
    /// Paired with another entity's WormholeComponent.
    ///
    /// Unlike jump gates, wormholes have stability — an unstable wormhole
    /// adds a risk penalty to its traversal cost. A collapsed wormhole
    /// is excluded from the graph entirely.
    ///
    /// Stub — collapse mechanics and discovery are future features.
    /// </summary>
    public class WormholeComponent : ComponentBase
    {
        public override string Name => "wormhole";

        /// <summary>Entity Id of the wormhole exit point.</summary>
        public string? LinkedWormholeEntityId { get; set; }

        /// <summary>
        /// Whether this wormhole is currently passable.
        /// A collapsed wormhole is excluded from the navigation graph.
        /// </summary>
        public bool IsStable { get; set; } = true;

        /// <summary>
        /// Risk multiplier applied to the traversal cost.
        /// 1.0 = no penalty. 1.1 = 10% cost penalty (represents navigational risk).
        /// Higher values make wormhole routes less attractive than gate routes.
        /// </summary>
        public float StabilityPenaltyMultiplier { get; set; } = 1.1f;

        public override void Tick(Entity owner, double deltaTime) { }
    }

    /// <summary>
    /// Marks a station entity as a stargate — part of a named network.
    /// Unlike jump gates (point-to-point), stargates can connect to
    /// multiple destinations within the same network.
    ///
    /// All listed network gate entity Ids get a zero-cost directed edge
    /// in the navigation graph.
    ///
    /// Stub — network membership, access control, and gate construction
    /// are future features.
    /// </summary>
    public class StargateComponent : ComponentBase
    {
        public override string Name => "stargate";

        /// <summary>
        /// Entity Ids of all connected gates in this stargate network.
        /// Each connection gets a directed zero-cost edge in the navigation graph.
        /// </summary>
        public List<string> NetworkGateEntityIds { get; set; } = new();

        public bool IsOperational { get; set; } = true;

        public override void Tick(Entity owner, double deltaTime) { }
    }
}
