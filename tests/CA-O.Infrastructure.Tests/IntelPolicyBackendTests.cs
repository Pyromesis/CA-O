using CAO.Core.Interfaces;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Policy;
using CAO.Infrastructure.Overclock;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Exercises the CPU backend against a simulated powercfg. No elevation, no
/// power scheme and no CPU are touched, which is exactly why the backend was
/// split into a pure policy in Core and this thin executor in Infrastructure.
/// </summary>
public sealed class IntelPolicyBackendTests
{
    private const string ProcessorSubgroup = "54533251-82be-4824-96c1-47b60b740d00";
    private const string BoostMode = "be337241-0d82-4146-a960-4f3749d470c3";
    private const string MaxProcessorState = "bc5038f7-23e0-4960-96da-33abaf5935ec";

    private static readonly KnobId BoostKnob = IntelPowerPolicy.BoostModeKnob;
    private static readonly KnobId FrequencyKnob = IntelPowerPolicy.MaxFrequencyKnob;

    [Fact]
    public async Task Probe_ReportsPolicyOnly_WhenXtuIsNotInstalled()
    {
        FakePowerCfg power = new();
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        OverclockCapabilities caps = backend.Probe(CancellationToken.None);

        Assert.Equal(CpuBackendKind.PolicyOnly, caps.Cpu);
        Assert.Equal("intel-policy", caps.Details[0].BackendId);
        Assert.True(caps.Details[0].Available);
    }

    [Fact]
    public async Task Probe_ReportsXtuGuided_WhenEverythingIsPresent()
    {
        FakePowerCfg power = new();
        IntelPolicyBackend backend = new(power, FakeXtu.Guided("16.1.1.0"));

        OverclockCapabilities caps = backend.Probe(CancellationToken.None);

        Assert.Equal(CpuBackendKind.XtuGuided, caps.Cpu);
        Assert.Equal("16.1.1.0", caps.Details[0].DriverVersion);
    }

    [Fact]
    public async Task Probe_NeverAdvertisesAGpuOrMemoryBackend()
    {
        IntelPolicyBackend backend = new(new FakePowerCfg(), FakeXtu.Policy());

        OverclockCapabilities caps = backend.Probe(CancellationToken.None);

        Assert.Equal(GpuBackendKind.None, caps.Gpu);
        Assert.Equal(MemoryBackendKind.None, caps.Memory);
        Assert.All(caps.Knobs, knob => Assert.Equal(OverclockComponent.Cpu, knob.Component));
    }

    [Fact]
    public async Task ReadAsync_UnknownKnob_Fails_InsteadOfGuessing()
    {
        IntelPolicyBackend backend = new(new FakePowerCfg(), FakeXtu.Policy());

        KnobRead read = await backend.ReadAsync(new KnobId("nvidia.gpu.powerLimit"), CancellationToken.None);

        Assert.False(read.Success);
        Assert.Equal(IntelPolicyBackend.UnknownKnobReasonKey, read.FailureReasonKey);
    }

    [Fact]
    public async Task ReadAsync_ParsesTheAcIndex()
    {
        FakePowerCfg power = new();
        power.Values[MaxProcessorState] = 87;
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        KnobRead read = await backend.ReadAsync(FrequencyKnob, CancellationToken.None);

        Assert.True(read.Success);
        Assert.Equal(87d, read.Value.Scalar);
    }

    [Fact]
    public async Task ReadAsync_WhenTheCommandFails_ReportsFailure_NotZero()
    {
        FakePowerCfg power = new() { FailQueries = true };
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        KnobRead read = await backend.ReadAsync(FrequencyKnob, CancellationToken.None);

        Assert.False(read.Success);
        Assert.Equal(IntelPolicyBackend.ReadFailedReasonKey, read.FailureReasonKey);
    }

