using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.GameNetwork;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Shared;
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
        "disable-network-throttling",
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
    [InlineData("disable-network-throttling")]
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

    [Fact]
    public async Task NetworkThrottlingDetectsTheUnsignedMaximumAsApplied()
    {
        var registry = new MemoryRegistry();
        // Windows devuelve REG_DWORD 0xFFFFFFFF como int -1.
        registry.SetValue(RegistryHive2.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            "NetworkThrottlingIndex", -1, RegistryValueKind2.DWord);
        Assert.Equal(OptimizationState.AppliedByCao, new DisableNetworkThrottling().Detect(registry));
        await Task.CompletedTask;
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
