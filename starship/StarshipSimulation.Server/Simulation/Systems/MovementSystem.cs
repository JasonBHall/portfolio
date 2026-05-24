// StarshipSimulation.Server/Simulation/Systems/MovementSystem.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;
using StarshipSimulation.Shared.Entities.Orders;
using DriveType = StarshipSimulation.Shared.Entities.Components.DriveType;
// TraversalPlanner, TraversalPlan, MovementProfile — same solution, no extra using needed

namespace StarshipSimulation.Server.Simulation.Systems
{
    /// <summary>
    /// Drives all entity movement each micro tick.
    ///
    /// Two movement modes — chosen per entity based on whether it has
    /// a PlayerControlledComponent:
    ///
    ///   PLAYER CONTROLLED → Newtonian physics
    ///     Thrust applied along heading, velocity persists.
    ///     Maneuver thrusters handle rotation and lateral translation.
    ///     Drag applied only when no plan and no input (active drag).
    ///     Client input comes from CommandHandler → entity.Heading + throttle.
    ///
    ///   AUTONOMOUS (NPC) → Route following
    ///     Entity has a current Order with a destination.
    ///     Accelerates toward waypoint, decelerates to arrive cleanly.
    ///     Respects the same physics — max speed, turn rate, fuel.
    ///     Jump requires full stop. Warp preserves entry velocity.
    ///
    /// In both modes, ShipStatsComponent provides the cached capability
    /// values — no per-tick component iteration required.
    ///
    /// See Core Truths — Movement, Commitment and Consequence.
    /// </summary>
    public class MovementSystem
    {
        private readonly UniverseService _universe;
        private static readonly Random   _rng = new();

        // Drag coefficient applied per tick when active drag is in effect.
        // Tunable — higher = stops faster, lower = more coasting feel.
        private const float ActiveDragFactor = 0.92f;

        // Arrival threshold — entity is considered "at destination" within this distance.
        private const float ArrivalThreshold = 5f;

        // Minimum speed below which we snap to zero (prevents endless micro-drift).
        private const float VelocitySnapThreshold = 0.01f;

        public MovementSystem(UniverseService universe)
        {
            _universe = universe;
        }

        // ------------------------------------------------------------
        // Main tick — called by UniverseService.RunMicroTick()
        // ------------------------------------------------------------

        public void Tick(double deltaTime)
        {
            var dt = (float)deltaTime;

            foreach (var (_, entity) in _universe.Entities)
            {
                if (!entity.IsAlive) continue;

                var stats = entity.GetComponent<ShipStatsComponent>();

                // ----------------------------------------------------
                // Engineless entities — pure drift, no drag
                // Swarms, torpedoes, debris, probes on trajectory
                // ----------------------------------------------------
                if (stats == null)
                {
                    if (entity.Velocity != Vector2.Zero)
                    {
                        entity.Position += entity.Velocity * dt;
                        entity.MarkDirty();
                        _universe.SpatialGrid.UpdateEntity(entity);
                    }
                    continue;
                }

                // Rebuild stats cache if dirty
                stats.Tick(entity, deltaTime);

                if (!stats.CanMove)
                {
                    // No engines capable of movement — drift only
                    ApplyDrift(entity, dt);
                    _universe.SpatialGrid.UpdateEntity(entity);
                    continue;
                }

                // Player-controlled entities use Newtonian input-driven physics.
                // Autonomous (NPC) entities follow pre-planned routes.
                var isPlayerControlled = entity.HasComponent<PlayerControlledComponent>();

                if (isPlayerControlled)
                    TickPlayerMovement(entity, stats, deltaTime);
                else
                    TickAutonomousMovement(entity, stats, deltaTime);

                TickFuel(entity, deltaTime);
                _universe.SpatialGrid.UpdateEntity(entity);
            }
        }

