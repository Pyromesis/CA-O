using CAO.Core.Abstractions;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Exact registry round-trips (FASE 8): capture → mutate → restore → compare
/// for EVERY supported kind, including REG_EXPAND_SZ unexpanded, REG_BINARY
/// bytes and REG_MULTI_SZ elements. Rollback never infers kinds.
/// </summary>
public sealed class RegistryExactRoundTripTests
{
    private const string KeyPath = @"SOFTWARE\CA-O\RoundTrip";
    private const string ValueName = "V";

    public static TheoryData<string, object?, RegistryValueKind2> AllKinds() => new()
    {
        { "REG_SZ", "hola mundo", RegistryValueKind2.String },
        { "REG_EXPAND_SZ", "%SystemRoot%\\temp", RegistryValueKind2.ExpandString },
        { "REG_BINARY", (byte[])[0x00, 0xFF, 0x10, 0x7F], RegistryValueKind2.Binary },
        { "REG_DWORD", 42, RegistryValueKind2.DWord },
        { "REG_QWORD", 4_000_000_000L, RegistryValueKind2.QWord },
        { "REG_MULTI_SZ", new[] { "uno", "dos", "tres" }, RegistryValueKind2.MultiString },
        { "REG_NONE-absent", null, RegistryValueKind2.None },
    };

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void CaptureMutateRestorePreservesExactValueAndKind(
        string label, object? original, RegistryValueKind2 kind)
    {
        var registry = new MemoryRegistry();
        if (original is not null)
        {
            registry.SetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, original, kind);
        }

        // CAPTURE
        var raw = registry.GetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, out var capturedKind);
        var entry = new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(), KeyPath, ValueName, raw, Existed: original is not null)
        { Kind = capturedKind };

        // MUTATE (a different value of a different kind)
        registry.SetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName,
            "mutated", RegistryValueKind2.String);

        // RESTORE via SetValueRaw with the DECLARED kind
        if (entry.Existed)
        {
            registry.SetValueRaw(RegistryHive2.CurrentUser, entry.KeyPath, entry.ValueName,
                entry.Value, entry.Kind);
        }
        else
        {
            registry.DeleteValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        }

        // COMPARE exact semantics
        var restored = registry.GetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, out var restoredKind);
        Assert.Equal(entry.Existed, restored is not null);
        if (!entry.Existed)
        {
            Assert.Null(restored);
            return;
        }

        Assert.Equal(kind, restoredKind);
        Assert.True(entry.SemanticallyEquals(new RegistrySnapshotEntry(
            entry.Hive, entry.KeyPath, entry.ValueName, restored, true) { Kind = restoredKind }),
            $"El valor restaurado no coincide exactamente para {label}.");
    }

    [Fact]
    public void ExpandStringIsCapturedUnexpanded()
    {
        var registry = new MemoryRegistry();
        registry.SetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName,
            "%ProgramData%\\CA-O", RegistryValueKind2.ExpandString);

        var raw = registry.GetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, out var kind);

        Assert.Equal(RegistryValueKind2.ExpandString, kind);
        Assert.Equal("%ProgramData%\\CA-O", raw); // NOT expanded to C:\ProgramData\...
    }

    [Fact]
    public void BinaryRoundTripIsByteExact()
    {
        var registry = new MemoryRegistry();
        var bytes = (byte[])Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        registry.SetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, bytes, RegistryValueKind2.Binary);

        var raw = registry.GetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, out var kind);

        Assert.Equal(RegistryValueKind2.Binary, kind);
        Assert.IsType<byte[]>(raw);
        Assert.True(((byte[])raw!).AsSpan().SequenceEqual(bytes));
    }
}
