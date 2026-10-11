using System.Management;
using System.ServiceProcess;
using CAO.Core.Abstractions;
using CAO.Core.Overclock.Policy;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Reads what is actually installed, and nothing more.
///
/// Every probe is wrapped so a missing WMI class, a denied service query or
/// an absent registry key yields "unknown", which the policy maps to the
/// conservative verdict. The bias is deliberate: when in doubt CA-O offers
/// power policy and tells the user why XTU is not being offered, because the
/// alternative is walking someone into XTU and letting it disable their
/// protections because CA-O said "supported".
/// </summary>
public sealed class SystemXtuProbe(IRegistryAccessor registry) : IXtuProbe
{
    private const string XtuRegistryKey = @"SOFTWARE\Intel\XTU";
    private const string XtuServiceName = "XTUService";

    private static readonly string[] XtuInstallPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Intel", "XTU", "XTUService.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Intel", "XTU", "XTUService.exe"),
    ];

    public XtuProbeFacts Probe(CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (ct.IsCancellationRequested)
        {
            return XtuProbeFacts.Unknown;
        }

        string vendor = ReadCpuVendor();
        bool isLaptop = DetectLaptop();
        bool installed = DetectInstalled();
        bool servicePresent = DetectService();

        return new XtuProbeFacts(
            CpuVendor: vendor,
            IsLaptop: isLaptop,
            XtuInstalled: installed,
            XtuServicePresent: servicePresent,
            XtuVersion: installed ? ReadVersion() : null,
            XtuOverclockingSupported: installed && DetectOverclockingSupported());
    }

    private static string ReadCpuVendor() => QueryFirstString(
        "SELECT Manufacturer FROM Win32_Processor",
        "Manufacturer");

    private static bool DetectLaptop() =>
        QueryFirstInt("SELECT PCSystemType FROM Win32_ComputerSystem", "PCSystemType") == 2 ||
        ExistsWmiClass("SELECT InstanceName FROM Win32_Battery");

    private bool DetectInstalled()
    {
        try
        {
            if (registry.GetSubKeyNames(RegistryHive2.LocalMachine, XtuRegistryKey).Count > 0)
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Fall through to the filesystem check below.
        }

        foreach (string path in XtuInstallPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool DetectService()
    {
        try
        {
            using ServiceController controller = new(XtuServiceName);
            return controller.Status != ServiceControllerStatus.Stopped;
        }
        catch (InvalidOperationException)
        {
            // Service is not installed at all.
            return false;
        }
#pragma warning disable CA1031 // a probe must never throw into the page
        catch (Exception)
        {
            return false;
        }
#pragma warning restore CA1031
    }

    private string? ReadVersion()
    {
        foreach (string valueName in (ReadOnlySpan<string>)["DisplayVersion", "Version", "ProductVersion"])
        {
            try
            {
                object? raw = registry.GetValue(RegistryHive2.LocalMachine, XtuRegistryKey, valueName);
                string? text = raw?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Only a positive registry signal counts. Absent evidence means CA-O does
    /// not claim XTU overclocking support, which is the reversible answer.
    /// </summary>
    private bool DetectOverclockingSupported()
    {
        try
        {
            object? raw = registry.GetValue(RegistryHive2.LocalMachine, XtuRegistryKey, "OverclockingSupported");
            return raw switch
            {
                int value => value != 0,
                long value => value != 0L,
                string text => string.Equals(text, "1", StringComparison.Ordinal) ||
                               string.Equals(text, "true", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    private static string QueryFirstString(string query, string property)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(query);
            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    object? value = item[property];
                    if (value is not null)
                    {
                        return value.ToString() ?? string.Empty;
                    }
                }
            }
        }
        catch (ManagementException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static int QueryFirstInt(string query, string property)
    {
        string raw = QueryFirstString(query, property);
        return int.TryParse(raw, out int value) ? value : 0;
    }

    private static bool ExistsWmiClass(string query)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(query);
            using ManagementObjectCollection results = searcher.Get();
            return results.Count > 0;
        }
        catch (ManagementException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}
