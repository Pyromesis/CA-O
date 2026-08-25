namespace CAO.Shared;

public sealed record NetworkEndpointMeasurement(
    string Endpoint,
    string Kind,
    int Attempts,
    int SuccessfulAttempts,
    double? MedianLatencyMs,
    double? JitterMs);

public sealed record NetworkDiagnosticsReport(
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> DnsServers,
    IReadOnlyList<NetworkEndpointMeasurement> Measurements,
    DateTime TimestampUtc);