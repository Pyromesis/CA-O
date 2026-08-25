using CAO.Shared;

namespace CAO.Core.Diagnostics;

/// <summary>
/// Health engine (spec 15/85-88): scores every dimension from MEASURED
/// inputs only. A dimension without data yields Score=null / IsMeasured=false
/// — the engine never invents numbers, and the score never depends on how
/// many tweaks are applied.
/// </summary>
public static class HealthEngine
{
    public static SystemDiagnosticReport Evaluate(
        SystemContext? context = null,
        NetworkDiagnosticsReport? network = null,
        StorageDiagnosticsReport? storage = null,
        DriverDiagnosticsReport? drivers = null,
        SecurityDiagnosticsReport? security = null,
        InterruptPressureReport? interrupts = null)
    {
        var scores = new List<HealthScore>
        {
            ScoreSystem(context),
            ScoreThermals(context),
            ScoreNetwork(network),
            ScoreStorage(storage),
            ScoreDrivers(drivers),
            ScoreSecurity(security),
            ScoreInput(interrupts),
            ScoreGaming(context, interrupts),
            new(HealthDimension.Startup, null, false, "Requiere analizar entradas de inicio y su impacto observado."),
            new(HealthDimension.Stability, null, false, "Requiere histórico de estabilidad (fallos/crashes) aún no disponible."),
        };

        var findings = CollectFindings(context, network, storage, drivers, security, interrupts);
        return new SystemDiagnosticReport(scores, findings);
    }

    // ---------------- scorers ----------------

    private static HealthScore ScoreSystem(SystemContext? context)
    {
        if (context is null || context.WindowsBuild <= 0 || context.RamGb <= 0)
        {
            return new(HealthDimension.System, null, false, "Faltan datos básicos de Windows o memoria.");
        }

        var score = 90;
        var reason = "Datos básicos correctos.";
        if (context.RamGb < 8)
        {
            score -= 20;
            reason += " Memoria limitada para cargas modernas.";
        }

        return new(HealthDimension.System, score, true, reason);
    }

    private static HealthScore ScoreThermals(SystemContext? context) => context?.ThermalState switch
    {
        ThermalState.Nominal => new(HealthDimension.Thermals, 95, true, "Temperaturas dentro de rango nominal."),
        ThermalState.Warm => new(HealthDimension.Thermals, 75, true, "Temperaturas elevadas bajo carga."),
        ThermalState.Throttling => new(HealthDimension.Thermals, 40, true, "Throttling térmico activo: cuello de botella real."),
        _ => new(HealthDimension.Thermals, null, false, "El sistema no expone sensores térmicos legibles."),
    };

    private static HealthScore ScoreNetwork(NetworkDiagnosticsReport? network)
    {
        if (network is null || network.Measurements.Count == 0)
        {
            return new(HealthDimension.Network, null, false, "Sin mediciones de red ejecutadas.");
        }

        var gateway = network.Measurements.FirstOrDefault(m => m.Kind == "Gateway") ??
                      network.Measurements[0];
        if (gateway.MedianLatencyMs is null)
        {
            return new(HealthDimension.Network, 30, true, "La puerta de enlace no responde a ICMP.");
        }

        var lossRatio = (double)(gateway.Attempts - gateway.SuccessfulAttempts) / Math.Max(1, gateway.Attempts);
        var jitter = gateway.JitterMs ?? 0;
        var score = 100;
        score -= (int)Math.Round(lossRatio * 200);                       // cada 5% de pérdida -10
        if (jitter > 15) score -= 20; else if (jitter > 6) score -= 10;
        if (gateway.MedianLatencyMs > 50) score -= 10;

        var reason = $"Gateway {gateway.MedianLatencyMs:0} ms, jitter {jitter:0.0} ms, pérdida {lossRatio * 100:0}%.";
        return new(HealthDimension.Network, Math.Clamp(score, 0, 100), true, reason);
    }

    private static HealthScore ScoreStorage(StorageDiagnosticsReport? storage)
    {
        var system = storage?.Volumes.FirstOrDefault(v => v.IsSystemVolume);
        if (system is null)
        {
            return new(HealthDimension.Storage, null, false, "Sin datos del volumen del sistema.");
        }

        var freePercent = system.TotalBytes == 0 ? 0 : (double)system.FreeBytes / system.TotalBytes * 100;
        var score = freePercent switch
        {
            >= 25 => 95,
            >= 15 => 80,
            >= 10 => 65,
            >= 5 => 45,
            _ => 25,
        };
        return new(HealthDimension.Storage, score, true,
            $"Espacio libre en C:: {freePercent:0.0}% ({system.FileSystem}).");
    }

    private static HealthScore ScoreDrivers(DriverDiagnosticsReport? drivers)
    {
        if (drivers is null || drivers.Drivers.Count == 0)
        {
            return new(HealthDimension.Drivers, null, false, "Sin enumeración de drivers ejecutada.");
        }

        var problems = drivers.Drivers.Count(d => d.ProblemCode != 0);
        var unsigned = drivers.Drivers.Count(d => d.IsSigned == false);
        var score = Math.Clamp(98 - problems * 12 - unsigned * 6, 0, 100);
        return new(HealthDimension.Drivers, score, true,
            $"{drivers.Drivers.Count} drivers; {problems} con problema, {unsigned} sin firma.");
    }

