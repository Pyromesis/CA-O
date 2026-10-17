using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.GameNetwork;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Shared;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// 2.2.19: optimizaciones de input lag y red para juegos (roundtrip exacto
/// con MemoryRegistry) y la lógica pura de la página Red gaming
/// (estadísticas de latencia, diagnóstico de ruta, validación de destino).
/// </summary>
public sealed class InputLagAndGameNetworkTests
{
    private const string KernelKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";

    public static TheoryData<string> NewIds() => new()
    {
        "enable-global-timer-resolution",
        "mmcss-games-task-priority",
        "disable-accessibility-hotkeys",
        "keyboard-fast-repeat",
        "prioritize-game-traffic-qos",
        "disable-energy-efficient-ethernet",
    };

    [Theory]
    [MemberData(nameof(NewIds))]
    public void NewOptimizationsAreInProductionAndInTheDefaultBatch(string id)
    {
        Assert.Contains(OptimizationCatalog.All, o => o.Definition.Id == id);
        Assert.Contains(CatalogProjections.BatchDefault, o => o.Definition.Id == id);
        var definition = OptimizationCatalog.All.First(o => o.Definition.Id == id).Definition;
        Assert.False(string.IsNullOrWhiteSpace(definition.TooltipEs));
        Assert.True(definition.Risk <= RiskLevel.Low, $"{id}: debe ser de riesgo bajo");
        Assert.Equal(AntiCheatImpact.None, definition.AntiCheatImpact);
    }

    [Theory]
    [InlineData("enable-global-timer-resolution")]
    [InlineData("mmcss-games-task-priority")]
    [InlineData("disable-accessibility-hotkeys")]
    [InlineData("keyboard-fast-repeat")]
    [InlineData("prioritize-game-traffic-qos")]
    public async Task RegistryOptimizationsRoundTripExactly(string id)
    {
        var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == id);
        var registry = new MemoryRegistry();
        // Un valor previo real que el revert debe devolver tal cual.
        registry.SetValue(RegistryHive2.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
            "Priority", 2, RegistryValueKind2.DWord);
        var context = new OptimizationContext { Registry = registry };

        Assert.Equal(OptimizationState.NotApplied, optimization.Detect(registry));
        var snapshot = optimization.Capture(registry);
        Assert.True((await optimization.ApplyAsync(context)).Success);
        Assert.Equal(OptimizationState.AppliedByCao, optimization.Detect(registry));
        Assert.True((await optimization.VerifyAsync(context)).Verified);

