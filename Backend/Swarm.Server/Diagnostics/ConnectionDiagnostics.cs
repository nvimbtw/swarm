using System.Diagnostics;

namespace Swarm.Server.Diagnostics;

// One instance is shared by the HTTP and WebSocket controllers through dependency injection.
public sealed class ConnectionDiagnostics
{
    private const int SendSampleLimit = 100;
    private const int DisconnectionHistoryLimit = 20;
    private const double SlowSendThresholdMilliseconds = 20;

    private readonly object _gate = new();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly Dictionary<Guid, Connection> _connections = new();
    private readonly Queue<ConnectionSnapshot> _recentDisconnections = new();
    private readonly Queue<double> _sendDurations = new();
    private long _totalConnections;
    private long _messagesSent;
    private long _messagesReceived;

    public Guid OpenConnection()
    {
        lock (_gate)
        {
            Guid id = Guid.NewGuid();
            _connections.Add(id, new Connection());
            _totalConnections++;
            return id;
        }
    }

    public void RecordSent(Guid id, double durationMilliseconds)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(id, out Connection? connection))
            {
                return;
            }

            connection.MessagesSent++;
            _messagesSent++;
            _sendDurations.Enqueue(durationMilliseconds);
            if (_sendDurations.Count > SendSampleLimit)
            {
                _sendDurations.Dequeue();
            }
        }
    }

    public void RecordReceived(Guid id)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(id, out Connection? connection))
            {
                connection.MessagesReceived++;
                _messagesReceived++;
            }
        }
    }

    public void CloseConnection(Guid id)
    {
        lock (_gate)
        {
            if (!_connections.Remove(id, out Connection? connection))
            {
                return;
            }

            _recentDisconnections.Enqueue(connection.Snapshot(id, DateTimeOffset.UtcNow));
            if (_recentDisconnections.Count > DisconnectionHistoryLimit)
            {
                _recentDisconnections.Dequeue();
            }
        }
    }

    public DiagnosticsSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var activeConnections = new ConnectionSnapshot[_connections.Count];
            int index = 0;
            foreach (var entry in _connections)
            {
                activeConnections[index++] = entry.Value.Snapshot(entry.Key);
            }

            // Copy the samples so the calculation module receives an ordinary double array.
            double[] measurements = _sendDurations.ToArray();
            var sendDuration = new SendDurationSnapshot(
                measurements.Length,
                SendSampleLimit,
                Statistics.Average(measurements),
                Statistics.Maximum(measurements),
                SlowSendThresholdMilliseconds,
                Statistics.CountAbove(measurements, SlowSendThresholdMilliseconds));

            long uptimeSeconds = (long)_uptime.Elapsed.TotalSeconds;
            return new DiagnosticsSnapshot(
                _startedAt,
                uptimeSeconds,
                DurationFormatter.Format(uptimeSeconds),
                activeConnections.Length,
                _totalConnections,
                _messagesSent,
                _messagesReceived,
                sendDuration,
                activeConnections,
                _recentDisconnections.ToArray());
        }
    }

    private sealed class Connection
    {
        private readonly Stopwatch _duration = Stopwatch.StartNew();

        public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;
        public long MessagesSent { get; set; }
        public long MessagesReceived { get; set; }

        public ConnectionSnapshot Snapshot(Guid id, DateTimeOffset? disconnectedAt = null)
        {
            long durationSeconds = (long)_duration.Elapsed.TotalSeconds;
            return new ConnectionSnapshot(
                id,
                ConnectedAt,
                disconnectedAt,
                durationSeconds,
                DurationFormatter.Format(durationSeconds),
                MessagesSent,
                MessagesReceived);
        }
    }
}
