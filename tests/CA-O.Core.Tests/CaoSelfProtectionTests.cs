using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Startup;
using CAO.Core.Security;
using CAO.Shared.Constants;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Protección de los recursos propios de CA-O: por nombre canónico, por
/// binario dentro de la instalación/ProgramData y por fabricante. Ninguna
/// optimización genérica puede retirar el canal SYSTEM ni el arranque de la UI.
/// </summary>
public sealed class CaoSelfProtectionTests
{
    [Theory]
    [InlineData(BuildConstants.ServiceName)]
    [InlineData("cao.privileged")]
    [InlineData(BuildConstants.ServiceDisplayName)]
    public void OwnServiceIsRecognizedByName(string name) =>
        Assert.True(CaoSelfProtection.IsOwnService(name, imagePath: null));

    [Theory]
    [InlineData(@"C:\Program Files\CA-O\service\CA-O.Privileged.exe")]
    [InlineData(@"C:\Program Files\CA-O\service\custom-host.exe")]
    [InlineData(@"C:\ProgramData\CA-O\anything.exe")]
    [InlineData("\"C:\\Program Files\\CA-O\\ui\\CA-O.UI.exe\" --tray")]
    [InlineData(@"C:\Program Files\CA-O\service\CA-O.Privileged.exe -k")]
    public void OwnBinariesAreRecognized(string command) =>
        Assert.True(CaoSelfProtection.IsOwnExecutable(command));

    [Theory]
    [InlineData(@"C:\Program Files\Contoso\agent.exe")]
    [InlineData("\"C:\\Program Files\\Contoso\\updater.exe\" /silent")]
    [InlineData(@"D:\Games\Launcher\launcher.exe")]
    public void ThirdPartyBinariesAreNotOurs(string command) =>
        Assert.False(CaoSelfProtection.IsOwnExecutable(command));

    [Fact]
    public void OwnStartupEntriesAreProtectedByNamePathOrPublisher()
    {
        Assert.True(CaoSelfProtection.IsOwnStartupEntry("CA-O", @"C:\Other\helper.exe", null));
        Assert.True(CaoSelfProtection.IsOwnStartupEntry("Cualquiera", @"C:\Program Files\CA-O\ui\CA-O.UI.exe", null));
        Assert.True(CaoSelfProtection.IsOwnStartupEntry("Cualquiera", @"C:\Other\app.exe", BuildConstants.Publisher));
        Assert.False(CaoSelfProtection.IsOwnStartupEntry("Contoso", @"C:\Program Files\Contoso\app.exe", "Contoso"));
    }

    [Fact]
    public void OwnScheduledTasksAreProtectedByNameOrAction()
    {
        Assert.True(CaoSelfProtection.IsOwnScheduledTask(@"\CA-O\Arranque", @"C:\Other\x.exe"));
        Assert.True(CaoSelfProtection.IsOwnScheduledTask(
            @"\Contoso\Tarea", @"C:\Program Files\CA-O\service\CA-O.Privileged.exe"));
        Assert.False(CaoSelfProtection.IsOwnScheduledTask(
            @"\Contoso\Actualizador", @"C:\Program Files\Contoso\updater.exe"));
    }

    [Fact]
    public async Task RunKeyOptimizationKeepsCaoEntries()
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        var registry = new RunKeyRegistry();
        registry.Set(RegistryHive2.LocalMachine, runKey, "CA-O", @"C:\Program Files\CA-O\ui\CA-O.UI.exe");
        registry.Set(RegistryHive2.LocalMachine, runKey, "ContosoUpdater", @"C:\Program Files\Contoso\updater.exe");

        var sut = new DisableUnnecessaryStartupApps();

        var preview = await sut.PreviewAsync(registry);
        Assert.Single(preview.Lines);
        Assert.Contains("ContosoUpdater", preview.Lines[0].Target, StringComparison.OrdinalIgnoreCase);

        await sut.ApplyAsync(new OptimizationContext { Registry = registry });

        Assert.NotNull(registry.GetValue(RegistryHive2.LocalMachine, runKey, "CA-O"));
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, runKey, "ContosoUpdater"));
    }

    [Fact]
    public async Task RunKeyOptimizationKeepsEntriesWhoseCommandIsCaoUi()
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        var registry = new RunKeyRegistry();
        registry.Set(RegistryHive2.LocalMachine, runKey, "InicioCAO", @"C:\Program Files\CA-O\ui\CA-O.UI.exe");

        var sut = new DisableHeavyStartupApps();
        await sut.ApplyAsync(new OptimizationContext { Registry = registry });

        Assert.NotNull(registry.GetValue(RegistryHive2.LocalMachine, runKey, "InicioCAO"));
    }

    private sealed class RunKeyRegistry : IRegistryAccessor
    {
        private readonly Dictionary<(RegistryHive2 Hive, string Key, string Name), (object Value, RegistryValueKind2 Kind)> _values = new();

        public void Set(RegistryHive2 hive, string key, string name, object value,
            RegistryValueKind2 kind = RegistryValueKind2.String) =>
            _values[(hive, key, name)] = (value, kind);

        public RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName) =>
            _values.TryGetValue((hive, keyPath, valueName), out var entry) ? entry.Kind : RegistryValueKind2.None;

        public object? GetValue(RegistryHive2 hive, string keyPath, string valueName) =>
            _values.TryGetValue((hive, keyPath, valueName), out var entry) ? entry.Value : null;

        public object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind)
        {
            if (_values.TryGetValue((hive, keyPath, valueName), out var entry))
            {
                kind = entry.Kind;
                return entry.Value;
            }
            kind = RegistryValueKind2.None;
            return null;
        }

        public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
            Set(hive, keyPath, valueName, value, kind);

        public void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
            Set(hive, keyPath, valueName, value, kind);

        public bool DeleteValue(RegistryHive2 hive, string keyPath, string valueName) =>
            _values.Remove((hive, keyPath, valueName));

        public IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath) =>
            _values.Keys
                .Where(k => k.Hive == hive && k.Key.Equals(keyPath, StringComparison.OrdinalIgnoreCase))
                .Select(k => k.Name)
                .ToList();

        public IReadOnlyList<string> GetSubKeyNames(RegistryHive2 hive, string keyPath) => [];
    }
}
