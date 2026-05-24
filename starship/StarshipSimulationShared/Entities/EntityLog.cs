using System;
using System.Collections.Generic;

namespace StarshipSimulation.Shared.Entities
{
    // ------------------------------------------------------------
    // Log level
    // ------------------------------------------------------------

    public enum LogLevel
    {
        Info    = 0,
        Warning = 1,
        Error   = 2,
        Event   = 3   // significant game events — combat, jumps, job complete
    }

    // ------------------------------------------------------------
    // Single log entry
    // ------------------------------------------------------------

    public class EntityLogEntry
    {
        public DateTime Timestamp { get; init; }
        public string   Source    { get; init; } = "";
        public string   Message   { get; init; } = "";
        public LogLevel Level     { get; init; } = LogLevel.Info;

        /// <summary>Pre-formatted string for wire transmission.</summary>
        public string Formatted =>
            $"[{Timestamp:HH:mm:ss}] [{Source}] {Message}";
    }

    // ------------------------------------------------------------
    // EntityLog — always present on every entity
    // ------------------------------------------------------------

    /// <summary>
    /// A rolling log of what an entity is doing, why, and what just happened.
    ///
    /// Three layers:
    ///   CurrentStatus  — what it's doing right now (one sentence)
    ///   CurrentIntent  — the broader goal (job, patrol, standby)
    ///   Recent entries — rolling 20-entry buffer from all systems
    ///
    /// Every simulation system writes here as standard practice.
    /// Five entries are sent over the wire per tick.
    /// Full 20 entries available server-side for debugging.
    ///
    /// See Core Truths — Entity Reporting.
    /// </summary>
    public class EntityLog
    {
        // ------------------------------------------------------------
        // Status and intent — overwritten by systems as they run
        // ------------------------------------------------------------

        /// <summary>
        /// What the entity is doing right now.
        /// Written by whichever system is currently driving the entity.
        /// </summary>
        public string CurrentStatus { get; private set; } = "idle";

        /// <summary>
        /// The broader goal — job, patrol, standby instruction.
        /// Written when a job or order is assigned.
        /// Persists until explicitly overwritten.
        /// </summary>
        public string CurrentIntent { get; private set; } = "";

        // ------------------------------------------------------------
        // Rolling log buffer
        // ------------------------------------------------------------

        private const int MaxEntries = 20;

        private readonly Queue<EntityLogEntry> _buffer = new();

        /// <summary>All buffered entries, newest last.</summary>
        public IReadOnlyCollection<EntityLogEntry> Entries => _buffer;

        // ------------------------------------------------------------
        // Write API — called by simulation systems
        // ------------------------------------------------------------

        /// <summary>
        /// Update the current one-line status.
        /// Call whenever the entity transitions to a new activity.
        /// </summary>
        public void SetStatus(string status)
        {
            CurrentStatus = status;
        }

        /// <summary>
        /// Update the current intent (job, order, mission).
        /// Call when a job is assigned or a player issues a command.
        /// </summary>
        public void SetIntent(string intent)
        {
            CurrentIntent = intent;
        }

        /// <summary>
        /// Write a log entry. Trims buffer to MaxEntries.
        /// </summary>
        public void Write(
            string   source,
            string   message,
            LogLevel level = LogLevel.Info)
        {
            _buffer.Enqueue(new EntityLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Source    = source,
                Message   = message,
                Level     = level
            });

            while (_buffer.Count > MaxEntries)
                _buffer.Dequeue();
        }

        // Convenience overloads for common levels
        public void Info   (string source, string message) => Write(source, message, LogLevel.Info);
        public void Warning(string source, string message) => Write(source, message, LogLevel.Warning);
        public void Error  (string source, string message) => Write(source, message, LogLevel.Error);
        public void Event  (string source, string message) => Write(source, message, LogLevel.Event);

        // ------------------------------------------------------------
        // Wire helpers — called by UniverseStream
        // ------------------------------------------------------------

        /// <summary>
        /// Returns the N most recent entries as pre-formatted strings.
        /// Used to populate EntityState.RecentLog for wire transmission.
        /// </summary>
        public List<string> GetRecentFormatted(int count = 5)
        {
            var result = new List<string>();
            var entries = new List<EntityLogEntry>(_buffer);

            int start = Math.Max(0, entries.Count - count);
            for (int i = entries.Count - 1; i >= start; i--)
                result.Add(entries[i].Formatted);

            return result;
        }
    }
}
