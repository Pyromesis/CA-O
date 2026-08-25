namespace CAO.Shared;

/// <summary>Quality of the evidence behind an optimization (spec 9).</summary>
public enum Confidence
{
    Unknown,
    Low,
    Medium,
    High,
}

/// <summary>
/// How the change relates to anti-cheat software (spec 49). Detection is
/// conservative: absence of known conflict is never presented as a guarantee.
/// </summary>
public enum AntiCheatImpact
{
    /// <summary>No interaction expected with anti-cheat components.</summary>
    None,

    /// <summary>No conflict documented today; re-evaluated per release.</summary>
    NoKnownConflict,

    /// <summary>Documented or plausible interference; blocked by default.</summary>
    PotentialConflict,

    /// <summary>The change REMOVES something an anti-cheat requires.</summary>
    RequiredSecurityFeature,

    /// <summary>Known to break specific anti-cheats.</summary>
    Incompatible,

    Unknown,
}
