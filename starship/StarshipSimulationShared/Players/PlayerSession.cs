using System;
using System.Collections.Concurrent;
using System.Threading;

namespace StarshipSimulation.Shared.Players
{
    /// <summary>
    /// Represents a connected player and their simulation presence.
    ///
    /// Created on login, destroyed on disconnect.
    /// Links a client connection to the entity they control in the simulation.
    ///
    /// Intentionally has no reference to UniverseService — Shared cannot
    /// depend on Server. Entity creation on login is handled by CommandHandler
    /// which lives in Server and has access to both.
    /// </summary>
    public class PlayerSession
    {
        /// <summary>
        /// Stable client-side identifier sent on login.
        /// Allows session resumption if a client reconnects.
        /// </summary>
        public string ClientId { get; set; } = "";

        /// <summary>
        /// Server-assigned player identifier.
        /// e.g. "player-1"
        /// </summary>
        public string PlayerId { get; set; } = "";

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// The entity this player currently controls.
        /// Sent back to the client as part of the login acknowledgement
        /// so the client knows which entity to apply WASD input to.
        /// </summary>
        public string EntityId { get; set; } = "";
    }

    /// <summary>
    /// Manages all active player sessions.
    /// Registered as a singleton in Program.cs.
    ///
    /// Handles session resumption — if a client reconnects with the
    /// same ClientId, they get back their existing session rather than
    /// spawning a duplicate entity.
    ///
    /// Note: Does NOT create simulation entities. That is the
    /// responsibility of CommandHandler which has access to UniverseService.
    /// </summary>
    public class PlayerSessionStore
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _byClientId = new();
        private readonly ConcurrentDictionary<string, PlayerSession> _byPlayerId  = new();
        private int _counter = 0;

        /// <summary>
        /// Returns an existing session for this ClientId if one exists.
        /// Returns null if this is a new client — caller must create
        /// the session via CreateSession() after spawning the entity.
        /// </summary>
        public PlayerSession? TryResume(string clientId)
        {
            if (_byClientId.TryGetValue(clientId, out var existing))
            {
                Console.WriteLine($"[Sessions] Resumed session: {existing.PlayerId}");
                return existing;
            }

            return null;
        }

        /// <summary>
        /// Creates and registers a new session.
        /// Called by CommandHandler after it has spawned the player entity.
        /// </summary>
        public PlayerSession CreateSession(string clientId, string name, string entityId)
        {
            var playerId = $"player-{Interlocked.Increment(ref _counter)}";

            var session = new PlayerSession
            {
                ClientId = clientId,
                PlayerId = playerId,
                Name     = string.IsNullOrWhiteSpace(name) ? playerId : name,
                EntityId = entityId
            };

            _byClientId[clientId] = session;
            _byPlayerId[playerId]  = session;

            Console.WriteLine($"[Sessions] New session: {playerId} ({session.Name}) entity={entityId}");

            return session;
        }

        /// <summary>
        /// Removes a session on disconnect.
        /// Does not destroy the entity — that is a game design decision.
        /// </summary>
        public void Remove(string playerId)
        {
            if (_byPlayerId.TryRemove(playerId, out var session))
            {
                _byClientId.TryRemove(session.ClientId, out _);
                Console.WriteLine($"[Sessions] Removed session: {playerId}");
            }
        }

        public PlayerSession? GetByPlayerId(string playerId)
            => _byPlayerId.TryGetValue(playerId, out var s) ? s : null;

        public PlayerSession? GetByClientId(string clientId)
            => _byClientId.TryGetValue(clientId, out var s) ? s : null;
    }
}