        // ------------------------------------------------------------
        // PLAYER MOVEMENT — Newtonian physics driven by client input
        //
        // Thrust model (asymmetric by design — "flip and burn" feel):
        //   W (throttle > 0) → full MaxThrust along +Heading
        //   S (throttle < 0) → MaxThrust × ReverseThrustFraction along -Heading
        //                      (default 0.5 — braking bias; encourages 180°
        //                      flips over long slow reverse crawls)
        //   A/D  → Heading rotation (CommandHandler writes entity.Heading)
        //   Q/E  → lateral strafe via CurrentLateralInput
        //
        // Dampener rule — Z toggles DampenersOn:
        //   ON  + no pilot input → DIRECTIONAL dampening
        //     The ship picks which thrusters to fire based on where its
        //     velocity is relative to heading:
        //       velocity pointing BEHIND heading  → main drive forward (+H)
        //       velocity pointing AHEAD of heading → reverse thrust (-H, at
        //                                            ReverseThrustFraction)
        //       lateral component                  → lateral authority opposes
        //     Caps each component at what that thruster could do this tick.
        //     Below snap threshold: zero velocity.
        //
        //   OFF + no pilot input → ApplyDrift (pure Newtonian coast)
        //
        //   Any pilot input present → dampening is skipped; input dominates.
        // ------------------------------------------------------------

        private void TickPlayerMovement(
            Entity entity,
            ShipStatsComponent stats,
            double deltaTime)
        {
            var dt = (float)deltaTime;

            var engine = entity.GetAllComponents()
                               .OfType<EngineModule>()
                               .FirstOrDefault(e =>
                                   e.IsOperational &&
                                   e.DriveType == DriveType.Sublight);

            float throttle = engine?.CurrentThrottle     ?? 0f;
            float lateral  = engine?.CurrentLateralInput ?? 0f;
            float revFrac  = engine?.ReverseThrustFraction ?? 0.5f;

            bool hasThrustInput  = MathF.Abs(throttle) > 0.001f;
            bool hasLateralInput = MathF.Abs(lateral)  > 0.001f;

            // ---------------- FORWARD / REVERSE THRUST (W/S) ----------------
            if (hasThrustInput && stats.TotalThrust > 0f)
            {
                // Reverse is capped at ReverseThrustFraction of forward capability
                float effective = throttle >= 0f
                    ? throttle
                    : throttle * revFrac;   // throttle -1 → effective -revFrac

                float accel = stats.TotalThrust * effective * dt;
                entity.Velocity += entity.Heading * accel;

                float fuelCost = MathF.Abs(effective) * dt * 0.5f;
                ConsumeFuel(entity, FuelType.Hydrogen, fuelCost);
            }

            // ---------------- LATERAL STRAFE (Q/E) ----------------
            if (hasLateralInput && stats.LateralAuthority > 0f)
            {
                var right = new Vector2(entity.Heading.Y, -entity.Heading.X);
                entity.Velocity += right * stats.LateralAuthority * lateral * dt;

                float fuelCost = MathF.Abs(lateral) * dt * 0.2f;
                ConsumeFuel(entity, FuelType.Hydrogen, fuelCost);
            }

            // ---------------- CLAMP TO MAX SPEED ----------------
            if (entity.Velocity.LengthSquared() > stats.MaxSpeed * stats.MaxSpeed)
                entity.Velocity = Vector2.Normalize(entity.Velocity) * stats.MaxSpeed;

            // ---------------- NO-INPUT: DIRECTIONAL DAMPENERS ----------------
            if (!hasThrustInput && !hasLateralInput)
            {
                var pc = entity.GetComponent<PlayerControlledComponent>();
                bool dampenersOn = pc?.DampenersOn ?? true;

                if (dampenersOn)
                    ApplyDirectionalDampening(entity, stats, revFrac, dt);
                // else: pure Newtonian drift — fall through to integration.
            }

            // ---------------- APPLY VELOCITY ----------------
            if (entity.Velocity.LengthSquared() > VelocitySnapThreshold * VelocitySnapThreshold)
            {
                entity.Position += entity.Velocity * dt;
                entity.MarkDirty();
            }
            else if (entity.Velocity != Vector2.Zero)
            {
                entity.Velocity = Vector2.Zero;
                entity.MarkDirty();
            }

            // ---------------- STATUS LINE ----------------
            var speed = entity.Velocity.Length();
            var damp  = (entity.GetComponent<PlayerControlledComponent>()?.DampenersOn ?? true) ? "DMP" : "---";
            entity.Log.SetStatus(
                $"Player — thr={throttle:+0.0;-0.0;0} lat={lateral:+0.0;-0.0;0} spd={speed:F1} [{damp}]"
            );
        }

