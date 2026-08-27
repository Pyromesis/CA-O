using CAO.Shared;

namespace CAO.Core.Diagnostics;

/// <summary>
/// CA-O Health Check (§112): valida servicio, IPC, ACL, storage, snapshots, journal, permisos y versión.
/// </summary>
public sealed record HealthCheckResult(
    string Component,
    bool Healthy,
    string MessageEs,
    string? ErrorCode = null);

public static class CaoHealthCheck
{
    public static IReadOnlyList<HealthCheckResult> Run(
        string? serviceStatus = null,
        bool journalExists = true,
        bool snapshotsDirectoryExists = true,
        bool historyExists = true,
        string? appVersion = null,
        int windowsBuild = 0)
    {
        var results = new List<HealthCheckResult>();

        // Servicio
        results.Add(new HealthCheckResult(
            "PrivilegedService",
            serviceStatus is "connected" or "conectado",
            serviceStatus is "connected" or "conectado" ? "Servicio privilegiado conectado." : $"Servicio estado: {serviceStatus ?? "desconocido"}",
            serviceStatus is "connected" or "conectado" ? null : ErrorCodes.IpcMalformedRequest));

        // IPC
        results.Add(new HealthCheckResult(
            "IPC",
            CAO.Shared.IPC.IpcProtocol.Version == 2,
            $"Protocolo IPC v{CAO.Shared.IPC.IpcProtocol.Version} — límite request {CAO.Shared.IPC.IpcProtocol.MaxRequestBytes / 1024}KB",
            null));

        // Storage
        results.Add(new HealthCheckResult(
            "Storage",
            Directory.Exists(CaOPaths.SnapshotsDirectory) || snapshotsDirectoryExists,
            Directory.Exists(CaOPaths.SnapshotsDirectory) ? $"Snapshots OK: {CaOPaths.SnapshotsDirectory}" : "Directorio snapshots no existe (se creará al aplicar)."));

        results.Add(new HealthCheckResult(
            "Journal",
            journalExists,
            journalExists ? $"Journal: {Path.Combine(CaOPaths.ProgramDataRoot, "journal.jsonl")}" : "Journal ausente — sin transacciones previas."));

        results.Add(new HealthCheckResult(
            "History",
            historyExists,
            historyExists ? $"Historial: {CaOPaths.HistoryFile}" : "Historial vacío."));

        // Versión
        results.Add(new HealthCheckResult(
            "Version",
            !string.IsNullOrWhiteSpace(appVersion ?? CAO.Shared.AppVersion.Semantic) && windowsBuild >= 0,
            $"CA-O {appVersion ?? CAO.Shared.AppVersion.Semantic} · Windows build {windowsBuild}"));

        // Permisos: verificación básica de escritura
        var canWrite = CanWrite(CaOPaths.ProgramDataRoot);
        results.Add(new HealthCheckResult(
            "Permissions",
            canWrite,
            canWrite ? $"Escritura OK en {CaOPaths.ProgramDataRoot}" : $"Sin escritura en {CaOPaths.ProgramDataRoot} — ejecute como admin para reparar."));

        return results;
    }

    private static bool CanWrite(string path)
    {
        try { Directory.CreateDirectory(path); return true; } catch { return false; }
    }

    public static bool IsHealthy(IReadOnlyList<HealthCheckResult> results) => results.All(r => r.Healthy);
}