        Assert.True((await optimization.RevertAsync(context, snapshot)).Success);
        Assert.Equal(OptimizationState.NotApplied, optimization.Detect(registry));
        Assert.Equal(2, registry.GetValue(RegistryHive2.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority"));
    }

    [Fact]
    public async Task GlobalTimerResolutionWritesTheKernelKeyAndNeedsWindows11()
    {
        var optimization = new EnableGlobalTimerResolution();
        var registry = new MemoryRegistry();
        await optimization.ApplyAsync(new OptimizationContext { Registry = registry });
        Assert.Equal(1, registry.GetValue(RegistryHive2.LocalMachine, KernelKey, "GlobalTimerResolutionRequests"));
        Assert.True(optimization.Definition.Flags.HasFlag(OptimizationFlags.RequiresReboot));

        Assert.False((await optimization.CheckPreconditionsAsync(Context(19045))).Passed);
        Assert.True((await optimization.CheckPreconditionsAsync(Context(26200))).Passed);
    }

    [Fact]
    public async Task QosPoliciesMarkEveryGameWithDscp46WithoutThrottling()
    {
        var registry = new MemoryRegistry();
        await new PrioritizeGameTrafficQos().ApplyAsync(new OptimizationContext { Registry = registry });

        Assert.Equal("1", registry.GetValue(RegistryHive2.LocalMachine, PrioritizeGameTrafficQos.NlaKey, "Do not use NLA"));
        foreach (var exe in PrioritizeGameTrafficQos.GameExecutables)
        {
            var key = PrioritizeGameTrafficQos.PoliciesKey + @"\" + PrioritizeGameTrafficQos.PolicyPrefix + Path.GetFileNameWithoutExtension(exe);
            Assert.Equal("1.0", registry.GetValue(RegistryHive2.LocalMachine, key, "Version"));
            Assert.Equal(exe, registry.GetValue(RegistryHive2.LocalMachine, key, "Application Name"));
            Assert.Equal("46", registry.GetValue(RegistryHive2.LocalMachine, key, "DSCP Value"));
            Assert.Equal("-1", registry.GetValue(RegistryHive2.LocalMachine, key, "Throttle Rate"));
        }
        Assert.Equal(PrioritizeGameTrafficQos.GameExecutables.Count,
            PrioritizeGameTrafficQos.GameExecutables.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task EnergyEfficientEthernetOnlyTouchesPhysicalAdaptersThatExposeIt()
    {
        const string netClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "DriverDesc", "Realtek PCIe GbE Family Controller", RegistryValueKind2.String);
        registry.SetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "*EEE", "1", RegistryValueKind2.String);
        registry.SetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "EnableGreenEthernet", "1", RegistryValueKind2.String);
        registry.SetValue(RegistryHive2.LocalMachine, netClass + @"\0002", "DriverDesc", "Hyper-V Virtual Ethernet Adapter", RegistryValueKind2.String);
        registry.SetValue(RegistryHive2.LocalMachine, netClass + @"\0002", "*EEE", "1", RegistryValueKind2.String);

        var optimization = new DisableEnergyEfficientEthernet();
        var context = new OptimizationContext { Registry = registry };
        var snapshot = optimization.Capture(registry);
        Assert.True((await optimization.ApplyAsync(context)).Success);
        Assert.Equal("0", registry.GetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "*EEE"));
        Assert.Equal("0", registry.GetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "EnableGreenEthernet"));
        Assert.Equal("1", registry.GetValue(RegistryHive2.LocalMachine, netClass + @"\0002", "*EEE"));
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "EEELinkAdvertisement"));

        await optimization.RevertAsync(context, snapshot);
        Assert.Equal("1", registry.GetValue(RegistryHive2.LocalMachine, netClass + @"\0001", "*EEE"));
    }

    /// <summary>
    /// NetworkThrottlingIndex es el "network-throttling-index-hack" vetado (spec 75/100):
    /// retirado del catálogo en 2.2.20, pero sigue en AllLegacy para que Restaurar
    /// pueda deshacerlo en equipos que lo aplicaron con 2.2.19.
    /// </summary>
    [Fact]
    public void NetworkThrottlingIsRetiredButStillRevertible()
    {
        Assert.DoesNotContain(OptimizationCatalog.All, o => o.Definition.Id == "disable-network-throttling");
        Assert.Contains("disable-network-throttling", OptimizationCatalog.LegacyIds);
        Assert.Contains(OptimizationCatalog.AllLegacy, o => o.Definition.Id == "disable-network-throttling");
        Assert.False(OptimizationCatalog.RetiredAliases.ContainsKey("disable-network-throttling"));
    }

    // ---------- LatencyStats ----------

    [Fact]
    public void StatsComputeMedianJitterAndLoss()
    {
        var stats = LatencyStats.From([20, 22, null, 24, 20]);
        Assert.Equal(5, stats.Sent);
        Assert.Equal(4, stats.Received);
        Assert.Equal(20, stats.LossPercent);
        Assert.Equal(21, stats.MedianMs);
        Assert.Equal(20, stats.MinMs);
        // |22-20| + |24-22| + |20-24| = 8, entre 3 diferencias.
        Assert.Equal(2.7, stats.JitterMs);
    }

    [Fact]
    public void AllLostIsUnreachable()
    {
        var stats = LatencyStats.From([null, null, null]);
        Assert.Equal(100, stats.LossPercent);
        Assert.Null(stats.MedianMs);
        Assert.Equal(ConnectionGrade.Unreachable, stats.Grade);
        Assert.Equal(LatencyStats.Empty, LatencyStats.From([]));
    }

    [Theory]
    [InlineData(new double[] { 10, 11, 10, 11 }, ConnectionGrade.Excellent)]
    [InlineData(new double[] { 50, 52, 50, 52 }, ConnectionGrade.Good)]
    [InlineData(new double[] { 90, 91, 90, 91 }, ConnectionGrade.Fair)]
    [InlineData(new double[] { 200, 201, 200, 201 }, ConnectionGrade.Poor)]
    [InlineData(new double[] { 20, 80, 20, 80 }, ConnectionGrade.Poor)]
    public void GradeFollowsLatencyAndJitter(double[] samples, ConnectionGrade expected)
    {
        Assert.Equal(expected, LatencyStats.From(samples.Select(s => (double?)s).ToList()).Grade);
    }

    // ---------- RouteDiagnosis ----------

    private static RouteHop Hop(int ttl, double? ms, double loss = 0, bool destination = false)
    {
        var samples = new List<double?>();
        var lost = (int)Math.Round(loss / 100 * 10);
        for (var i = 0; i < 10; i++) samples.Add(i < lost || ms is null ? null : ms);
        return new RouteHop(ttl, ms is null ? null : $"10.0.0.{ttl}", LatencyStats.From(samples), destination);
    }

    [Fact]
    public void SlowFirstHopBlamesTheLocalNetwork()
    {
        var verdict = RouteDiagnosis.Analyze([Hop(1, 45), Hop(2, 50), Hop(3, 60, destination: true)]);
        Assert.Equal(RouteVerdictKind.LocalNetwork, verdict.Kind);
        Assert.Equal(1, verdict.HopTtl);
    }

    [Fact]
    public void LossOnlyAtAnIntermediateRouterIsIgnored()
    {
        // El salto 3 limita ICMP hacia sí mismo pero el destino no pierde nada.
        var verdict = RouteDiagnosis.Analyze([Hop(1, 2), Hop(2, 8), Hop(3, 12, loss: 60), Hop(4, 15, destination: true)]);
        Assert.Equal(RouteVerdictKind.Healthy, verdict.Kind);
    }

    [Fact]
    public void LossThatReachesTheDestinationIsReportedWhereItStarts()
    {
        var verdict = RouteDiagnosis.Analyze([Hop(1, 2), Hop(2, 8), Hop(3, 12), Hop(4, 15, loss: 20), Hop(5, 18, loss: 20, destination: true)]);
        Assert.Equal(RouteVerdictKind.PathLoss, verdict.Kind);
        Assert.Equal(4, verdict.HopTtl);
    }

    [Fact]
    public void BigInheritedJumpIsReported()
    {
        var verdict = RouteDiagnosis.Analyze([Hop(1, 2), Hop(2, 9), Hop(3, 12), Hop(4, 130), Hop(5, 132, destination: true)]);
        Assert.Equal(RouteVerdictKind.LatencyJump, verdict.Kind);
        Assert.Equal(4, verdict.HopTtl);
    }

    [Fact]
    public void SilentDestinationStillAnalysesVisibleHops()
    {
        var withJump = RouteDiagnosis.Analyze([Hop(1, 2), Hop(2, 10), Hop(3, 140), Hop(4, null)]);
        Assert.Equal(RouteVerdictKind.LatencyJump, withJump.Kind);
        Assert.StartsWith("El destino no responde", withJump.MessageEs, StringComparison.Ordinal);

        var flat = RouteDiagnosis.Analyze([Hop(1, 2), Hop(2, 10), Hop(3, 14), Hop(4, null)]);
        Assert.Equal(RouteVerdictKind.DestinationUnreachable, flat.Kind);
    }

    [Fact]
    public void NoResponsesIsNoData()
    {
        Assert.Equal(RouteVerdictKind.NoData, RouteDiagnosis.Analyze([Hop(1, null), Hop(2, null)]).Kind);
    }

    // ---------- Destino personalizado ----------

    [Theory]
    [InlineData("1.2.3.4", "1.2.3.4", 443)]
    [InlineData("1.2.3.4:7000", "1.2.3.4", 7000)]
    [InlineData(" dynamodb.us-east-1.amazonaws.com ", "dynamodb.us-east-1.amazonaws.com", 443)]
    [InlineData("game.example.com:27015", "game.example.com", 27015)]
    public void CustomTargetAcceptsHostsAndPorts(string text, string host, int port)
    {
        Assert.True(GameRegionCatalog.TryParseCustomTarget(text, out var parsedHost, out var parsedPort));
        Assert.Equal(host, parsedHost);
        Assert.Equal(port, parsedPort);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2.3.4:0")]
    [InlineData("1.2.3.4:70000")]
    [InlineData("host;calc.exe")]
    [InlineData("a b")]
    [InlineData("..evil")]
    [InlineData("host:")]
    public void CustomTargetRejectsAnythingThatIsNotAHost(string? text)
    {
        Assert.False(GameRegionCatalog.TryParseCustomTarget(text, out _, out _));
    }

    [Fact]
    public void RegionCatalogHasUniqueIdsAndCloudHosts()
    {
        Assert.Equal(GameRegionCatalog.All.Count, GameRegionCatalog.All.Select(r => r.Id).Distinct().Count());
        Assert.All(GameRegionCatalog.All, r => Assert.EndsWith(".amazonaws.com", r.Host, StringComparison.Ordinal));
        Assert.NotNull(GameRegionCatalog.Find("SA-EAST-1"));
        Assert.Null(GameRegionCatalog.Find("nope"));
    }

    private static SystemContext Context(int build) => new()
    {
        WindowsBuild = build,
        WindowsEdition = "Pro",
        Architecture = "x64",
        CpuName = "cpu",
        CpuCores = 8,
        CpuLogicalProcessors = 16,
        RamGb = 32,
        MeasuredUtc = DateTime.UtcNow,
    };
}