        // ------------------------------------------------------------
        // Directional dampening — Space-Engineers-style auto-brake.
        //
        // Splits current velocity into two orthogonal components relative
        // to the ship's heading:
        //   vParallel  — along heading axis (forward/backward drift)
        //   vLateral   — perpendicular to heading (sideways drift)
        //
        // Each is opposed by the appropriate thruster budget for this tick:
        //   parallel → main drive (MaxThrust × dt), with reverse leg at
        //              ReverseThrustFraction to match the active model
        //   lateral  → lateral authority (LateralAuthority × dt)
        //
        // Each cancel is capped at the magnitude of that component — we
        // never overshoot and reverse direction in a single tick.
        // ------------------------------------------------------------

        private static void ApplyDirectionalDampening(
            Entity entity,
            ShipStatsComponent stats,
            float reverseThrustFraction,
            float dt)
        {
            if (entity.Velocity == Vector2.Zero) return;

            Vector2 heading = entity.Heading;
            if (heading.LengthSquared() < 0.0001f) return;  // no heading → cannot aim thrust

            Vector2 h = Vector2.Normalize(heading);
            Vector2 right = new Vector2(h.Y, -h.X);

            // Decompose velocity
            float vPar = Vector2.Dot(entity.Velocity, h);       // along heading
            float vLat = Vector2.Dot(entity.Velocity, right);   // sideways

            // --- Parallel cancel ---
            if (MathF.Abs(vPar) > 0f && stats.TotalThrust > 0f)
            {
                //   vPar > 0 → velocity points forward → brake with REVERSE leg
                //   vPar < 0 → velocity points backward → brake with FORWARD leg (full)
                float budget = stats.TotalThrust * dt;
                if (vPar > 0f) budget *= reverseThrustFraction;

                // Opposite-sign delta, capped so we don't overshoot past zero
                float delta = MathF.Min(budget, MathF.Abs(vPar));
                entity.Velocity -= h * MathF.Sign(vPar) * delta;
            }

            // --- Lateral cancel ---
            if (MathF.Abs(vLat) > 0f && stats.LateralAuthority > 0f)
            {
                float budget = stats.LateralAuthority * dt;
                float delta  = MathF.Min(budget, MathF.Abs(vLat));
                entity.Velocity -= right * MathF.Sign(vLat) * delta;
            }

            if (entity.Velocity.LengthSquared() < VelocitySnapThreshold * VelocitySnapThreshold)
                entity.Velocity = Vector2.Zero;

            entity.MarkDirty();
        }

        // ------------------------------------------------------------
        // AUTONOMOUS MOVEMENT — NPC route following via NavigationSystem
        // ------------------------------------------------------------

        private void TickAutonomousMovement(
            Entity             entity,
            ShipStatsComponent stats,
            double             deltaTime)
        {
            var dt = (float)deltaTime;

            // No order → drift to a stop
            if (entity.CurrentOrder == null)
            {
                entity.HasArrived    = false;
                entity.TraversalPlan = null;
                ApplyDrift(entity, dt);
                ApplyActiveDrag(entity, stats, dt);
                entity.Log.SetStatus(entity.Velocity.Length() < VelocitySnapThreshold
                    ? "Idle"
                    : $"Decelerating  spd={entity.Velocity.Length():F1}");
                return;
            }

            var step = entity.CurrentOrder.CurrentStep;
            if (step == null)
            {
                // All steps done — OrderSystem will call CompleteOrder on next macro tick.
                // MovementSystem must NOT clear CurrentOrder here — that would prevent
                // OrderSystem from reaching CompleteOrder and calling ClearJob().
                // Just stop moving and wait.
                ApplyActiveDrag(entity, stats, dt);
                entity.Log.SetStatus("Arrived — finalising");
                return;
            }

            // MovementSystem only handles MoveTo steps
            if (step.StepType != StarshipSimulation.Shared.Entities.Orders.StepType.MoveTo)
                return;

            // Guard: OnEntityArrived has set this, OrderSystem will advance on macro tick
            if (entity.HasArrived) return;

            // Ensure ActiveRoute is planned
            if (entity.ActiveRoute == null)
            {
                var destination = ResolveDestination(step);
                if (destination == null)
                {
                    entity.CurrentOrder.CurrentStepIndex++;
                    return;
                }

                entity.ActiveRoute = _universe.NavigationSystem.PlanRoute(
                    entity.Position, destination.Value, stats);
                entity.ActiveSegmentIndex    = 0;
                entity.SegmentElapsedSeconds = 0;
                entity.HasArrived            = false;
                entity.TraversalPlan         = null;

                if (!entity.ActiveRoute.IsReachable)
                {
                    entity.Log.Warning("Movement",
                        $"No route to {destination.Value} — skipping step");
                    entity.ActiveRoute = null;
                    entity.CurrentOrder.CurrentStepIndex++;
                    return;
                }

                entity.Log.Info("Movement",
                    $"Route planned: {entity.ActiveRoute.Segments.Count} segment(s) " +
                    $"~{entity.ActiveRoute.TotalSeconds:F0}s");
                entity.MarkDirty();
            }

            ExecuteRouteSegment(entity, stats, dt);
            TickFuel(entity, deltaTime);
        }