    [Fact]
    public async Task WriteAsync_SetsActivatesThenVerifies()
    {
        FakePowerCfg power = new();
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        KnobWriteResult result = await backend.WriteAsync(FrequencyKnob, KnobValue.FromScalar(75), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(75, power.Values[MaxProcessorState]);
        Assert.Contains(power.Commands, c => c.Key == SystemCommandKey.PowerCfgSetAcValueIndex);
        Assert.Contains(power.Commands, c => c.Key == SystemCommandKey.PowerCfgSetActiveCurrent);
        // The verify read must be the last command; otherwise a success would
        // mean nothing.
        Assert.Equal(SystemCommandKey.PowerCfgQueryAcValueIndex, power.Commands[^1].Key);
    }

    [Fact]
    public async Task WriteAsync_WhenActivationFails_DoesNotClaimTheChangeLanded()
    {
        FakePowerCfg power = new() { FailActivation = true };
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        KnobWriteResult result = await backend.WriteAsync(FrequencyKnob, KnobValue.FromScalar(75), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(IntelPolicyBackend.ActivationFailedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task WriteAsync_WhenTheReadBackDiffers_DoesNotClaimTheChangeLanded()
    {
        FakePowerCfg power = new() { IgnoreWrites = true };
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        KnobWriteResult result = await backend.WriteAsync(FrequencyKnob, KnobValue.FromScalar(75), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(IntelPolicyBackend.VerifyFailedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task WriteAsync_OutOfRangeValue_RunsNoCommandAtAll()
    {
        FakePowerCfg power = new();
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        KnobWriteResult result = await backend.WriteAsync(FrequencyKnob, KnobValue.FromScalar(5000), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(power.Commands);
    }

    [Fact]
    public async Task WriteAsync_WhenTheExecutorThrows_DegradesInsteadOfCrashingThePage()
    {
        IntelPolicyBackend backend = new(new ThrowingExecutor(), FakeXtu.Policy());

        KnobWriteResult result = await backend.WriteAsync(FrequencyKnob, KnobValue.FromScalar(75), CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RestoreAsync_TriesEveryKnob_EvenWhenTheFirstOneFails()
    {
        FakePowerCfg power = new();
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());
        UnitSnapshot snapshot = new(
            OverclockComponent.Cpu,
            [
                new KeyValuePair<KnobId, KnobValue>(FrequencyKnob, KnobValue.FromScalar(40)),
                new KeyValuePair<KnobId, KnobValue>(BoostKnob, KnobValue.FromScalar(0)),
            ],
            DateTimeOffset.UnixEpoch,
            DriverVersion: null);

        power.IgnoreWrites = true; // both restores fail their verification
        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        // Breaking on the first failure would have attempted one restore and
        // left the other knob silently wrong.
        Assert.Equal(2, power.Commands.Count(c => c.Key == SystemCommandKey.PowerCfgSetAcValueIndex));
        Assert.False(outcome.Success);
    }

    [Fact]
    public async Task SnapshotAsync_OmitsKnobsItCouldNotRead()
    {
        FakePowerCfg power = new();
        power.Values.Remove(MaxProcessorState);
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        UnitSnapshot snapshot = await backend.SnapshotAsync(CancellationToken.None);

        Assert.Single(snapshot.Values);
        Assert.Equal(BoostKnob, snapshot.Values[0].Key);
    }

    [Fact]
    public void CanWrite_IsFalse_WhenTheCurrentValueCannotBeRead()
    {
        FakePowerCfg power = new() { FailQueries = true };
        IntelPolicyBackend backend = new(power, FakeXtu.Policy());

        Assert.False(backend.CanWrite(FrequencyKnob));
    }

    [Fact]
    public void CanWrite_IsTrue_WhenTheCurrentValueIsProvable()
    {
        IntelPolicyBackend backend = new(new FakePowerCfg(), FakeXtu.Policy());

        Assert.True(backend.CanWrite(FrequencyKnob));
        Assert.False(backend.CanWrite(new KnobId("nvidia.gpu.powerLimit")));
    }

    [Fact]
    public async Task ReadDetails_AlwaysStatesThatThisIsPolicyAndNotAnOverclock()
    {
        IntelPolicyBackend backend = new(new FakePowerCfg(), FakeXtu.Guided("16.1.1.0"));

        IReadOnlyList<OverclockReading> readings = await backend.ReadDetailsAsync(CancellationToken.None);

        OverclockReading scope = Assert.Single(readings, r => r.LabelKey == "overclock_cpu_scope");
        Assert.Equal("policy", scope.Value);
        Assert.Equal("overclock_cpu_scope_note", scope.UnavailableReasonKey);
    }

    private sealed class FakePowerCfg : IPrivilegedCommandExecutor
    {
        public Dictionary<string, int> Values { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            [BoostMode] = 1,
            [MaxProcessorState] = 100,
        };

        public List<(SystemCommandKey Key, IReadOnlyList<string> Args)> Commands { get; } = [];

        public bool FailQueries { get; set; }

        public bool FailActivation { get; set; }

        public bool IgnoreWrites { get; set; }

        public Task<PrivilegedCommandResult> ExecuteAsync(
            SystemCommandKey key,
            IReadOnlyList<string> arguments,
            CancellationToken ct = default)
        {
            Commands.Add((key, arguments));

            if (key == SystemCommandKey.PowerCfgQueryAcValueIndex)
            {
                if (FailQueries || arguments.Count < 4)
                {
                    return Task.FromResult(new PrivilegedCommandResult(1, string.Empty, "simulated failure", false));
                }

                string setting = arguments[3];
                if (!Values.TryGetValue(setting, out int value))
                {
                    // A real powercfg query for a setting this scheme does not
                    // carry returns no index at all. Returning a default here
                    // would let the snapshot capture a value that was never
                    // read.
                    return Task.FromResult(new PrivilegedCommandResult(0, "no index in this scheme", string.Empty, false));
                }

                return Task.FromResult(new PrivilegedCommandResult(
                    0, $"Current AC Power Setting Index: 0x{value:x8}", string.Empty, false));
            }

            if (key == SystemCommandKey.PowerCfgSetActiveCurrent)
            {
                return Task.FromResult(FailActivation
                    ? new PrivilegedCommandResult(1, string.Empty, "simulated activation failure", false)
                    : new PrivilegedCommandResult(0, string.Empty, string.Empty, false));
            }

            if (key == SystemCommandKey.PowerCfgSetAcValueIndex && arguments.Count == 5 &&
                !int.TryParse(arguments[4], out _))
            {
                return Task.FromResult(new PrivilegedCommandResult(1, string.Empty, "bad value", false));
            }

            if (key == SystemCommandKey.PowerCfgSetAcValueIndex && !IgnoreWrites)
            {
                Values[arguments[3]] = int.Parse(arguments[4]);
            }

            return Task.FromResult(new PrivilegedCommandResult(0, string.Empty, string.Empty, false));
        }
    }

    private sealed class ThrowingExecutor : IPrivilegedCommandExecutor
    {
        public Task<PrivilegedCommandResult> ExecuteAsync(
            SystemCommandKey key,
            IReadOnlyList<string> arguments,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("simulated executor failure");
    }

    private sealed class FakeXtu : IXtuProbe
    {
        private readonly XtuProbeFacts _facts;

        private FakeXtu(XtuProbeFacts facts) => _facts = facts;

        public static FakeXtu Policy() => new(XtuProbeFacts.Unknown);

        public static FakeXtu Guided(string version) => new(new XtuProbeFacts(
            "GenuineIntel", false, true, true, version, true));

        public XtuProbeFacts Probe(CancellationToken ct) => _facts;
    }

    private static void AssertProcessorSubgroup(string subgroup) => Assert.Equal(ProcessorSubgroup, subgroup);
}
