namespace Swarm.Server.Diagnostics;

public sealed record DiagnosticsSnapshot(
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    string Uptime,
    int ActiveConnectionCount,
    long TotalConnections,
    long MessagesSent,
    long MessagesReceived,
    SendDurationSnapshot SendDuration,
    ConnectionSnapshot[] ActiveConnections,
    ConnectionSnapshot[] RecentDisconnections);

public sealed record SendDurationSnapshot(
    int SampleCount,
    int SampleLimit,
    double? AverageMilliseconds,
    double? MaximumMilliseconds,
    double SlowThresholdMilliseconds,
    int CountAboveThreshold);

public sealed record ConnectionSnapshot(
    Guid Id,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? DisconnectedAt,
    long DurationSeconds,
    string Duration,
    long MessagesSent,
    long MessagesReceived);
