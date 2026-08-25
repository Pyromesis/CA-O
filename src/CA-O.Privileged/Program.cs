using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Services;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Persistence;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.SystemInterop;

namespace CAO.Privileged;

internal static class Program
{
    public static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<ISystemContextProvider>(_ => new SystemContextProvider(
            new WmiSystemInfoProvider(),
            new SecurityDiagnosticsProvider(),
            new AntiCheatScanProvider()));
        builder.Services.AddSingleton<OptimizationEngine>(services => new OptimizationEngine(
            new RegistryAccessor(),
            new WmiRestorePointService(),
            new FileSnapshotStore(),
            new JsonHistoryLogger(),
            new ServiceManager(),
            new ProcessRunner(),
            services.GetRequiredService<ISystemContextProvider>()));
        builder.Services.AddHostedService<PrivilegedPipeService>();
        builder.Services.AddWindowsService(options => options.ServiceName = "CA-O Privileged Service");
        return builder.Build().RunAsync();
    }
}
