using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;
using Xunit.Abstractions;

namespace CAO.Integration.Tests;

/// <summary>
/// Persistence ACL checks (FASE 14). The write-denial assertion only runs
/// when the test host is NOT elevated (an elevated host would bypass the
/// Users restriction); on elevated CI hosts the script
/// scripts\harden-data-acls.ps1 is validated by the E2E pass instead.
/// </summary>
public sealed class DataAclTests
{
    private readonly ITestOutputHelper _output;

    public DataAclTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    [Fact]
    public void HardenedRootDeniesUserWrites_WhenNotElevated()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O");
        if (!Directory.Exists(root))
        {
            _output.WriteLine("SKIP: app never ran here; nothing to assert yet.");
            return;
        }

        if (IsElevated())
        {
            _output.WriteLine("SKIP: elevated processes bypass deny-on-Users; covered by E2E.");
            return;
        }

        if (!File.Exists(Path.Combine(root, "acls-hardened.flag")))
        {
            _output.WriteLine("SKIP: policy not applied yet on this machine; script is the gate.");
            return;
        }

        var acl = new DirectoryInfo(root).GetAccessControl();
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var usersWrite = rules.Cast<FileSystemAccessRule>().Any(rule =>
            rule.IdentityReference.Value.StartsWith("S-1-5-32-545", StringComparison.Ordinal) && // BUILTIN\Users
            (rule.FileSystemRights & (FileSystemRights.Write | FileSystemRights.CreateFiles |
                                      FileSystemRights.CreateDirectories | FileSystemRights.Modify)) != 0 &&
            rule.AccessControlType == AccessControlType.Allow);

        Assert.False(usersWrite,
            "BUILTIN\\Users tiene permisos de escritura en %ProgramData%\\CA-O; ejecute scripts\\harden-data-acls.ps1.");
    }
}
