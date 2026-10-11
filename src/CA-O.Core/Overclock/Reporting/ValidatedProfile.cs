using System.Text.Json.Serialization;

namespace CAO.Core.Overclock.Reporting;

/// <summary>
/// A tuning run that reached its ceiling without a fault, kept as the machine's
/// validated answer for one knob. This is the only thing a game profile may
/// reference: a value no run has produced is a guess, and CA-O does not ship
/// guesses (design 6.9).
/// </summary>
public sealed record ValidatedOverclockProfile(
    [property: JsonPropertyName("knob")] KnobId Knob,
    [property: JsonPropertyName("component")] OverclockComponent Component,
    [property: JsonPropertyName("stableValue")] double StableValue,
    [property: JsonPropertyName("stableCeiling")] double? StableCeiling,
    [property: JsonPropertyName("validatedUtc")] DateTimeOffset ValidatedAtUtc,
    [property: JsonPropertyName("driverVersion")] string? DriverVersion,
    [property: JsonPropertyName("pointsPassed")] int PointsPassed);

/// <summary>Where validated profiles live. One file per knob; saving twice keeps the latest.</summary>
public interface IValidatedProfileStore
{
    Task<IReadOnlyList<ValidatedOverclockProfile>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(ValidatedOverclockProfile profile, CancellationToken cancellationToken);
}