        // ------------------------------------------------------------
        // ROUTE SEGMENT EXECUTION
        // ------------------------------------------------------------

        private void ExecuteRouteSegment(
            Entity             entity,
            ShipStatsComponent stats,
            float              dt)
        {
            var route = entity.ActiveRoute;
            if (route == null) return;

            if (entity.ActiveSegmentIndex >= route.Segments.Count)
            {
                CompleteRoute(entity);
                return;
            }

            var segment = route.Segments[entity.ActiveSegmentIndex];
            bool segmentDone = false;

            switch (segment.Mode)
            {
                case MovementMode.Sublight:
                    segmentDone = ExecuteSublight_Segment(entity, stats, segment, dt);
                    break;

                case MovementMode.Warp:
                    segmentDone = ExecuteWarp_Segment(entity, stats, segment.To, dt);
                    break;

                case MovementMode.JumpCharging:
                    entity.SegmentElapsedSeconds += dt;
                    entity.Log.SetStatus(
                        $"Jump charging  {entity.SegmentElapsedSeconds:F0}s / " +
                        $"{segment.DurationSeconds:F0}s");
                    if (entity.SegmentElapsedSeconds >= segment.DurationSeconds)
                    {
                        entity.SegmentElapsedSeconds = 0;
                        segmentDone = true;
                    }
                    entity.MarkDirty();
                    break;

                case MovementMode.JumpExecuting:
                    entity.Position = segment.To;
                    entity.Velocity = Vector2.Zero;
                    entity.TraversalPlan = null;
                    entity.Log.Event("Movement", $"Jump executed → {segment.To}");
                    entity.MarkDirty();
                    segmentDone = true;
                    break;

                case MovementMode.GateTransfer:
                    entity.Position = segment.To;
                    entity.Velocity = Vector2.Zero;
                    entity.TraversalPlan = null;
                    entity.Log.Event("Movement", $"Gate transit → {segment.To}");
                    entity.MarkDirty();
                    segmentDone = true;
                    break;

                case MovementMode.WormholeTransfer:
                    entity.Position = segment.To;
                    entity.Velocity = Vector2.Zero;
                    entity.TraversalPlan = null;
                    entity.Log.Event("Movement", $"Wormhole transit → {segment.To}");
                    entity.MarkDirty();
                    segmentDone = true;
                    break;

                case MovementMode.StargateTransfer:
                    entity.Position = segment.To;
                    entity.Velocity = Vector2.Zero;
                    entity.TraversalPlan = null;
                    entity.Log.Event("Movement", $"Stargate transit → {segment.To}");
                    entity.MarkDirty();
                    segmentDone = true;
                    break;
            }

            if (segmentDone)
            {
                entity.ActiveSegmentIndex++;
                entity.TraversalPlan = null;
                if (entity.ActiveSegmentIndex >= route.Segments.Count)
                    CompleteRoute(entity);
            }
        }

        private void CompleteRoute(Entity entity)
        {
            entity.ActiveRoute           = null;
            entity.ActiveSegmentIndex    = 0;
            entity.SegmentElapsedSeconds = 0;
            entity.TraversalPlan         = null;
            entity.Velocity              = Vector2.Zero;
            entity.MarkDirty();

            _universe.OrderSystem.OnEntityArrived(entity);
        }

        // ------------------------------------------------------------
        // SUBLIGHT SEGMENT — traversal plan evaluation
        //
        // Builds a TraversalPlan once per MoveTo leg (on first call).
        // Subsequent calls evaluate plan.Evaluate(elapsed) — pure arithmetic.
        // No Newtonian integration, no oscillation, no drift.
        //
        // Observer hook (future): when a player observes this entity,
        // discard the plan and run full Newtonian physics instead.
        // The plan is rebuilt on next departure.
        // ------------------------------------------------------------

