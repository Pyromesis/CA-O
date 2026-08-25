namespace CAO.Shared;

public sealed record SecurityFeatureState(string Name, bool? Enabled, string Evidence);

public sealed record SecurityDiagnosticsReport(
    IReadOnlyList<SecurityFeatureState> Features,
    bool VanguardDetected,
    DateTime TimestampUtc);