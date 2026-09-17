using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using CAO.Infrastructure.Windows.Services;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Persistence;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.SystemInterop;
using CAO.Infrastructure.Windows.SystemRegistry;
using CAO.Core.Security;
using CAO.Shared.Security;
using CAO.Shared.Constants;

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
        builder.Services.AddSingleton<CAO.Core.Interfaces.IDnsConfigurationProvider, CAO.Infrastructure.Networking.WmiDnsConfigurationProvider>();
        // FASE 12 real: el journal y el gate de recuperación pendiente deben
        // existir en producción, no solo en tests. El detectLive se resuelve
        // de forma perezosa porque el motor aún no existe al registrar.
        var journal = new FileTransactionJournal();
        var snapshotStore = new FileSnapshotStore();
        OptimizationEngine? engineRef = null;
        var recovery = new CrashRecoveryService(
            journal,
            snapshotStore,
            optimizationId => engineRef is null
                ? OptimizationState.Unknown
                : engineRef.Detect(optimizationId));
        builder.Services.AddSingleton<ITransactionJournal>(journal);
        builder.Services.AddSingleton(recovery);
        builder.Services.AddSingleton<OptimizationEngine>(services =>
        {
            var engine = new OptimizationEngine(
                new RegistryAccessor(),
                new WmiRestorePointService(),
                snapshotStore,
                new JsonHistoryLogger(),
                new ServiceManager(),
                new CAO.Infrastructure.Windows.Execution.SystemCommandGateway(),
                services.GetRequiredService<ISystemContextProvider>(),
                journal, () => recovery.HasPendingRecovery(), null,
                services.GetRequiredService<CAO.Core.Interfaces.IDnsConfigurationProvider>());
            engineRef = engine;
            return engine;
        });
        builder.Services.AddSingleton<IPrivilegedCallerAuthorizer, AdministratorsOnlyAuthorizer>();
        builder.Services.AddHostedService<PrivilegedPipeService>();
        builder.Services.AddWindowsService(options => options.ServiceName = BuildConstants.ServiceName);
        return builder.Build().RunAsync();
    }
}
