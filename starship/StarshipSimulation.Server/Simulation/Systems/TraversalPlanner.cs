// StarshipSimulation.Server/Simulation/Systems/TraversalPlanner.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Builds TraversalPlans for MoveTo legs.
    ///
    /// ── Shape Constants Cache ──────────────────────────────────────────────
    /// The arc shape is always the same for a given engine × profile combination.
    /// ShapeConstants are precomputed once and cached, keyed by a bucketed string
    /// derived from thrust/speed/turnRate and profile name. This is the Doom
    /// lookup table pattern — the curve shape is fixed, only the distance (coast
    /// duration) varies per trip.
    ///
    /// ── Dot Product Retask ────────────────────────────────────────────────
    /// When a ship is retasked mid-flight, the existing velocity is evaluated
    /// against the new destination direction via dot product:
    ///
    ///   dot > 0.7  → complementary — project velocity onto destination,
    ///                use that as free initial speed (shorter accel phase)
    ///   0.3–0.7   → partial — keep aligned component, brake the rest
    ///   dot < 0.3  → opposing/perpendicular — full braking phase first
    ///
    /// Future: maneuver thrusters bleed lateral velocity as part of the
    /// partial retask calculation.
    ///
    /// ── Warp / Jump / Gate ───────────────────────────────────────────────
    /// These modes suspend Newtonian motion and translate the entity to a new
    /// position. Gate/wormhole/stargate always require a full stop first.
    /// Military profile BrakeOnArrival=false is overridden to true for these.
    ///
    /// ── Profiles ─────────────────────────────────────────────────────────
    /// All four profiles (Economy/Standard/Speed/Military) share the same arc
    /// math — they differ only in ThrustFraction, SpeedFraction, and whether
    /// the braking phase is included. Economy produces a smaller, quieter arc.
    /// Military arrives at cruise speed with velocity intact.
    ///
    /// See Core Truths — Traversal Plan, Movement Profiles.
    /// </summary>
    public static class TraversalPlanner
    {
        // ── Constants ──────────────────────────────────────────────────────

        private const float MinTripDistance    = 0.5f;
        private const float ComplementaryDot   = 0.7f;   // above = use free velocity
        private const float PartialDot         = 0.3f;   // below = full braking

        // ── Shape constants cache ─────────────────────────────────────────
        // Key: bucketed thrust:speed:turnRate:profileName
        // Value: precomputed arc geometry that is constant for this engine × profile

        private static readonly Dictionary<string, ShapeConstants> _shapeCache = new();

        private record ShapeConstants(
            float EffectiveThrust,   // thrust × profile.ThrustFraction
            float CruiseSpeed,       // maxSpeed × profile.SpeedFraction
            float AccelTime,         // time from 0 to cruise speed
            float AccelDist,         // distance during accel phase
            float FlipTime,          // time to rotate 180°
            float FlipDist,          // distance during flip (still moving forward)
            float MinFullTripDist    // below this, use short-trip plan
        );

        // ── Public entry point ────────────────────────────────────────────

        /// <summary>
        /// Builds a traversal plan from <paramref name="from"/> to <paramref name="to"/>.
        ///
        /// <paramref name="currentVelocity"/> — ship's current velocity. When non-zero,
        /// the dot product against the destination direction determines whether to
        /// prepend a braking phase, use the velocity as a head start, or bleed the
        /// perpendicular component.
        ///
        /// <paramref name="profile"/> — engine usage profile. Defaults to Standard.
        /// Economy reduces thrust and speed. Military skips the braking phase.
        ///
        /// <paramref name="forceBrakeOnArrival"/> — override BrakeOnArrival to true.
        /// Used for gate/wormhole/stargate transit which always require a full stop.
        /// </summary>
        public static TraversalPlan? Build(
            Vector2            from,
            Vector2            to,
            ShipStatsComponent stats,
            Vector2            currentVelocity  = default,
            MovementProfile?   profile          = null,
            bool               forceBrakeOnArrival = false)
        {
            profile ??= MovementProfile.Standard;

            // Auto-downgrade to economy on low fuel
            profile = ApplyFuelConservation(stats, profile);

            float distance = Vector2.Distance(from, to);
            if (distance < MinTripDistance) return null;

            var shape = GetShapeConstants(stats, profile);

            bool brakeOnArrival = forceBrakeOnArrival || profile.BrakeOnArrival;
            var  dir            = Vector2.Normalize(to - from);
            var  retrograde     = -dir;
            float currentSpeed  = currentVelocity.Length();

            // ── Dot product retask evaluation ─────────────────────────────
            float effectiveStartSpeed = 0f;

            if (currentSpeed > 1f)
            {
                float dot = Vector2.Dot(
                    Vector2.Normalize(currentVelocity),
                    dir);

                if (dot >= ComplementaryDot)
                {
                    // Complementary vector — project existing speed onto destination.
                    // This becomes our free head-start speed, shortening the accel phase.
                    effectiveStartSpeed = currentSpeed * dot;
                }
                else if (dot >= PartialDot)
                {
                    // Partial alignment — keep the aligned component, brake the rest.
                    // Future: maneuver thrusters bleed the perpendicular component here.
                    effectiveStartSpeed = currentSpeed * Math.Max(0f, dot);
                    float lateralSpeed  = currentSpeed * (float)Math.Sqrt(1f - dot * dot);

                    // For now, add a short braking phase to shed lateral velocity.
                    // Lateral bleed time ≈ lateral_speed / (thrust × 0.5)  (partial thruster authority)
                    float lateralBrakeTime = lateralSpeed / Math.Max(shape.EffectiveThrust * 0.5f, 0.001f);
                    if (lateralBrakeTime > 0.1f)
                    {
                        return BuildWithLateralBrake(
                            from, to, dir, retrograde, distance,
                            shape, currentVelocity, effectiveStartSpeed,
                            lateralBrakeTime, brakeOnArrival, profile);
                    }
                }
                else
                {
                    // Opposing or perpendicular — full braking phase first.
                    return BuildWithEmergencyBrake(
                        from, to, stats, dir,
                        currentVelocity, currentSpeed, shape,
                        brakeOnArrival, profile);
                }
            }

            // Build plan with effective start speed (0 if stationary, or head-start if complementary)
            return BuildFromRest(from, to, dir, retrograde, distance,
                                 shape, effectiveStartSpeed, brakeOnArrival);
        }

        // ── Main plan builder ─────────────────────────────────────────────

        private static TraversalPlan BuildFromRest(
            Vector2       from,
            Vector2       to,
            Vector2       dir,
            Vector2       retrograde,
            float         distance,
            ShapeConstants shape,
            float         startSpeed,
            bool          brakeOnArrival)
        {
            float cruise      = shape.CruiseSpeed;
            float thrust      = shape.EffectiveThrust;

            // Adjust accel phase if we already have some speed
            float accelSpeed  = Math.Max(0f, cruise - startSpeed);
            float accelTime   = accelSpeed / thrust;
            float accelDist   = startSpeed * accelTime + 0.5f * thrust * accelTime * accelTime;

            float brakingTime = brakeOnArrival ? shape.AccelTime  : 0f;
            float brakingDist = brakeOnArrival ? shape.AccelDist  : 0f;
            float nonCoast    = accelDist + shape.FlipDist + brakingDist;

            if (nonCoast >= distance)
                return BuildShortTripPlan(from, to, dir, retrograde, distance,
                                          thrust, shape.FlipTime, cruise,
                                          startSpeed, brakeOnArrival);

            float coastDist = distance - nonCoast;
            float coastTime = coastDist / cruise;

            var phases  = new List<TraversalPhase>();
            double cursor = 0.0;

            // Phase 1 — Accel (possibly shortened by head-start speed)
            var accelEnd = from + dir * accelDist;
            phases.Add(new TraversalPhase
            {
                Type         = TraversalPhaseType.Accel,
                StartTime    = cursor,
                Duration     = accelTime,
                StartPos     = from,
                EndPos       = accelEnd,
                StartVel     = dir * startSpeed,
                EndVel       = dir * cruise,
                StartHeading = dir,
                EndHeading   = dir,
            });
            cursor += accelTime;

            // Phase 2 — Coast
            var coastEnd = accelEnd + dir * coastDist;
            phases.Add(new TraversalPhase
            {
                Type         = TraversalPhaseType.Coast,
                StartTime    = cursor,
                Duration     = coastTime,
                StartPos     = accelEnd,
                EndPos       = coastEnd,
                StartVel     = dir * cruise,
                EndVel       = dir * cruise,
                StartHeading = dir,
                EndHeading   = dir,
            });
            cursor += coastTime;

            // Phase 3 — Flip
            var flipEnd = coastEnd + dir * shape.FlipDist;
            phases.Add(new TraversalPhase
            {
                Type         = TraversalPhaseType.Flip,
                StartTime    = cursor,
                Duration     = shape.FlipTime,
                StartPos     = coastEnd,
                EndPos       = flipEnd,
                StartVel     = dir * cruise,
                EndVel       = dir * cruise,
                StartHeading = dir,
                EndHeading   = retrograde,
            });
            cursor += shape.FlipTime;

            // Phase 4 — Braking (or coast-to-target for military)
            if (brakeOnArrival)
            {
                phases.Add(new TraversalPhase
                {
                    Type         = TraversalPhaseType.Braking,
                    StartTime    = cursor,
                    Duration     = brakingTime,
                    StartPos     = flipEnd,
                    EndPos       = to,
                    StartVel     = dir * cruise,
                    EndVel       = Vector2.Zero,
                    StartHeading = retrograde,
                    EndHeading   = retrograde,
                });
                cursor += brakingTime;
            }
            else
            {
                // Military — arrive at cruise speed, no braking
                // Remaining distance covered at cruise after flip
                float militaryCoastDist = Vector2.Distance(flipEnd, to);
                float militaryCoastTime = militaryCoastDist / cruise;
                phases.Add(new TraversalPhase
                {
                    Type         = TraversalPhaseType.Coast,
                    StartTime    = cursor,
                    Duration     = militaryCoastTime,
                    StartPos     = flipEnd,
                    EndPos       = to,
                    StartVel     = dir * cruise,
                    EndVel       = dir * cruise,
                    StartHeading = dir,   // reorient forward after flip for combat
                    EndHeading   = dir,
                });
                cursor += militaryCoastTime;
            }

            return new TraversalPlan
            {
                StartedAt    = DateTime.UtcNow,
                Phases       = phases,
                TotalSeconds = cursor,
                Destination  = to,
            };
        }

        // ── Retask: opposing/perpendicular — full brake first ─────────────

        private static TraversalPlan BuildWithEmergencyBrake(
            Vector2        from,
            Vector2        to,
            ShipStatsComponent stats,
            Vector2        newDir,
            Vector2        currentVelocity,
            float          currentSpeed,
            ShapeConstants shape,
            bool           brakeOnArrival,
            MovementProfile profile)
        {
            float thrust      = shape.EffectiveThrust;
            var   velDir      = Vector2.Normalize(currentVelocity);
            float decelTime   = currentSpeed / thrust;
            float decelDist   = currentSpeed * currentSpeed / (2f * thrust);
            var   stoppedPos  = from + velDir * decelDist;

            // Build main plan from stopped position
            var mainPlan = Build(stoppedPos, to, stats,
                                 Vector2.Zero, profile, !profile.BrakeOnArrival ? false : brakeOnArrival);

            var brakingPhase = new TraversalPhase
            {
                Type         = TraversalPhaseType.Braking,
                StartTime    = 0,
                Duration     = decelTime,
                StartPos     = from,
                EndPos       = stoppedPos,
                StartVel     = currentVelocity,
                EndVel       = Vector2.Zero,
                StartHeading = -velDir,   // snap retrograde
                EndHeading   = -velDir,
            };

            if (mainPlan == null)
            {
                return new TraversalPlan
                {
                    StartedAt    = DateTime.UtcNow,
                    Phases       = new List<TraversalPhase> { brakingPhase },
                    TotalSeconds = decelTime,
                    Destination  = to,
                };
            }

            var allPhases = new List<TraversalPhase> { brakingPhase };
            foreach (var p in mainPlan.Phases)
            {
                allPhases.Add(new TraversalPhase
                {
                    Type         = p.Type,
                    StartTime    = p.StartTime + decelTime,
                    Duration     = p.Duration,
                    StartPos     = p.StartPos,
                    EndPos       = p.EndPos,
                    StartVel     = p.StartVel,
                    EndVel       = p.EndVel,
                    StartHeading = p.StartHeading,
                    EndHeading   = p.EndHeading,
                });
            }

            return new TraversalPlan
            {
                StartedAt    = DateTime.UtcNow,
                Phases       = allPhases,
                TotalSeconds = decelTime + mainPlan.TotalSeconds,
                Destination  = to,
            };
        }

        // ── Retask: partial alignment — lateral bleed ─────────────────────

        private static TraversalPlan BuildWithLateralBrake(
            Vector2        from,
            Vector2        to,
            Vector2        dir,
            Vector2        retrograde,
            float          distance,
            ShapeConstants shape,
            Vector2        currentVelocity,
            float          alignedSpeed,
            float          lateralBrakeTime,
            bool           brakeOnArrival,
            MovementProfile profile)
        {
            // Short lateral correction phase, then proceed with aligned speed
            float   bleedDist   = alignedSpeed * lateralBrakeTime;
            Vector2 bleedEndPos = from + dir * bleedDist;

            var lateralPhase = new TraversalPhase
            {
                Type         = TraversalPhaseType.Braking,
                StartTime    = 0,
                Duration     = lateralBrakeTime,
                StartPos     = from,
                EndPos       = bleedEndPos,
                StartVel     = currentVelocity,
                EndVel       = dir * alignedSpeed,
                StartHeading = dir,
                EndHeading   = dir,
            };

            float remainingDist = Vector2.Distance(bleedEndPos, to);
            var   mainPlan = BuildFromRest(
                bleedEndPos, to, dir, retrograde, remainingDist,
                shape, alignedSpeed, brakeOnArrival);

            var allPhases = new List<TraversalPhase> { lateralPhase };
            foreach (var p in mainPlan.Phases)
            {
                allPhases.Add(new TraversalPhase
                {
                    Type         = p.Type,
                    StartTime    = p.StartTime + lateralBrakeTime,
                    Duration     = p.Duration,
                    StartPos     = p.StartPos,
                    EndPos       = p.EndPos,
                    StartVel     = p.StartVel,
                    EndVel       = p.EndVel,
                    StartHeading = p.StartHeading,
                    EndHeading   = p.EndHeading,
                });
            }

            return new TraversalPlan
            {
                StartedAt    = DateTime.UtcNow,
                Phases       = allPhases,
                TotalSeconds = lateralBrakeTime + mainPlan.TotalSeconds,
                Destination  = to,
            };
        }

        // ── Short trip: never reaches cruise speed ────────────────────────

        private static TraversalPlan BuildShortTripPlan(
            Vector2 from,
            Vector2 to,
            Vector2 dir,
            Vector2 retrograde,
            float   distance,
            float   thrust,
            float   flipTime,
            float   cruiseSpeed,
            float   startSpeed,
            bool    brakeOnArrival)
        {
            // Compute peak speed reachable given distance
            // total_dist = 2 × 0.5 × a × t²  →  t = sqrt(dist/a)
            float usable      = distance * 0.5f;
            float halfTime    = MathF.Sqrt(2f * usable / thrust);
            float peakSpeed   = Math.Min(startSpeed + thrust * halfTime, cruiseSpeed);

            var phases  = new List<TraversalPhase>();
            double cursor = 0.0;
            var midpoint  = from + dir * (distance * 0.5f);

            phases.Add(new TraversalPhase
            {
                Type = TraversalPhaseType.Accel, StartTime = cursor, Duration = halfTime,
                StartPos = from,     EndPos = midpoint,
                StartVel = dir * startSpeed, EndVel = dir * peakSpeed,
                StartHeading = dir,  EndHeading = dir,
            });
            cursor += halfTime;

            float flipActual = Math.Min(flipTime, halfTime);
            var   flipEnd    = midpoint + dir * (peakSpeed * flipActual * 0.5f);
            phases.Add(new TraversalPhase
            {
                Type = TraversalPhaseType.Flip, StartTime = cursor, Duration = flipActual,
                StartPos = midpoint, EndPos = flipEnd,
                StartVel = dir * peakSpeed, EndVel = dir * peakSpeed,
                StartHeading = dir, EndHeading = retrograde,
            });
            cursor += flipActual;

            if (brakeOnArrival)
            {
                phases.Add(new TraversalPhase
                {
                    Type = TraversalPhaseType.Braking, StartTime = cursor, Duration = halfTime,
                    StartPos = flipEnd, EndPos = to,
                    StartVel = dir * peakSpeed, EndVel = Vector2.Zero,
                    StartHeading = retrograde, EndHeading = retrograde,
                });
                cursor += halfTime;
            }
            else
            {
                float militaryTime = Vector2.Distance(flipEnd, to) / Math.Max(peakSpeed, 0.001f);
                phases.Add(new TraversalPhase
                {
                    Type = TraversalPhaseType.Coast, StartTime = cursor, Duration = militaryTime,
                    StartPos = flipEnd, EndPos = to,
                    StartVel = dir * peakSpeed, EndVel = dir * peakSpeed,
                    StartHeading = dir, EndHeading = dir,
                });
                cursor += militaryTime;
            }

            return new TraversalPlan
            {
                StartedAt    = DateTime.UtcNow,
                Phases       = phases,
                TotalSeconds = cursor,
                Destination  = to,
            };
        }

        // ── Shape constants cache ─────────────────────────────────────────

        /// <summary>
        /// Returns (or builds and caches) the arc geometry constants for a given
        /// engine configuration × movement profile combination.
        /// Key is bucketed so similar engines share entries.
        /// </summary>
        private static ShapeConstants GetShapeConstants(
            ShipStatsComponent stats,
            MovementProfile    profile)
        {
            string key = ShapeKey(stats, profile.Name);
            if (_shapeCache.TryGetValue(key, out var cached)) return cached;

            float thrust    = Math.Max(stats.TotalThrust, 0.001f) * profile.ThrustFraction;
            float speed     = Math.Max(stats.MaxSpeed,    0.001f) * profile.SpeedFraction;
            float turnRate  = Math.Max(stats.TurnRate,    0.001f);

            float accelTime = speed / thrust;
            float accelDist = 0.5f * thrust * accelTime * accelTime;
            float flipTime  = MathF.PI / turnRate;
            float flipDist  = speed * flipTime;
            float minFull   = accelDist + flipDist + accelDist;  // no coast below this

            var shape = new ShapeConstants(thrust, speed, accelTime, accelDist,
                                           flipTime, flipDist, minFull);
            _shapeCache[key] = shape;
            return shape;
        }

        /// <summary>
        /// Cache key: bucketed thrust × speed × turnRate × profileName.
        /// Bucketing ensures similar engine configs share entries without
        /// producing a different key for every floating point variation.
        /// </summary>
        private static string ShapeKey(ShipStatsComponent stats, string profileName)
        {
            int t = (int)(stats.TotalThrust / 10f) * 10;
            int s = (int)(stats.MaxSpeed    / 5f)  * 5;
            int r = (int)(stats.TurnRate    * 10f);
            return $"{t}:{s}:{r}:{profileName}";
        }

        // ── Fuel conservation auto-downgrade ─────────────────────────────

        private static MovementProfile ApplyFuelConservation(
            ShipStatsComponent stats,
            MovementProfile    requested)
        {
            if (requested.FuelConservationThreshold <= 0f) return requested;
            if (stats.TotalHydrogenCapacity <= 0f)         return requested;

            float fuelFraction = stats.CurrentHydrogen / stats.TotalHydrogenCapacity;
            if (fuelFraction < requested.FuelConservationThreshold &&
                requested.Name != MovementProfile.Economy.Name)
            {
                // Caller should log this — planner doesn't have entity access
                return MovementProfile.Economy;
            }

            return requested;
        }
    }
}
