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
                services.GetRequiredService<CAO.Core.Interfaces.IDnsConfigurationProvider>(),
                // CAO-BUG-2026-10-06 (N-1): sin inyectar el gestor de recuperación
                // el motor no puede cerrar una transacción pendiente, y con
                // HasPendingRecovery en true cualquier apply o revert devolvía
                // CAO-TXN-004 sin salida posible.
                recovery: recovery);
            engineRef = engine;
            return engine;
        });
        builder.Services.AddSingleton<IPrivilegedCallerAuthorizer, AdministratorsOnlyAuthorizer>();
        // CAO-BUG-2026-10-06 (F2): el gestor de recuperación se registraba pero
        // nadie lo consultaba. Este hosted service lo revisa al arrancar y deja el
        // detalle en el registro del servicio. Se registra ANTES del pipe para que
        // la revisión empiece antes de aceptar peticiones.
        builder.Services.AddHostedService<RecoveryStartupReporter>();
        builder.Services.AddHostedService<PrivilegedPipeService>();
        builder.Services.AddWindowsService(options => options.ServiceName = BuildConstants.ServiceName);
        return builder.Build().RunAsync();
    }
}
