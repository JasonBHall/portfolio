using System.Numerics;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Marks an entity as eligible for trade contract assignment.
    ///
    /// Only entities with this component are visible to the TradeSystem
    /// scheduler. This eliminates full-universe iteration — the scheduler
    /// scans only the logistics pool, which is a small fraction of entities.
    ///
    /// CurrentJobId as the busy flag:
    ///   null     → idle, available for a new contract
    ///   non-null → executing a job, skip for new assignments
    ///
    /// No separate IsBusy boolean needed. The job ID is the flag, and
    /// it resolves to the full TradeJob for UI and reporting purposes.
    ///
    /// Mid-mission behaviour:
    ///   If AcceptsTradeContracts is set to false while a ship is executing
    ///   a job, the ship completes its current run. Cargo in transit is
    ///   never abandoned. On completion CurrentJobId clears, the ship does
    ///   not re-enter the pool, and moves to its rally point if set.
    ///
    /// See Core Truths — Economy System, Trade Eligibility.
    /// </summary>
    public class LogisticsComponent : ComponentBase
    {
        public override string Name => "logistics";

        // ------------------------------------------------------------
        // Trade eligibility
        // ------------------------------------------------------------

        /// <summary>
        /// Whether this entity should be considered for trade contracts.
        /// Can be toggled at any time. Mid-mission changes take effect
        /// after the current job completes.
        /// </summary>
        public bool AcceptsTradeContracts { get; set; } = false;

        // ------------------------------------------------------------
        // Current job — the busy flag
        // ------------------------------------------------------------

        /// <summary>
        /// Id of the TradeJob this entity is currently executing.
        /// null  = idle and available.
        /// non-null = busy, skip for new assignments.
        ///
        /// Set by TradeSystem when a job is awarded.
        /// Cleared by OrderSystem when the final delivery step completes.
        /// </summary>
        public string? CurrentJobId { get; set; }

        /// <summary>
        /// Human-readable summary of the current job for UI display.
        /// e.g. "Delivering 5000 ironOre → Iron Smelter 1"
        /// Set alongside CurrentJobId. Cleared when job clears.
        /// </summary>
        public string? CurrentJobSummary { get; set; }

        // ------------------------------------------------------------
        // Availability — used by TradeSystem scheduler
        // ------------------------------------------------------------

        /// <summary>
        /// True when this entity is in neutral state and ready for a new contract.
        /// Identical condition at game start and after any job completes.
        /// The game loop finds entities in this state — no special coordination needed.
        /// </summary>
        public bool IsAvailable =>
            AcceptsTradeContracts && CurrentJobId == null;

        // ------------------------------------------------------------
        // Rally point — defines idle behaviour after job completion
        // ------------------------------------------------------------

        /// <summary>
        /// Entity to return to after completing a job (fleet carrier,
        /// tender dock). Tracks the entity even if it moves.
        /// Takes priority over RallyPointPosition if both are set.
        /// </summary>
        public string? RallyPointEntityId { get; set; }

        /// <summary>
        /// Fixed coordinate to hold at after completing a job.
        /// Used when there is no entity to track.
        /// </summary>
        public Vector2? RallyPointPosition { get; set; }

        /// <summary>Whether this entity has any rally point defined.</summary>
        public bool HasRallyPoint =>
            RallyPointEntityId != null || RallyPointPosition.HasValue;

        /// <summary>
        /// Remaining macro ticks of dwell time before the entity departs.
        /// Set by OrderSystem during load/unload: 1 tick per stack transferred.
        /// Entity stays at the station until this reaches 0.
        /// This means larger loads take proportionally longer — a 50-slot
        /// freighter loading full takes 50 macro ticks (~4 minutes) to complete.
        /// </summary>
        public int DwellTicksRemaining { get; set; }

        public bool IsDwelling => DwellTicksRemaining > 0;

        // ------------------------------------------------------------
        // Job management — called by TradeSystem and OrderSystem
        // ------------------------------------------------------------

        /// <summary>
        /// Assigns a trade job. Called by TradeSystem when a contract is awarded.
        /// </summary>
        public void AssignJob(string jobId, string summary)
        {
            CurrentJobId      = jobId;
            CurrentJobSummary = summary;
        }

        /// <summary>
        /// Resets to neutral state. Called by OrderSystem.CompleteOrder.
        /// The game loop finds the entity on the next pass — no coordination needed.
        /// </summary>
        public void ClearJob()
        {
            CurrentJobId          = null;
            CurrentJobSummary     = null;
            DwellTicksRemaining   = 0;
        }

        // ------------------------------------------------------------
        // Tick — no passive behaviour
        // ------------------------------------------------------------

        public override void Tick(Entity owner, double deltaTime) { }
    }
}
