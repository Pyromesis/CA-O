using System.Diagnostics;
using System.Runtime.InteropServices;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>
/// Libera memoria en espera en el servicio SYSTEM: recorta el working set de
/// cada proceso (EmptyWorkingSet) y purga la standby list
/// (NtSetSystemInformation). Efecto temporal: Windows vuelve a llenar la
/// standby con el uso normal. No promete velocidad: solo deja más memoria
/// disponible de inmediato. Mantenimiento no reversible.
/// </summary>
public sealed class CleanupMemoryRam : IOptimization
{
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    private bool _ranThisSession;
    private long _freedBytes;
    private int _trimmedProcesses;
    private bool _standbyPurged;

    public OptimizationDefinition Definition => new()
    {
        Id = "cleanup-memory-ram",
        NameEs = "Liberar memoria en espera (RAM)",
        NameEn = "Free standby memory (RAM)",
        DescriptionEs = "Recorta working sets y purga la lista en espera para dejar memoria disponible. Efecto temporal, sin impacto en juegos.",
        DescriptionEn = "Trims working sets and purges the standby list to leave memory available. Temporary effect, no gaming impact.",
        TooltipEs = "EmptyWorkingSet por proceso + purga de standby en el servicio SYSTEM. Libera memoria disponible al momento; Windows la vuelve a ocupar con el uso. No acelera juegos ni apps.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("memory-ram=mantenimiento puntual (sin estado previo que guardar)");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var before = AvailPhysBytes();

        int trimmed = 0;
        foreach (var process in Process.GetProcesses())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                try
                {
                    if (EmptyWorkingSet(process.Handle)) trimmed++;
                }
                catch { /* proceso protegido o ya salido: se omite */ }
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }

        // Purga de standby: puede fallar sin privilegio suficiente; es
        // best-effort y se informa con honestidad en el mensaje.
        var standbyPurged = TryPurgeStandby(ct);

        var after = AvailPhysBytes();
        _freedBytes = after - before;
        _trimmedProcesses = trimmed;
        _standbyPurged = standbyPurged;
        _ranThisSession = true;

        var freedMb = _freedBytes / 1024 / 1024;
        var freedLabel = _freedBytes <= 0
            ? "sin cambio medible en disponible"
            : $"{freedMb} MB disponibles";
        var standbyLabel = standbyPurged ? "standby purgada" : "standby no purgada (sin privilegio suficiente)";
        return Task.FromResult(OperationResult.Ok(
            $"Memoria liberada: {freedLabel} (working set recortado en {trimmed} procesos, {standbyLabel}). " +
            "Efecto temporal: no acelera el equipo."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La liberación de memoria no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (!_ranThisSession)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }
        return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
            $"Verificado: working set recortado en {_trimmedProcesses} procesos, " +
            $"{(_standbyPurged ? "standby purgada" : "standby intacta")}, " +
            $"delta disponible {_freedBytes / 1024 / 1024} MB."));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Memory",
                    Target = "working set de cada proceso",
                    Before = "memoria privada en RAM",
                    After = "páginas recortadas (se recuperan bajo demanda)",
                },
                new PreviewLine
                {
                    Kind = "Memory",
                    Target = "standby list del sistema",
                    Before = "caché en espera",
                    After = "purgada (se vuelve a llenar con el uso)",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });

    private static bool TryPurgeStandby(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var command = MemoryPurgeStandbyList;
                var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
                if (status == 0) return true;
            }
            catch { return false; }
            try { Task.Delay(400, ct).GetAwaiter().GetResult(); } catch { return false; }
        }
        return false;
    }

    private static long AvailPhysBytes()
    {
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            return GlobalMemoryStatusEx(ref status) ? (long)status.AvailPhys : 0;
        }
        catch { return 0; }
    }
}