/// <summary>2.2.20: Auto HDR verificable en 24H2+, QoS compartido y arreglos por comando.</summary>
public sealed class Release2220OptimizationTests
{
    [Theory]
    [InlineData(22631)]
    [InlineData(26200)]
    public async Task AutoHdrIsVerifiableOnEveryBuildAndKeepsOtherDirectXFlags(int build)
    {
        var previous = EnableAutoHdr.CurrentBuild;
        EnableAutoHdr.CurrentBuild = () => build;
        try
        {
            var registry = new MemoryRegistry();
            registry.SetValue(RegistryHive2.CurrentUser, EnableAutoHdr.DxKey, EnableAutoHdr.DxValue,
                "VRROptimizeEnable=1;GpuPreference=2;", RegistryValueKind2.String);
            var hdr = new EnableAutoHdr();
            var context = new OptimizationContext { Registry = registry };
            var snapshot = hdr.Capture(registry);

            Assert.Equal(OptimizationState.NotApplied, hdr.Detect(registry));
            await hdr.ApplyAsync(context);
            Assert.Equal(OptimizationState.AppliedByCao, hdr.Detect(registry));
            var combined = (string)registry.GetValue(RegistryHive2.CurrentUser, EnableAutoHdr.DxKey, EnableAutoHdr.DxValue)!;
            Assert.Contains("VRROptimizeEnable=1", combined);
            Assert.Contains("GpuPreference=2", combined);
            Assert.Contains("AutoHDREnable=1", combined);

            await hdr.RevertAsync(context, snapshot);
            Assert.Equal("VRROptimizeEnable=1;GpuPreference=2;",
                registry.GetValue(RegistryHive2.CurrentUser, EnableAutoHdr.DxKey, EnableAutoHdr.DxValue));
            Assert.Null(registry.GetValue(RegistryHive2.CurrentUser, EnableAutoHdr.GameBarKey, EnableAutoHdr.GameBarValue));
        }
        finally
        {
            EnableAutoHdr.CurrentBuild = previous;
        }
    }

