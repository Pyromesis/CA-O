using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Engine;

public static class SystemHealthAnalyzer
{
    public static SystemDiagnosticReport Analyze(SystemInfoReport system)
    {
        var scores = new List<HealthScore>
        {
            ScoreSystem(system),
            ScoreStorage(system),
            new(HealthDimension.Gaming, null, false, "Requiere diagnóstico específico de GPU, pantalla y juego."),
            new(HealthDimension.Security, null, false, "Requiere comprobar Secure Boot, TPM, VBS, HVCI y Defender."),
            new(HealthDimension.Network, null, false, "Requiere medir gateway, DNS, jitter y pérdida de paquetes."),
            new(HealthDimension.Drivers, null, false, "Requiere enumerar y verificar firmas y estados de drivers."),
            new(HealthDimension.Thermals, null, false, "Requiere muestras térmicas bajo carga."),
            new(HealthDimension.Startup, null, false, "Requiere analizar entradas de inicio y su impacto observado."),
        };

        var findings = new List<DiagnosticFinding>();
        if (system.RamGb > 0 && system.RamGb < 8)
        {
            findings.Add(new(HealthDimension.System, DiagnosticSeverity.Warning, "low-memory", "La memoria instalada puede limitar algunas cargas; no se recomienda desactivar la compresión de memoria."));
        }

        if (system.IsLaptop)
        {
            findings.Add(new(HealthDimension.System, DiagnosticSeverity.Information, "portable-device", "Equipo portátil detectado; las recomendaciones deben conservar batería y temperatura."));
        }

        return new SystemDiagnosticReport(scores, findings);
    }

    private static HealthScore ScoreSystem(SystemInfoReport system)
    {
        if (system.WindowsBuild <= 0 || string.IsNullOrWhiteSpace(system.CpuName) || system.RamGb <= 0)
        {
            return new(HealthDimension.System, null, false, "Faltan datos básicos de Windows, CPU o memoria.");
        }

        return new(HealthDimension.System, 100, true, "Datos básicos del sistema disponibles; no se han detectado fallos generales en este nivel." );
    }

    private static HealthScore ScoreStorage(SystemInfoReport system) =>
        system.HasSsd
            ? new(HealthDimension.Storage, null, false, "SSD detectado; faltan salud, espacio libre, temperatura y latencia para puntuar almacenamiento.")
            : new(HealthDimension.Storage, null, false, "El tipo de almacenamiento no permite puntuar salud sin métricas adicionales.");
}