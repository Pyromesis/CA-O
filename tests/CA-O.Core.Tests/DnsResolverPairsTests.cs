using CAO.Shared.Networking;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Plan 2026-09-29 §2.3: un par mezclado (p. ej. ISP + 1.1.1.1) nunca se
/// aplica tal cual: el secundario se corrige al compañero canónico, al
/// hermano /24 o a null.
/// </summary>
public sealed class DnsResolverPairsTests
{
    [Fact]
    public void KnownPair_StaysUntouched()
    {
        var (primary, secondary, corrected) = DnsResolverPairs.NormalizePair("8.8.8.8", "8.8.4.4");
        Assert.Equal("8.8.8.8", primary);
        Assert.Equal("8.8.4.4", secondary);
        Assert.False(corrected);
    }

    [Fact]
    public void MixedPublicPair_SecondaryCorrectedToCanonicalCompanion()
    {
        var (primary, secondary, corrected) = DnsResolverPairs.NormalizePair("1.1.1.1", "8.8.8.8");
        Assert.Equal("1.1.1.1", primary);
        Assert.Equal("1.0.0.1", secondary);
        Assert.True(corrected);
    }

    [Fact]
    public void IspPlusPublic_NeverMixed_SecondaryBecomesNullWithoutSibling()
    {
        var (primary, secondary, corrected) = DnsResolverPairs.NormalizePair("192.168.1.10", "1.1.1.1");
        Assert.Equal("192.168.1.10", primary);
        Assert.Null(secondary);
        Assert.True(corrected);
    }

    [Fact]
    public void IspWithSameSlash24Sibling_KeepsSibling()
    {
        var (primary, secondary, corrected) = DnsResolverPairs.NormalizePair(
            "192.168.1.10", "192.168.1.11", ["192.168.1.11", "1.1.1.1"]);
        Assert.Equal("192.168.1.10", primary);
        Assert.Equal("192.168.1.11", secondary);
        Assert.False(corrected);
    }

    [Fact]
    public void LonePrimary_CompletesToSameProviderPair()
    {
        var (primary, secondary, _) = DnsResolverPairs.NormalizePair("9.9.9.9", null);
        Assert.Equal("9.9.9.9", primary);
        Assert.Equal("149.112.112.112", secondary);
    }

    [Fact]
    public void IsSameProvider_DistinguishesMixing()
    {
        Assert.True(DnsResolverPairs.IsSameProvider("1.1.1.1", "1.0.0.1"));
        Assert.False(DnsResolverPairs.IsSameProvider("1.1.1.1", "8.8.8.8"));
        Assert.False(DnsResolverPairs.IsSameProvider("192.168.1.10", "1.1.1.1"));
    }
}
