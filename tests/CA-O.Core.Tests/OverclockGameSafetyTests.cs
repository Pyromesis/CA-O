using System.Text.RegularExpressions;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// The overclock module is anti-cheat safe by construction, not by policy. These tests
/// read the module's own source and fail the build the moment that stops being true.
/// </summary>
/// <remarks>
/// The Hypertune page markets itself as operating "at the hardware level, never touching
/// game files". That claim is cheap to write and expensive to keep, so this module keeps
/// it in machine-checkable form: no driver handle, no process injection, no game
/// directory, and no launch options smuggled into somebody else's process.
/// </remarks>
public sealed class OverclockGameSafetyTests
{
    private static readonly string[] ForbiddenTokens =
    [
        // Process injection and privilege escalation. Every one of these is something an
        // anti-cheat treats as an attack rather than as tuning.
        "OpenProcess",
        "WriteProcessMemory",
        "ReadProcessMemory",
        "CreateRemoteThread",
        "NtCreateThreadEx",
        "RtlAdjustPrivilege",
        "SetWindowsHookEx",
        "ZwSetInformationThread",
        "Process.Start",

        // Kernel driver handles. A tuning profile must go through vendor user-mode APIs.
        "DeviceIoControl",
        "\\\\.\\",

        // Game directories. Writing there is cheating; reading one to find a game is the
        // detection provider's job and lives outside this module.
        "steamapps",
        "Riot Games",
        "Epic Games",
        "GameDirectory",
        "InstallDirectory",

        // Fields that were considered and deliberately removed from the game profile.
        "LaunchOptions",
        "ProcessPriority",
    ];

    [Fact]
    public void NoOverclockSource_ReachesIntoAnotherProcessOrAKernelDriver()
    {
        var offenders = new List<string>();

        foreach (var file in OverclockSources())
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)} mentions {token}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheModule_ActuallyHasSourcesToCheck()
    {
        // A guard that finds nothing because the path is wrong guards nothing.
        Assert.True(OverclockSources().Count >= 12, "the overclock module should be larger than this");
    }

    [Fact]
    public void NoOverclockSource_StartsAnExternalProcess()
    {
        var starts = new List<string>();

        foreach (var file in OverclockSources())
        {
            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"\bProcess\s*\.\s*Start", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                starts.Add(Path.GetFileName(file));
            }
        }

        Assert.Empty(starts);
    }

    [Fact]
    public void TheGameProfile_TypeCarriesNoLaunchOrPriorityField()
    {
        var properties = typeof(Core.Overclock.Gaming.GameOverclockProfile)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("LaunchOptions", properties);
        Assert.DoesNotContain("ProcessPriority", properties);
    }

    private static IReadOnlyList<string> OverclockSources()
    {
        var root = RepoRoot();
        var enumeration = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            RecurseSubdirectories = true,
        };

        var files = new List<string>();
        foreach (var relative in new[]
        {
            Path.Combine("src", "CA-O.Core", "Overclock"),
            Path.Combine("src", "CA-O.Infrastructure", "Overclock"),
        })
        {
            var directory = Path.Combine(root, relative);
            Assert.True(Directory.Exists(directory), $"missing {directory}");
            files.AddRange(Directory.EnumerateFiles(directory, "*.cs", enumeration));
        }

        files.Add(Path.Combine(root, "src", "CA-O.UI", "ViewModels", "OverclockViewModel.cs"));
        files.Add(Path.Combine(root, "src", "CA-O.UI", "OverclockPage.xaml.cs"));
        return files;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CA-O.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("CA-O.sln was not found above the test output directory.");
    }
}