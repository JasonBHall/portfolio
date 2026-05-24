using System;

namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Marker component — identifies an entity as an in-flight projectile.
    ///
    /// Spawned by WeaponModule.Fire when a WeaponModule has WantsToFire set.
    /// A projectile is a bare entity with Velocity and no ShipStatsComponent —
    /// MovementSystem drifts it along its velocity vector each tick
    /// (engineless entities branch in MovementSystem.Tick).
    ///
    /// Lifetime — simple spawn-time + max-seconds check. ProjectileSystem
    /// marks the entity dead when exceeded; CleanupDeadEntities reaps it
    /// on the next macro tick.
    ///
    /// Deliberate scaffolding — the LifetimeComponent / LifetimeSystem in
    /// the backlog will subsume this with range + fuel expiry and
    /// return-to-carrier behaviour. For now projectiles just expire.
    ///
    /// No damage logic here — hit detection and target effects land with
    /// the combat systems proper.
    /// </summary>
    public class ProjectileComponent : ComponentBase
    {
        public override string Name => "projectile";

        /// <summary>Entity id that fired this projectile.</summary>
        public string OwnerId { get; set; } = "";

        /// <summary>
        /// Target entity id at fire-time, if the owner had a lock.
        /// Informational — no guidance applied yet.
        /// </summary>
        public string? TargetId { get; set; }

        /// <summary>UTC timestamp when the projectile was spawned.</summary>
        public DateTime SpawnedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Seconds before ProjectileSystem reaps it.
        /// Configurable per-weapon via WeaponModule.ProjectileLifetimeSeconds.
        /// </summary>
        public float LifetimeSeconds { get; set; } = 3f;

        public override void Tick(Entity owner, double deltaTime) { }
    }
}