    private static HealthScore ScoreSecurity(SecurityDiagnosticsReport? security)
    {
        if (security is null || security.Features.Count == 0)
        {
            return new(HealthDimension.Security, null, false, "Sin lectura de características de seguridad.");
        }

        var known = security.Features.Where(f => f.Enabled is not null).ToList();
        if (known.Count == 0)
        {
            return new(HealthDimension.Security, null, false, "Estado de seguridad indeterminado.");
        }

        var enabled = known.Count(f => f.Enabled!.Value);
        var score = Math.Clamp((int)Math.Round((double)enabled / known.Count * 100), 0, 100);
        return new(HealthDimension.Security, score, true,
            $"{enabled}/{known.Count} protecciones verificadas activas.");
    }

    private static HealthScore ScoreInput(InterruptPressureReport? interrupts)
    {
        if (interrupts is null)
        {
            return new(HealthDimension.Input, null, false, "Sin muestreo DPC/ISR ejecutado.");
        }

        var score = interrupts.TotalMaxDpcPercent switch
        {
            < 5 => 95,
            < 10 => 85,
            < 25 => 65,
            _ => 40,
        };
        return new(HealthDimension.Input, score, true,
            $"DPC máx {_Format(interrupts.TotalMaxDpcPercent)}% — severidad {interrupts.SeverityEs}.");
    }

    private static HealthScore ScoreGaming(SystemContext? context, InterruptPressureReport? interrupts)
    {
        var measuredParts = new List<int>();
        if (context is { ThermalState: ThermalState.Nominal or ThermalState.Warm })
        {
            measuredParts.Add(context.ThermalState == ThermalState.Nominal ? 95 : 75);
        }

        if (interrupts is not null)
        {
            AddGamingPart(measuredParts, interrupts.TotalMaxDpcPercent);
        }

        if (measuredParts.Count == 0)
        {
            return new(HealthDimension.Gaming, null, false,
                "Requiere térmicas + DPC medidos para puntuar Gaming Readiness.");
        }

        var score = (int)Math.Round(measuredParts.Average());
        return new(HealthDimension.Gaming, score, true,
            "Compuesto de térmicas e interrupciones medidas.");
    }

    private static void AddGamingPart(List<int> parts, double dpcPercent)
    {
        if (dpcPercent < 5) parts.Add(95);
        else if (dpcPercent < 10) parts.Add(85);
        else if (dpcPercent < 25) parts.Add(65);
        else parts.Add(40);
    }

    // ---------------- findings ----------------

    private static List<DiagnosticFinding> CollectFindings(
        SystemContext? context,
        NetworkDiagnosticsReport? network,
        StorageDiagnosticsReport? storage,
        DriverDiagnosticsReport? drivers,
        SecurityDiagnosticsReport? security,
        InterruptPressureReport? interrupts)
    {
        var findings = new List<DiagnosticFinding>();

        if (context is { ThermalState: ThermalState.Throttling })
        {
            findings.Add(new(HealthDimension.Thermals, DiagnosticSeverity.Critical,
                "thermal-throttling", "Cuello de botella térmico detectado: resuelva refrigeración antes de optimizar."));
        }

        if (context is { RamGb: > 0 and < 8 })
        {
            findings.Add(new(HealthDimension.System, DiagnosticSeverity.Warning,
                "low-memory", "Memoria limitada: no se recomienda desactivar la compresión de memoria."));
        }

        var gw = network?.Measurements.FirstOrDefault(m => m.Kind == "Gateway");
        if (gw is { MedianLatencyMs: not null } && (gw.JitterMs ?? 0) > 15)
        {
            findings.Add(new(HealthDimension.Network, DiagnosticSeverity.Warning,
                "high-jitter", "Jitter alto hacia la gateway: puede degradar el juego online (revise Wi-Fi/router)."));
        }

        var sysVolume = storage?.Volumes.FirstOrDefault(v => v.IsSystemVolume);
        if (sysVolume is not null && sysVolume.TotalBytes > 0 &&
            (double)sysVolume.FreeBytes / sysVolume.TotalBytes < 0.10)
        {
            findings.Add(new(HealthDimension.Storage, DiagnosticSeverity.Warning,
                "low-disk", "Menos del 10% libre en C:: afecta actualizaciones y rendimiento del SSD."));
        }

        if (drivers is not null)
        {
            foreach (var driver in drivers.Drivers.Where(d => d.ProblemCode != 0).Take(3))
            {
                findings.Add(new(HealthDimension.Drivers, DiagnosticSeverity.Warning,
                    "driver-problem", $"Driver con problema: {driver.Name} (código {driver.ProblemCode})."));
            }
        }

        if (security is not null)
        {
            foreach (var feature in security.Features.Where(f => f.Enabled == false).Take(4))
            {
                findings.Add(new(HealthDimension.Security, DiagnosticSeverity.Information,
                    "security-off", $"{feature.Name} está desactivado."));
            }
        }

        if (interrupts is { TotalMaxDpcPercent: >= 10 })
        {
            findings.Add(new(HealthDimension.Input, DiagnosticSeverity.Warning,
                "dpc-pressure", $"Presión DPC elevada ({_Format(interrupts.TotalMaxDpcPercent)}%): investigue drivers USB/red/audio."));
        }

        return findings;
    }

    private static string _Format(double value) => value.ToString("0.##");
}
