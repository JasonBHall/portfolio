using System;
using System.Linq;
using System.Numerics;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Drives weapon firing each micro tick.
    ///
    /// Iterates every entity, finds installed WeaponModules with
    /// WantsToFire = true, resolves the firing entity's target (from
    /// PlayerControlledComponent on owner), and spawns the projectile.
    ///
    /// This is where Fire() lives — not on WeaponModule itself — because
    /// projectile creation needs UniverseService, which Shared cannot
    /// reference. WeaponSystem lives in Server and has access to both.
    ///
    /// Clean ECS layering: CommandHandler sets a flag. WeaponSystem reads
    /// the flag and does the work. Components hold data; systems do.
    /// </summary>
    public class WeaponSystem
    {
        private readonly UniverseService _universe;

        public WeaponSystem(UniverseService universe)
        {
            _universe = universe;
        }

        public void Tick(double deltaTime)
        {
            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                var weapons = entity.GetAllComponents()
                                    .OfType<WeaponModule>()
                                    .Where(w => w.WantsToFire && w.IsOperational)
                                    .ToList();

                if (weapons.Count == 0) continue;

                // Resolve target once per entity — all weapons share the lock.
                var pc     = entity.GetComponent<PlayerControlledComponent>();
                var target = pc?.CurrentTargetId != null
                                ? _universe.GetEntity(pc.CurrentTargetId)
                                : null;

                foreach (var w in weapons)
                {
                    Fire(entity, w, target);
                    w.WantsToFire = false;
                }
            }
        }

        // ------------------------------------------------------------
        // Fire — spawns the projectile entity
        //
        // Fire direction:
        //   - If owner has a valid target within weapon range,
        //     aim directly at the target.
        //   - Otherwise, fire straight ahead along owner's heading.
        // ------------------------------------------------------------

        private void Fire(Entity owner, WeaponModule weapon, Entity? target)
        {
            Vector2 fireHeading = owner.Heading;

            if (target != null && target.IsAlive)
            {
                var toTarget = target.Position - owner.Position;
                var dist     = toTarget.Length();

                if (dist <= weapon.Range && dist > 0.0001f)
                    fireHeading = Vector2.Normalize(toTarget);
            }

            // Spawn slightly ahead of owner so the shot visibly leaves the bow.
            var spawnPos = owner.Position + fireHeading * 6f;
            var shotVel  = owner.Velocity + fireHeading * weapon.MuzzleSpeed;

            var shot = _universe.CreateEntity(
                name:     $"{weapon.ProjectileKind}-{owner.Name}",
                kind:     weapon.ProjectileKind,
                position: spawnPos
            );

            shot.Velocity = shotVel;
            shot.Heading  = fireHeading;

            shot.Components.Add(new ProjectileComponent
            {
                OwnerId         = owner.Id,
                TargetId        = target?.Id,
                SpawnedAtUtc    = DateTime.UtcNow,
                LifetimeSeconds = weapon.ProjectileLifetimeSeconds
            });

            shot.MarkDirty();

            owner.Log.Event("Weapon",
                $"{weapon.DisplayName} fired" +
                (target != null ? $" at {target.Name}" : " (no lock)"));
        }
    }
}
