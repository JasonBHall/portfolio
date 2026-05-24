using System;

namespace StarshipSimulation.Server.Simulation
{
    /// <summary>
    /// Tracks simulation time — both real-time wall clock and game time.
    ///
    /// Responsibilities:
    ///   - Know when the next macro tick is due
    ///   - Track total elapsed game time
    ///   - Provide interpolation factor between macro ticks (for smooth client rendering)
    ///
    /// Does not drive ticks — UniverseTicker calls this to check timing.
    /// </summary>
    public class UniverseClock
    {
        // ------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------

        /// <summary>
        /// How often a macro tick runs in real time.
        /// Default: 5 seconds. Configurable at construction.
        /// </summary>
        public TimeSpan MacroTickInterval { get; }

        // ------------------------------------------------------------
        // Macro tick boundaries
        // ------------------------------------------------------------

        private DateTime _lastMacroTick;
        private DateTime _nextMacroTick;

        // ------------------------------------------------------------
        // Game time
        // ------------------------------------------------------------

        /// <summary>
        /// Total elapsed game time in seconds.
        /// Advances by MacroTickInterval each macro tick.
        /// </summary>
        public double TotalGameSeconds { get; private set; }

        /// <summary>
        /// Total macro ticks that have run since startup.
        /// </summary>
        public long MacroTickCount { get; private set; }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public UniverseClock(TimeSpan macroTickInterval)
        {
            MacroTickInterval = macroTickInterval;
            _lastMacroTick = DateTime.UtcNow;
            _nextMacroTick = _lastMacroTick + macroTickInterval;
        }

        // ------------------------------------------------------------
        // Tick boundary
        // ------------------------------------------------------------

        /// <summary>
        /// Returns true if enough real time has passed to run a macro tick.
        /// </summary>
        public bool IsMacroTickDue(DateTime now) => now >= _nextMacroTick;

        /// <summary>
        /// Advances the macro tick boundary forward.
        /// Call immediately after running a macro tick.
        /// </summary>
        public void AdvanceMacroTick()
        {
            _lastMacroTick = _nextMacroTick;
            _nextMacroTick = _lastMacroTick + MacroTickInterval;
            TotalGameSeconds += MacroTickInterval.TotalSeconds;
            MacroTickCount++;
        }

        // ------------------------------------------------------------
        // Interpolation
        // ------------------------------------------------------------

        /// <summary>
        /// Returns a value 0→1 representing how far we are between the last
        /// and next macro tick boundaries.
        ///
        /// Used by the client to interpolate entity positions smoothly
        /// between discrete server ticks.
        /// </summary>
        public float GetInterpolationFactor(DateTime now)
        {
            double total = MacroTickInterval.TotalSeconds;
            double elapsed = (now - _lastMacroTick).TotalSeconds;

            if (total <= 0) return 1f;

            return Math.Clamp((float)(elapsed / total), 0f, 1f);
        }
    }
}
