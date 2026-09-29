using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Performance;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Plan 2026-09-29 §3: limpieza RAM con mecanismos reales y mensajes honestos.</summary>
public sealed class MemoryRamCleanupTests
{
    private static OptimizationContext Context() =>
        new() { Registry = new MemoryRegistry(), Executor = null, Services = null };

    [Fact]
    public void Definition_HasHonestMetadata()
    {
        var definition = new CleanupMemoryRam().Definition;
        Assert.Equal("cleanup-memory-ram", definition.Id);
        Assert.Equal(OptimizationCategory.Performance, definition.Category);
        Assert.True(definition.Flags.HasFlag(OptimizationFlags.NotReversible));
        Assert.False(string.IsNullOrWhiteSpace(definition.NameEs));
        Assert.False(string.IsNullOrWhiteSpace(definition.TooltipEs));
        foreach (var text in new[] { definition.DescriptionEs, definition.DescriptionEn, definition.TooltipEs })
        {
            Assert.DoesNotContain("FPS", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("%", text, StringComparison.Ordinal);
            Assert.DoesNotContain("garantiz", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Detect_IsAlwaysRunnableMaintenance()
    {
        Assert.Equal(OptimizationState.NotApplied, new CleanupMemoryRam().Detect(new MemoryRegistry()));
    }

    [Fact]
    public async Task Preview_DescribesBothMechanisms()
    {
        var preview = await new CleanupMemoryRam().PreviewAsync(new MemoryRegistry());
        Assert.Equal("cleanup-memory-ram", preview.OptimizationId);
        Assert.NotEmpty(preview.Lines);
    }

    [Fact]
    public async Task Apply_ThenVerify_PassesWithHonestMessage()
    {
        var optimization = new CleanupMemoryRam();
        var before = await optimization.VerifyAsync(Context());
        Assert.Equal(VerificationStatus.Unknown, before.Status);

        var result = await optimization.ApplyAsync(Context());
        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.MessageEs));

        var verify = await optimization.VerifyAsync(Context());
        Assert.Equal(VerificationStatus.Passed, verify.Status);
        Assert.Equal(OptimizationState.AppliedByCao, verify.ObservedState);
    }

    [Fact]
    public async Task Revert_IsMaintenanceNoop()
    {
        var result = await new CleanupMemoryRam().RevertAsync(Context(), new OptimizationSnapshot());
        Assert.True(result.Success);
    }
}
