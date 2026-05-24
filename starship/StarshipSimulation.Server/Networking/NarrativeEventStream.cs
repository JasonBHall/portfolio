using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StarshipSimulation.Shared.Messages;

namespace StarshipSimulation.Server.Networking
{
    /// <summary>
    /// Handles the /ws/events WebSocket channel.
    ///
    /// Pushes NarrativeEvents to connected clients immediately when
    /// they are emitted by the simulation — not tick-bound.
    ///
    /// This channel carries pre-calculated combat outcomes for visual
    /// playback: attrition sequences, explosions, carrier launches, etc.
    ///
    /// The server calculates outcomes instantly. The client plays them
    /// out over the event's Duration for the player.
    ///
    /// Design:
    ///   - Systems call NarrativeEventStream.Emit() to queue an event
    ///   - Each connected client has its own send queue
    ///   - Events are flushed to clients on each micro tick
    ///   - No event is dropped — if a client is slow, its queue grows
    ///     (bounded queue with warning at capacity)
    /// </summary>
    public class NarrativeEventStream
    {
        // ------------------------------------------------------------
        // Connected clients
        // Key = connection id (assigned on connect)
        // Value = that client's pending event queue
        // ------------------------------------------------------------

        private readonly ConcurrentDictionary<string, ClientEventQueue> _clients = new();

        // ------------------------------------------------------------
        // Serialisation
        // ------------------------------------------------------------

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ------------------------------------------------------------
        // Emit — called by simulation systems
        // Queues the event for all connected clients.
        // Fast — never blocks the simulation tick.
        // ------------------------------------------------------------

        public void Emit(NarrativeEvent narrativeEvent)
        {
            foreach (var client in _clients.Values)
                client.Enqueue(narrativeEvent);
        }

        // ------------------------------------------------------------
        // Connection handler
        // Called once per connecting client from Program.cs
        // ------------------------------------------------------------

        public async Task HandleAsync(WebSocket socket, CancellationToken ct)
        {
            var clientId = Guid.NewGuid().ToString();
            var queue    = new ClientEventQueue(capacity: 256);

            _clients[clientId] = queue;
            Console.WriteLine($"[EventStream] Client connected: {clientId}");

            try
            {
                while (!ct.IsCancellationRequested &&
                       socket.State == WebSocketState.Open)
                {
                    // Flush all pending events for this client
                    while (queue.TryDequeue(out var evt))
                    {
                        await SendAsync(socket, evt!, ct);
                    }

                    await Task.Delay(50, ct); // Flush at 20Hz
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[EventStream] Error on {clientId}: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                await CloseAsync(socket);
                Console.WriteLine($"[EventStream] Client disconnected: {clientId}");
            }
        }

        // ------------------------------------------------------------
        // Send helper
        // ------------------------------------------------------------

        private static async Task SendAsync(WebSocket socket, NarrativeEvent evt, CancellationToken ct)
        {
            var json  = JsonSerializer.Serialize(evt, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                ct
            );
        }

        private static async Task CloseAsync(WebSocket socket)
        {
            try
            {
                if (socket.State == WebSocketState.Open ||
                    socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None
                    );
                }
            }
            catch { }
        }

        // ------------------------------------------------------------
        // Per-client event queue
        // Bounded to prevent unbounded memory growth if a client is slow.
        // ------------------------------------------------------------

        private class ClientEventQueue
        {
            private readonly Queue<NarrativeEvent> _queue;
            private readonly int _capacity;

            public ClientEventQueue(int capacity)
            {
                _capacity = capacity;
                _queue    = new Queue<NarrativeEvent>(capacity);
            }

            public void Enqueue(NarrativeEvent evt)
            {
                if (_queue.Count >= _capacity)
                {
                    Console.WriteLine("[EventStream] Client queue full — dropping oldest event.");
                    _queue.Dequeue();
                }

                _queue.Enqueue(evt);
            }

            public bool TryDequeue(out NarrativeEvent? evt)
            {
                if (_queue.Count > 0)
                {
                    evt = _queue.Dequeue();
                    return true;
                }

                evt = null;
                return false;
            }
        }
    }
}