    [Fact]
    public async Task RevertingVoiceQosKeepsNlaWhileGameQosIsActive()
    {
        var registry = new MemoryRegistry();
        var context = new OptimizationContext { Registry = registry };
        var game = new PrioritizeGameTrafficQos();
        var voice = new PrioritizeVoiceChatQos();

        var gameSnapshot = game.Capture(registry);
        await game.ApplyAsync(context);
        var voiceSnapshot = voice.Capture(registry);
        await voice.ApplyAsync(context);

        // Revertir el juego primero: su snapshot no tenía NLA, pero la voz lo necesita.
        await game.RevertAsync(context, gameSnapshot);
        Assert.Equal("1", registry.GetValue(RegistryHive2.LocalMachine, QosPolicyOptimization.NlaKey, QosPolicyOptimization.NlaValue));
        Assert.Equal(OptimizationState.AppliedByCao, voice.Detect(registry));

        await voice.RevertAsync(context, voiceSnapshot);
        Assert.Empty(registry.Store);
    }

    [Theory]
    [InlineData("renew-ip-address")]
    [InlineData("clear-arp-cache")]
    [InlineData("run-system-file-checker")]
    [InlineData("repair-windows-image")]
    [InlineData("restart-wlan-service")]
    [InlineData("restart-xbox-services")]
    [InlineData("restart-text-input-service")]
    [InlineData("rebuild-font-cache")]
    public void NewFixesLiveInSolucionarNotInTheBatch(string id)
    {
        Assert.Contains(CatalogProjections.RepairActions, o => o.Definition.Id == id);
        Assert.DoesNotContain(CatalogProjections.BatchDefault, o => o.Definition.Id == id);
        Assert.True(OptimizationCatalog.All.First(o => o.Definition.Id == id).Definition.Flags.HasFlag(OptimizationFlags.NotReversible));
    }