        private bool ExecuteSublight_Segment(
            Entity             entity,
            ShipStatsComponent stats,
            MovementSegment    segment,
            float              dt)
        {
            float distance = Vector2.Distance(entity.Position, segment.To);
            if (distance < ArrivalThreshold)
            {
                entity.Position      = segment.To;
                entity.Velocity      = Vector2.Zero;
                entity.TraversalPlan = null;
                entity.MarkDirty();
                return true;
            }

            // Build plan on first call for this leg — pass current velocity
            // so retasking mid-flight produces correct dot-product handling,
            // and profile so fuel conservation auto-downgrades if needed.
            if (entity.TraversalPlan == null)
            {
                // Check if fuel conservation will kick in — warn player
                if (stats.TotalHydrogenCapacity > 0f)
                {
                    float fuelFrac = stats.CurrentHydrogen / stats.TotalHydrogenCapacity;
                    if (fuelFrac < entity.CurrentProfile.FuelConservationThreshold &&
                        entity.CurrentProfile.Name != MovementProfile.Economy.Name)
                    {
                        entity.Log.Warning("Movement",
                            $"Fuel low ({fuelFrac:P0}) — switching to economy profile");
                    }
                }

                entity.TraversalPlan = TraversalPlanner.Build(
                    entity.Position, segment.To, stats,
                    entity.Velocity,
                    entity.CurrentProfile);

                if (entity.TraversalPlan == null)
                {
                    entity.Position = segment.To;
                    entity.Velocity = Vector2.Zero;
                    entity.MarkDirty();
                    return true;
                }

                entity.Log.Info("Movement",
                    $"Plan: {entity.TraversalPlan.TotalSeconds:F0}s to destination");
            }

            // Evaluate plan at current elapsed time
            var    plan    = entity.TraversalPlan;
            double elapsed = (DateTime.UtcNow - plan.StartedAt).TotalSeconds;

            if (plan.IsComplete(elapsed))
            {
                entity.Position      = segment.To;
                entity.Velocity      = Vector2.Zero;
                entity.TraversalPlan = null;
                entity.MarkDirty();
                return true;
            }

            var (pos, vel, heading) = plan.Evaluate(elapsed);
            entity.Position = pos;
            entity.Velocity = vel;
            entity.Heading  = heading;
            entity.MarkDirty();

            float speed = vel.Length();
            entity.Log.SetStatus(
                $"{GetPhaseName(plan, elapsed)} — {distance:F0}m  spd={speed:F1}");

            return false;
        }

        private static string GetPhaseName(TraversalPlan plan, double elapsed)
        {
            foreach (var p in plan.Phases)
                if (elapsed <= p.EndTime)
                    return p.Type switch
                    {
                        TraversalPhaseType.Accel   => "Accelerating",
                        TraversalPhaseType.Coast   => "Coasting",
                        TraversalPhaseType.Flip    => "Flipping",
                        TraversalPhaseType.Braking => "Braking",
                        _                          => "Moving"
                    };
            return "Arriving";
        }

                // ------------------------------------------------------------
        // WARP SEGMENT — returns true when destination reached
        // Straight-line constant speed. Entry velocity preserved on exit.
        // ------------------------------------------------------------

        private bool ExecuteWarp_Segment(
            Entity             entity,
            ShipStatsComponent stats,
            Vector2            destination,
            float              dt)
        {
            if (!stats.CanWarp)
            {
                // Fallback: build a temporary sublight segment to destination
                var tempSeg = new MovementSegment
                {
                    Mode = MovementMode.Sublight,
                    From = entity.Position,
                    To   = destination,
                    DurationSeconds = 0
                };
                return ExecuteSublight_Segment(entity, stats, tempSeg, dt);
            }

            var   toTarget = destination - entity.Position;
            float distance = toTarget.Length();

            if (distance < ArrivalThreshold)
            {
                entity.Position = destination;
                // Warp exits with entry velocity preserved — ship arrives hot
                entity.MarkDirty();
                return true;
            }

            // Point along destination heading
            entity.Heading = Vector2.Normalize(toTarget);

            float travel = stats.WarpSpeed * dt;
            if (travel >= distance)
            {
                entity.Position = destination;
                entity.MarkDirty();
                return true;
            }

            entity.Position += entity.Heading * travel;
            ConsumeFuel(entity, FuelType.Dilithium, dt * 0.05f);
            entity.MarkDirty();
            return false;
        }

