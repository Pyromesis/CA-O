using System.Dynamic;
using System.Runtime.InteropServices;
using CAO.Infrastructure.SystemInterop;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Mapeo puro de ofertas WUApi a DTOs (sin COM real: ExpandoObject).
/// La orquestación COM vive en el servicio y se prueba en vivo.
/// </summary>
public sealed class WuDriverMappingTests
{
    private static dynamic Offer(
        string id = "11111111-2222-3333-4444-555555555555",
        string title = "Intel - MEDIA - 1.2.3.4",
        object? kbs = null,
        long size = 12345678,
        bool reboot = true)
    {
        dynamic identity = new ExpandoObject();
        identity.UpdateID = id;
        dynamic offer = new ExpandoObject();
        offer.Identity = identity;
        offer.Title = title;
        offer.KBArticleIDs = kbs ?? new List<object> { "5012345" };
        offer.MaxDownloadSize = size;
        offer.RebootRequired = reboot;
        return offer;
    }

    [Fact]
    public void MapUpdateInfo_MapsAllFields()
    {
        var info = WindowsUpdateDrivers.MapUpdateInfo(Offer());

        Assert.Equal("11111111-2222-3333-4444-555555555555", info.UpdateId);
        Assert.Equal("Intel - MEDIA - 1.2.3.4", info.Title);
        Assert.Equal("KB5012345", info.Kb);
        Assert.Equal(12345678, info.SizeBytes);
        Assert.True(info.RebootRequired);
    }

    [Fact]
    public void MapUpdateInfo_ToleratesMissingOptionalFields()
    {
        dynamic offer = new ExpandoObject();
        dynamic identity = new ExpandoObject();
        identity.UpdateID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        offer.Identity = identity;
        offer.Title = null;
        offer.KBArticleIDs = new List<object>();
        offer.MaxDownloadSize = 0L;
        offer.RebootRequired = false;

        var info = WindowsUpdateDrivers.MapUpdateInfo(offer);

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", info.UpdateId);
        Assert.Equal(string.Empty, info.Title);
        Assert.Equal(string.Empty, info.Kb);
        Assert.False(info.RebootRequired);
    }

    [Fact]
    public void AnyRebootRequired_DetectsFlag()
    {
        Assert.True(WindowsUpdateDrivers.AnyRebootRequired(new List<object> { Offer(reboot: false), Offer(reboot: true) }));
        Assert.False(WindowsUpdateDrivers.AnyRebootRequired(new List<object> { Offer(reboot: false) }));
        Assert.False(WindowsUpdateDrivers.AnyRebootRequired(new List<object>()));
    }

    // CA2201: probar el mapeo de HResult exige instancias reales de COMException.
#pragma warning disable CA2201
    [Theory]
    [InlineData(unchecked((int)0x80070005), "Windows Update denegó")]
    [InlineData(unchecked((int)0x80072EE7), "Sin conexión")]
    [InlineData(unchecked((int)0x8024402C), "Sin conexión")]
    public void DescribeFailure_MapsKnownHResults(int hresult, string expectedStart)
    {
        Assert.StartsWith(expectedStart, WindowsUpdateDrivers.DescribeFailure(new COMException("boom", hresult)));
    }

    [Fact]
    public void DescribeFailure_UnknownHResultShowsHex()
    {
        var text = WindowsUpdateDrivers.DescribeFailure(new COMException("boom", unchecked((int)0x80240055)));
        Assert.Contains("80240055", text);
    }

    [Fact]
    public void DescribeFailure_NonComShowsType()
    {
        Assert.Contains("InvalidOperationException", WindowsUpdateDrivers.DescribeFailure(new InvalidOperationException("nope")));
    }
#pragma warning restore CA2201

    /// <summary>
    /// Regresión CAO-TXN-003: la colección se crea por su propio ProgID
    /// (IUpdateSession no tiene CreateUpdateCollection). Vivo pero sin
    /// efectos: solo crea objetos COM vacíos, sin red ni instalación.
    /// </summary>
    [Fact]
    public void ComFactories_CreateSessionSearcherAndCollection()
    {
        object? session = null;
        object? collection = null;
        try
        {
            dynamic dynSession = WindowsUpdateDrivers.CreateSession();
            session = dynSession;
            dynSession.ClientApplicationID = "CA-O Tests";
            dynamic searcher = dynSession.CreateUpdateSearcher();
            Assert.NotNull((object)searcher);
            try { if (Marshal.IsComObject(searcher)) Marshal.FinalReleaseComObject(searcher); } catch { }

            dynamic dynCollection = WindowsUpdateDrivers.CreateCollection();
            collection = dynCollection;
            Assert.Equal(0, (int)dynCollection.Count);
        }
        finally
        {
            try { if (collection is not null && Marshal.IsComObject(collection)) Marshal.FinalReleaseComObject(collection); } catch { }
            try { if (session is not null && Marshal.IsComObject(session)) Marshal.FinalReleaseComObject(session); } catch { }
        }
    }
}