    [Theory]
    [InlineData("cleanup-steam-web-cache")]
    [InlineData("cleanup-epic-launcher-cache")]
    [InlineData("cleanup-battlenet-cache")]
    [InlineData("cleanup-game-launcher-logs")]
    [InlineData("cleanup-gpu-driver-installer-leftovers")]
    [InlineData("cleanup-windows-error-reports")]
    [InlineData("cleanup-old-system-logs")]
    [InlineData("cleanup-obs-logs")]
    [InlineData("cleanup-vscode-cache")]
    [InlineData("cleanup-developer-caches")]
    public void NewCleanupsAreFileCleanupsThatNeverRevert(string id)
    {
        var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == id);
        Assert.IsAssignableFrom<CAO.Core.Optimizations.Storage.TempFileCleanupOptimization>(optimization);
        Assert.True(optimization.Definition.Flags.HasFlag(OptimizationFlags.NotReversible));
        Assert.Equal(OptimizationCategory.Storage, optimization.Definition.Category);
    }

    [Fact]
    public async Task RenewIpContinuesWhenReleaseFailsOnStaticIp()
    {
        var calls = new List<SystemCommandKey>();
        var executor = new ScriptedExecutor(key =>
        {
            calls.Add(key);
            return key == SystemCommandKey.IpConfigRelease
                ? new PrivilegedCommandResult(1, string.Empty, "no DHCP", false)
                : new PrivilegedCommandResult(0, string.Empty, string.Empty, false);
        });
        var result = await new CAO.Core.Optimizations.Troubleshoot.RenewIpAddress()
            .ApplyAsync(new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor });
        Assert.True(result.Success);
        Assert.Equal([SystemCommandKey.IpConfigRelease, SystemCommandKey.IpConfigRenew, SystemCommandKey.IpConfigFlushDns], calls);
    }

    private sealed class ScriptedExecutor(Func<SystemCommandKey, PrivilegedCommandResult> script) : CAO.Core.Interfaces.IPrivilegedCommandExecutor
    {
        public Task<PrivilegedCommandResult> ExecuteAsync(SystemCommandKey key, IReadOnlyList<string> arguments, CancellationToken ct = default) =>
            Task.FromResult(script(key));
    }
}

/// <summary>CAO-BUG-2026-10-17: Restaurar un ajuste de energía AC tras reiniciar el servicio.</summary>
public sealed class PowerAcRevertAfterRestartTests
{
    [Fact]
    public async Task RevertWorksWithAFreshInstanceAfterApply()
    {
        var executor = new PowerCfgFake("0x00000001");
        var context = new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor };
        var applied = await new CAO.Core.Optimizations.Power.DisableUsbSelectiveSuspendAc().ApplyAsync(context);
        Assert.True(applied.Success);

        // Instancia nueva = servicio reiniciado: no queda nada en memoria.
        var fresh = new CAO.Core.Optimizations.Power.DisableUsbSelectiveSuspendAc();
        var reverted = await fresh.RevertAsync(context, fresh.Capture(context.Registry));
        Assert.True(reverted.Success, reverted.MessageEs);
        Assert.Contains(executor.Calls, c => c.EndsWith(" 1", StringComparison.Ordinal) && c.StartsWith("/setacvalueindex", StringComparison.Ordinal));
    }

    private sealed class PowerCfgFake(string current) : CAO.Core.Interfaces.IPrivilegedCommandExecutor
    {
        public List<string> Calls { get; } = [];

        public Task<PrivilegedCommandResult> ExecuteAsync(SystemCommandKey key, IReadOnlyList<string> arguments, CancellationToken ct = default)
        {
            Calls.Add(string.Join(' ', arguments));
            var stdout = key == SystemCommandKey.PowerCfgQueryAcValueIndex ? $"Current AC Power Setting Index: {current}" : string.Empty;
            return Task.FromResult(new PrivilegedCommandResult(0, stdout, string.Empty, false));
        }
    }
}
