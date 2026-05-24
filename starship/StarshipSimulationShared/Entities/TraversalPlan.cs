// StarshipSimulation.Shared/Entities/TraversalPlan.cs

using System;
using System.Collections.Generic;
using System.Numerics;

namespace StarshipSimulation.Shared.Entities
{
    // ----------------------------------------------------------------
    // Phase types
    // ----------------------------------------------------------------

    public enum TraversalPhaseType
    {
        Accel,    // heading toward target, thrust forward, velocity 0 → MaxSpeed
        Coast,    // heading toward target, no thrust, constant MaxSpeed
        Flip,     // rotating 180° (aft toward destination), still moving forward
        Braking   // aft toward destination, thrust opposing velocity, MaxSpeed → 0
    }

    // ----------------------------------------------------------------
    // A single phase of the traversal arc
    // ----------------------------------------------------------------

    /// <summary>
    /// One segment of a pre-computed traversal plan.
    /// All times are relative to plan start (seconds elapsed).
    /// </summary>
    public class TraversalPhase
    {
        public TraversalPhaseType Type      { get; init; }
        public double             StartTime { get; init; }   // seconds from plan start
        public double             Duration  { get; init; }   // seconds

        public Vector2 StartPos     { get; init; }
        public Vector2 EndPos       { get; init; }
        public Vector2 StartVel     { get; init; }
        public Vector2 EndVel       { get; init; }
        public Vector2 StartHeading { get; init; }
        public Vector2 EndHeading   { get; init; }

        public double EndTime => StartTime + Duration;
    }

    // ----------------------------------------------------------------
    // The full traversal plan
    // ----------------------------------------------------------------

    /// <summary>
    /// A pre-computed flight plan for a single MoveTo leg.
    ///
    /// Built once by TraversalPlanner when a MoveTo step is assigned.
    /// Evaluated each micro tick instead of running Newtonian physics.
    /// Cleared (set to null on entity) when any of these occur:
    ///   - new order assigned
    ///   - engine damaged (ShipStatsComponent invalidated)
    ///   - route blocked (gate destroyed, wormhole collapsed)
    ///   - entity takes combat damage
    ///   - player issues manual command
    ///
    /// When observed by a player, full physics takes over and the plan
    /// is discarded. The plan is rebuilt on next departure.
    ///
    /// See Core Truths — Traversal Plan.
    /// </summary>
    public class TraversalPlan
    {
        public DateTime              StartedAt    { get; init; } = DateTime.UtcNow;
        public List<TraversalPhase>  Phases       { get; init; } = new();
        public double                TotalSeconds { get; init; }

        public Vector2 Destination { get; init; }

        /// <summary>Has the plan run to completion?</summary>
        public bool IsComplete(double elapsedSeconds) => elapsedSeconds >= TotalSeconds;

        // ------------------------------------------------------------
        // Evaluation — position, velocity, heading at elapsed seconds
        // ------------------------------------------------------------

        /// <summary>
        /// Returns the interpolated state at a given elapsed time.
        /// Pure arithmetic — no integration, no drift.
        /// </summary>
        public (Vector2 position, Vector2 velocity, Vector2 heading)
            Evaluate(double elapsedSeconds)
        {
            elapsedSeconds = Math.Clamp(elapsedSeconds, 0, TotalSeconds);

            // Find the phase that contains this time
            foreach (var phase in Phases)
            {
                if (elapsedSeconds > phase.EndTime) continue;

                double t  = elapsedSeconds - phase.StartTime;
                double td = phase.Duration > 0 ? t / phase.Duration : 1.0;

                switch (phase.Type)
                {
                    case TraversalPhaseType.Accel:
                        return EvaluateAccel(phase, t);

                    case TraversalPhaseType.Coast:
                        return EvaluateCoast(phase, td);

                    case TraversalPhaseType.Flip:
                        return EvaluateFlip(phase, td);

                    case TraversalPhaseType.Braking:
                        return EvaluateBraking(phase, t);
                }
            }

            // Past end of plan — stationary at destination
            return (Destination, Vector2.Zero, Phases.Count > 0
                ? Phases[^1].EndHeading
                : Vector2.UnitX);
        }

        // ------------------------------------------------------------
        // Phase evaluators
        // ------------------------------------------------------------

        /// <summary>Quadratic acceleration: p = p0 + v0*t + 0.5*a*t²</summary>
        private static (Vector2, Vector2, Vector2)
            EvaluateAccel(TraversalPhase p, double t)
        {
            var dir   = p.EndVel.Length() > 0.001f
                ? Vector2.Normalize(p.EndVel) : Vector2.UnitX;
            float a   = p.Duration > 0
                ? p.EndVel.Length() / (float)p.Duration : 0f;

            var vel  = dir * (float)(a * t);
            var pos  = p.StartPos + dir * (float)(a * t * t * 0.5);

            return (pos, vel, dir);
        }

        /// <summary>
        /// Constant velocity: lerp position, heading faces direction of travel.
        /// During coast the ship's nose points where it's going — sensor cones
        /// face forward, visually correct.
        /// </summary>
        private static (Vector2, Vector2, Vector2)
            EvaluateCoast(TraversalPhase p, double td)
        {
            var pos = Vector2.Lerp(p.StartPos, p.EndPos, (float)td);
            // Heading faces direction of travel, not a stored value
            var heading = p.StartVel.Length() > 0.001f
                ? Vector2.Normalize(p.StartVel)
                : p.StartHeading;
            return (pos, p.StartVel, heading);
        }

        /// <summary>Flip: linear position, heading slerped from fwd to retrograde.</summary>
        private static (Vector2, Vector2, Vector2)
            EvaluateFlip(TraversalPhase p, double td)
        {
            var pos     = Vector2.Lerp(p.StartPos, p.EndPos, (float)td);
            var heading = SlerpHeading(p.StartHeading, p.EndHeading, (float)td);
            return (pos, p.StartVel, heading);
        }

        /// <summary>Quadratic deceleration: p = p0 + v0*t - 0.5*a*t²</summary>
        private static (Vector2, Vector2, Vector2)
            EvaluateBraking(TraversalPhase p, double t)
        {
            var dir   = p.StartVel.Length() > 0.001f
                ? Vector2.Normalize(p.StartVel) : Vector2.UnitX;
            float a   = p.Duration > 0
                ? p.StartVel.Length() / (float)p.Duration : 0f;

            float spd = Math.Max(0f, p.StartVel.Length() - a * (float)t);
            var vel   = dir * spd;
            var pos   = p.StartPos
                + dir * (float)(p.StartVel.Length() * t - 0.5 * a * t * t);

            // Heading is retrograde during braking (aft faces destination)
            return (pos, vel, p.EndHeading);
        }

        // ------------------------------------------------------------
        // Heading slerp helper
        // ------------------------------------------------------------

        private static Vector2 SlerpHeading(Vector2 from, Vector2 to, float t)
        {
            float angleFrom = MathF.Atan2(from.Y, from.X);
            float angleTo   = MathF.Atan2(to.Y,   to.X);

            // Shortest arc
            float delta = angleTo - angleFrom;
            while (delta >  MathF.PI) delta -= MathF.PI * 2f;
            while (delta < -MathF.PI) delta += MathF.PI * 2f;

            float angle = angleFrom + delta * t;
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
    }
}