        /// <summary>
        /// Rotates a direction vector toward a target direction
        /// at a maximum angular rate per tick.
        /// Capital ships rotate slowly. Fighters snap almost instantly.
        /// </summary>
        private static Vector2 RotateToward(
            Vector2 current,
            Vector2 target,
            float maxRadians)
        {
            if (maxRadians <= 0f || current == target) return target;

            float currentAngle = MathF.Atan2(current.Y, current.X);
            float targetAngle  = MathF.Atan2(target.Y,  target.X);

            float delta = targetAngle - currentAngle;

            // Wrap to -π..π
            while (delta >  MathF.PI) delta -= MathF.PI * 2f;
            while (delta < -MathF.PI) delta += MathF.PI * 2f;

            float step = Math.Clamp(delta, -maxRadians, maxRadians);
            float newAngle = currentAngle + step;

            return new Vector2(MathF.Cos(newAngle), MathF.Sin(newAngle));
        }

        // ------------------------------------------------------------
        // PHYSICS HELPERS
        // ------------------------------------------------------------

        /// <summary>
        /// Active drag — maneuver thrusters bleeding velocity when no plan.
        /// Applied to player ships releasing controls and idle NPCs.
        /// Coasting entities (those with an active plan) are exempt.
        /// </summary>
        private static void ApplyActiveDrag(
            Entity entity,
            ShipStatsComponent stats,
            float dt)
        {
            if (entity.Velocity == Vector2.Zero) return;

            // Drag proportional to lateral authority — better maneuvering = faster stop
            float dragFactor = MathF.Pow(
                ActiveDragFactor + (0.98f - ActiveDragFactor) * (1f - stats.LateralAuthority),
                dt * 20f   // scale to tick rate
            );

            entity.Velocity *= dragFactor;

            if (entity.Velocity.LengthSquared() < VelocitySnapThreshold * VelocitySnapThreshold)
                entity.Velocity = Vector2.Zero;

            entity.MarkDirty();
        }

        /// <summary>
        /// Pure drift — apply velocity to position with no dampening.
        /// Used for coasting entities (torpedoes, probes, planned coast).
        /// </summary>
        private static void ApplyDrift(Entity entity, float dt)
        {
            if (entity.Velocity == Vector2.Zero) return;
            entity.Position += entity.Velocity * dt;
            entity.MarkDirty();
        }

        // ------------------------------------------------------------
        // FUEL
        // ------------------------------------------------------------

        private static void TickFuel(Entity entity, double deltaTime)
        {
            foreach (var engine in entity.GetAllComponents().OfType<EngineModule>())
            {
                engine.TickFuel(deltaTime);

                // Tick jump recharge cooldown
                if (engine.JumpRechargeCooldown > 0f)
                {
                    engine.JumpRechargeCooldown -= (float)deltaTime;
                    if (engine.JumpRechargeCooldown <= 0f)
                    {
                        engine.JumpRechargeCooldown = 0f;
                        entity.GetComponent<ShipStatsComponent>()?.Invalidate();
                        Console.WriteLine($"[MovementSystem] {entity.Name} jump drive recharged.");
                    }
                }
            }
        }

        private static void ConsumeFuel(Entity entity, FuelType fuelType, float amount)
        {
            float remaining     = amount;
            float totalConsumed = 0f;

            foreach (var engine in entity.GetAllComponents()
                                         .OfType<EngineModule>()
                                         .Where(e => e.UsesFuel && e.FuelType == fuelType))
            {
                float consumed   = engine.ConsumeFuel(remaining);
                totalConsumed   += consumed;
                remaining       -= consumed;
                if (remaining <= 0f) break;
            }

            // Only invalidate stats cache when fuel actually depleted.
            // Calling Invalidate() every tick (even with zero consumption) causes
            // TotalThrust to read as 0 on the wire between recalculate calls.
            if (totalConsumed > 0f)
                entity.GetComponent<ShipStatsComponent>()?.Invalidate();
        }

        // ------------------------------------------------------------
        // ORDER HELPERS
        // ------------------------------------------------------------

        private Vector2? ResolveDestination(OrderStep step)
        {
            if (step.TargetPosition.HasValue)
                return step.TargetPosition.Value;

            if (step.TargetEntityId != null)
            {
                var target = _universe.GetEntity(step.TargetEntityId);
                return target?.Position;
            }

            return null;
        }
    }
}
