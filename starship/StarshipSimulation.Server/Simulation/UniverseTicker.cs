using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace StarshipSimulation.Server.Simulation
{
    /// <summary>
    /// Background service that drives the simulation tick loop.
    ///
    /// Runs continuously at the micro tick rate (~20Hz).
    /// Checks the clock each iteration and fires a macro tick when due.
    ///
    /// Tick separation:
    ///   Micro tick — runs every iteration (~50ms). Handles observed entities,
    ///                physics, and anything needing real-time fidelity.
    ///
    ///   Macro tick — runs on a configurable interval (default 5s). Handles
    ///                unobserved entities, economy, production, and trade.
    ///
    /// UniverseTicker owns timing. UniverseService owns simulation logic.
    /// </summary>
    public class UniverseTicker : BackgroundService
    {
        // ------------------------------------------------------------
        // Dependencies
        // ------------------------------------------------------------

        private readonly UniverseService _universe;

        // ------------------------------------------------------------
        // Timing
        // ------------------------------------------------------------

        /// <summary>
        /// How often the micro tick loop runs.
        /// 50ms = 20Hz. Adjust to tune CPU usage vs. simulation fidelity.
        /// </summary>
        private static readonly TimeSpan MicroTickInterval = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Delta time passed to micro tick systems — in seconds.
        /// </summary>
        private const double MicroDeltaSeconds = 0.05;

        /// <summary>
        /// Delta time passed to macro tick systems — in seconds.
        /// Matches UniverseClock.MacroTickInterval.
        /// </summary>
        private const double MacroDeltaSeconds = 5.0;

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public UniverseTicker(UniverseService universe)
        {
            _universe = universe;
        }

        // ------------------------------------------------------------
        // Tick loop
        // ------------------------------------------------------------

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[Ticker] Simulation loop started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                // Macro tick — runs when the clock says it's due
                if (_universe.Clock.IsMacroTickDue(now))
                {
                    _universe.RunMacroTick(MacroDeltaSeconds);
                    _universe.Clock.AdvanceMacroTick();
                }

                // Micro tick — runs every iteration
                _universe.RunMicroTick(MicroDeltaSeconds);

                await Task.Delay(MicroTickInterval, stoppingToken);
            }

            Console.WriteLine("[Ticker] Simulation loop stopped.");
        }
    }
}
