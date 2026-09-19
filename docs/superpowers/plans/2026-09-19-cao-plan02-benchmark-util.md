# Plan 02 — Benchmark útil A+B + fluidez gaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convertir el benchmark de CA-O en una herramienta útil: cargas reales (CPU ops/s multihilo, MEM sin ruido GC, disco sistema seq+4K), veredicto por categoría, persistencia con TTL/invalidación, fluidez gaming (avgFPS/1% low/P99+DPC) y UI con tabla, sparkline, CSV, DNS y arranque.

**Architecture:** Solo se tocan `SystemBenchmarkRunner` (cargas+veredicto), `BenchmarkAnalyzer` (suelo 3.0), `BenchmarkModels` (campos nuevos con defaults para no romper compat), nuevo `BenchmarkStore` (persistencia TTL), nuevo `IFrameCapture`+`DxgiFrameCapture` (fluidez), `BenchmarkViewModel` (sesiones por optimización + timeout adaptativo) y `BenchmarkPage`/`OptimizePage`/`UiState` (UI). El pipe privilegiado, la transacción y el hook `BenchmarkAsync` NO se tocan.

**Tech Stack:** C# / .NET 10 / WinUI 3 (WindowsAppSDK 2.4), xUnit, BCL + P/Invoke kernel32 (existente) + DXGI OutputDuplication vía P/Invoke (nuevo, sin NuGet), WMI `Win32_OperatingSystem` (System.Management ya referenciado).

**Spec:** `docs/superpowers/specs/2026-09-17-cao-premium-cat-benchmark-cleanup-design.md` (sección 4, líneas 48-74; criterios §8 línea 112).

## Global Constraints

- .NET 10 + WindowsAppSDK 2.4; C# nullable; cero dependencias nuevas (sin `PackageReference` nuevos, sin NuGet).
- Commits en español (`feat/fix/test(bench): …`); trabajar sobre rama `feat/plan02-benchmark-util` desde `main`.
- Build `dotnet build CA-O.sln -c Release` + tests `dotnet test CA-O.sln -c Release` verdes antes de cada commit lógico.
- Textos visibles de UI en español y vía `Localizer` (no hardcodear literales nuevos en XAML/CS visible); no pisar claves existentes.
- El benchmark nunca muta estado transaccional; ante CV alta, cambio de power/build o error → `InsufficientData` / `InsuficienteData`, nunca un veredicto falso.
- Suelo de ruido único: `BenchmarkPolicy.MinimumEffectPercent` (3.0); nada de umbrales literales duplicados.
- Cero telemetría nueva; sin PowerShell en hot-path; try/catch en todo acceso UI.
- Compat: records existentes solo crecen con parámetros opcionales al final; strings de veredicto existentes (`Mejora medible`, `Regresión`, `Sin mejora medible`, `InsuficienteData`) no se renombran.

## File Structure

- Modify: `src/CA-O.Infrastructure/Benchmarking/SystemBenchmarkRunner.cs` — CPU ops/s multihilo, MEM con buffers reusados + `GC.TryStartNoGCRegion`, disco 256 MB WriteThrough seq+4K en volumen sistema, fases con tiempos, header completo, `CompareFull` con categoría, `RunTrialsAsync` respeta `warmup` + reporta CV.
- Modify: `src/CA-O.Shared/DTO/BenchmarkModels.cs` — `BenchmarkRunHeader` += `OsUbr, AppVersion, MachineHash, CpuName, BgCpuPercent, GpuName` (defaults); `SystemBenchmarkResult` (vive en Infra) += `CpuMs, CpuCvPercent, MemoryCvPercent, DiskReadIops, DiskWriteIops, PhaseSeconds` (defaults `0`); `SystemBenchmarkFullComparison` += `ReasonEs = ""`.
- Modify: `src/CA-O.Core/Benchmark/BenchmarkAnalyzer.cs` — default `significanceThresholdPercent` = `BenchmarkPolicy.MinimumEffectPercent`.
- Create: `src/CA-O.Infrastructure/Benchmarking/BenchmarkStore.cs` — `ComputeMachineHash`, `SaveSession/LoadBaseline/IsBaselineValid` (TTL 7 d + igualdad machineHash/osUBR/appVer/power), `BenchmarkSession` record `{ Before, After, DnsBefore, DnsAfter, Category, OptimizationId }`.
- Create: `src/CA-O.Infrastructure/Benchmarking/IFrameCapture.cs` — `IFrameCapture { Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct) }`, `FrameCaptureResult { IReadOnlyList<double> FrameTimesMs, double DpcPercent, double InterruptPercent }`.
- Create: `src/CA-O.Infrastructure/Benchmarking/DxgiFrameCapture.cs` — `IFrameCapture` vía IDXGIOutputDuplication (P/Invoke propio); `null` si no disponible.
- Create: `src/CA-O.Infrastructure/Benchmarking/BootInfoProvider.cs` — `LastBootUpTime` vía WMI + `Uptime`; `null` ante error.
- Modify: `src/CA-O.UI/ViewModels/BenchmarkViewModel.cs` — sesiones por optimización, timeout adaptativo (120 s / 300 s HDD vía `DiskMediaDetector` de Core si accesible, si no WMI `MSFT_PhysicalDisk`), validación deserialización, entrada `Operation="benchmark"` en historial vía `JsonHistoryLogger`.
- Modify: `src/CA-O.UI/ViewModels/UiState.cs` — `PendingBenchmarkOptimizationId` (string vacía = ninguno) + `PendingBenchmarkCategory`.
- Modify: `src/CA-O.UI/BenchmarkPage.xaml(.cs)` — tabla deltas, sparkline `Polyline`, export CSV, card DNS, card arranque, card fluidez, banner contexto optimización, auto-run al navegar con pendiente, cancelar al salir, moods gato Working/Celebrate/Warn.
- Modify: `src/CA-O.UI/OptimizePage.xaml(.cs)` — botón "Medir antes/después" por optimización: fija pendiente en `UiState` y navega a `benchmark`.
- Modify: `src/CA-O.UI/Resources/Localizer.cs` — claves `benchmark.*` nuevas (es+en).
- Test: `tests/CA-O.Benchmark.Tests/BenchmarkWorkloadTests.cs` (nuevo), `tests/CA-O.Benchmark.Tests/BenchmarkStoreTests.cs` (nuevo), `tests/CA-O.Benchmark.Tests/FrameCaptureTests.cs` (nuevo); Modify: `BenchmarkTrialsTests.cs`, `BenchmarkStatisticsTests.cs` (solo lo que rompa la compilación por nuevos campos; los veredictos existentes no cambian).

---

### Task 1: Cargas CPU+MEM reales y trials con CV

**Files:**
- Modify: `src/CA-O.Infrastructure/Benchmarking/SystemBenchmarkRunner.cs`
- Test: `tests/CA-O.Benchmark.Tests/BenchmarkWorkloadTests.cs`

**Interfaces:**
- Consumes: `Core.Benchmark.BenchmarkPolicy` (`MinimumEffectPercent`, `MeasuredRuns`, `WarmupRuns`) — ya usado.
- Produces: `SystemBenchmarkResult` con `CpuScore` = ops/s (>0, varía con carga), `CpuMs`, `CpuCvPercent`, `MemoryCvPercent`, `PhaseSeconds: Dictionary<string,double>` — Task 2 y 3 consumen estos campos.

- [ ] **Step 1: Escribir el test que falla (CPU ops/s + MEM estable)**

```csharp
using CAO.Infrastructure.Benchmarking;
using Xunit;

namespace CAO.Benchmark.Tests;

public sealed class BenchmarkWorkloadTests
{
    [Fact]
    public async Task RunAsync_ReportsPositiveCpuOpsPerSecond()
    {
        var runner = new SystemBenchmarkRunner();
        // Solo CPU+MEM rápidas aquí; el disco se cubre en Task 2 ( Medir disco I/O en CI es lento ).
        var result = await runner.RunAsync("cpu-only", CancellationToken.None);

        Assert.True(result.CpuScore > 0, "CpuScore debe ser ops/s > 0, no un conteo constante.");
        Assert.True(result.CpuMs > 0, "CpuMs debe medir tiempo real de la carga.");
        Assert.True(result.MemoryBandwidthGbs > 0, "El ancho de banda de memoria debe ser > 0.");
    }

    [Fact]
    public async Task RunTrialsAsync_RespectsWarmupParameter_AndReportsCv()
    {
        var runner = new SystemBenchmarkRunner();
        var result = await runner.RunTrialsAsync(trials: 2, warmup: false, workloadId: "no-warmup", ct: CancellationToken.None);

        Assert.Equal("no-warmup", result.WorkloadId);
        Assert.True(result.CpuCvPercent >= 0, "Debe reportar CV% aunque sea 0 con 2 trials.");
        Assert.True(result.MemoryCvPercent >= 0);
    }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release --filter "FullyQualifiedName~BenchmarkWorkloadTests"`
Expected: FAIL — `CpuCvPercent`, `CpuMs`, `PhaseSeconds` no existen; `RunTrialsAsync` no tiene parámetro que respete (hoy ignora `warmup`).

- [ ] **Step 3: Implementación mínima — CPU multihilo + MEM sin ruido GC**

Cambios exactos en `SystemBenchmarkRunner.cs`:

a) `SystemBenchmarkResult` += campos con default (no rompe `BenchmarkTrialsTests`, que usa args nombrados):

```csharp
public sealed record SystemBenchmarkResult(
    BenchmarkRunHeader Header,
    double CpuScore,
    double MemoryBandwidthGbs,
    double DiskReadMbs,
    double DiskWriteMbs,
    TimeSpan Elapsed,
    double CpuMs = 0,
    double CpuCvPercent = 0,
    double MemoryCvPercent = 0,
    double DiskReadIops = 0,
    double DiskWriteIops = 0,
    Dictionary<string, double>? PhaseSeconds = null);
```

b) `MeasureCpu` → ops/s multihilo:

```csharp
private static (double OpsPerSecond, double Milliseconds) MeasureCpu(CancellationToken ct)
{
    const int bound = 300_000;
    var sw = Stopwatch.StartNew();
    var total = 0L;
    Parallel.For(2, bound, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, () => 0L,
        (candidate, _, local) =>
        {
            ct.ThrowIfCancellationRequested();
            var isPrime = true;
            for (var divisor = 2; (long)divisor * divisor <= candidate; divisor++)
            {
                if (candidate % divisor == 0) { isPrime = false; break; }
            }
            return local + (isPrime ? 1 : 0);
        },
        local => Interlocked.Add(ref total, local));
    sw.Stop();
    return (total / sw.Elapsed.TotalSeconds, sw.Elapsed.TotalMilliseconds);
}
```

c) `MeasureMemoryBandwidth` con buffers estáticos reusados + warmup fuera de crono + `GC.TryStartNoGCRegion`:

```csharp
private static readonly byte[] SharedSource = new byte[32 * 1024 * 1024];
private static readonly byte[] SharedTarget = new byte[32 * 1024 * 1024];
private static bool _memoryWarmedUp;

private static double MeasureMemoryBandwidth(CancellationToken ct)
{
    const int size = 32 * 1024 * 1024;
    if (!_memoryWarmedUp)
    {
        Random.Shared.NextBytes(SharedSource);
        Array.Copy(SharedSource, SharedTarget, size); // warmup fuera de crono
        _memoryWarmedUp = true;
    }
    var noGc = false;
    try { noGc = GC.TryStartNoGCRegion(128L * 1024 * 1024, disallowFullBlockingGC: true); } catch { }
    var sw = Stopwatch.StartNew();
    try
    {
        for (var round = 0; round < 4; round++)
        {
            ct.ThrowIfCancellationRequested();
            Array.Copy(SharedSource, SharedTarget, size);
        }
    }
    finally
    {
        sw.Stop();
        if (noGc) { try { GC.EndNoGCRegion(); } catch { } }
    }
    var totalBytes = 4L * size * 2 / (1024d * 1024 * 1024);
    return totalBytes / sw.Elapsed.TotalSeconds;
}
```

NOTA para el implementador: los buffers estáticos de 64 MB viven todo el proceso; es el tradeoff documentado (evita alloc+GC por trial). `GC.KeepAlive` no hace falta al ser estáticos.

d) `RunAsync` mide fases y las guarda:

```csharp
public async Task<SystemBenchmarkResult> RunAsync(string workloadId = "system-baseline", CancellationToken ct = default)
{
    var phases = new Dictionary<string, double>(StringComparer.Ordinal);
    var swTotal = Stopwatch.StartNew();
    var (cpuOps, cpuMs) = await Task.Run(() => MeasureCpu(ct), ct);
    phases["cpu"] = cpuMs / 1000d;
    var memSw = Stopwatch.StartNew();
    var memory = await Task.Run(() => MeasureMemoryBandwidth(ct), ct);
    memSw.Stop();
    phases["memory"] = memSw.Elapsed.TotalSeconds;
    var diskSw = Stopwatch.StartNew();
    var (read, write) = await Task.Run(() => MeasureDisk(ct), ct);
    diskSw.Stop();
    phases["disk"] = diskSw.Elapsed.TotalSeconds;
    swTotal.Stop();
    // header igual que hoy (Task 2 lo completa); Elapsed total se conserva.
    ...
    return new SystemBenchmarkResult(header, cpuOps, memory, read, write, swTotal.Elapsed,
        CpuMs: cpuMs, PhaseSeconds: phases);
}
```

e) `RunTrialsAsync`: respeta el parámetro `warmup` (hoy usa siempre `WarmupRuns`) y calcula CV por métrica sobre los trials:

```csharp
public async Task<SystemBenchmarkResult> RunTrialsAsync(
    int trials = 3, bool warmup = true, string workloadId = "system-baseline", CancellationToken ct = default)
{
    var warmupRuns = warmup ? Core.Benchmark.BenchmarkPolicy.WarmupRuns : 0;
    for (var warm = 0; warm < warmupRuns; warm++)
        await RunAsync(workloadId + "-warmup" + (warm + 1), ct);
    var results = new List<SystemBenchmarkResult>(trials);
    for (var trial = 0; trial < trials; trial++)
    {
        ct.ThrowIfCancellationRequested();
        results.Add(await RunAsync($"{workloadId}-t{trial + 1}", ct));
    }
    var median = MedianOf(results);
    static double Cv(IEnumerable<double> values)
    {
        var arr = values.ToArray();
        if (arr.Length < 2) return 0;
        var avg = arr.Average();
        if (avg == 0) return 0;
        var sd = Math.Sqrt(arr.Average(v => (v - avg) * (v - avg)));
        return Math.Round(sd / avg * 100, 2);
    }
    return median with
    {
        CpuCvPercent = Cv(results.Select(r => r.CpuScore)),
        MemoryCvPercent = Cv(results.Select(r => r.MemoryBandwidthGbs)),
    };
}
```

f) `MedianOf`: propagar `CpuMs` (mediana) y `PhaseSeconds` (del trial mediano de CPU; simple: `head.PhaseSeconds`):

```csharp
var head = list[0];
return head with
{
    CpuScore = Median(result => result.CpuScore),
    MemoryBandwidthGbs = Median(result => result.MemoryBandwidthGbs),
    DiskReadMbs = Median(result => result.DiskReadMbs),
    DiskWriteMbs = Median(result => result.DiskWriteMbs),
    CpuMs = Median(result => result.CpuMs),
};
```

- [ ] **Step 4: Correr tests y verificar que pasan**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release`
Expected: PASS — nuevos 2 + 7 existentes (los existentes usan args nombrados y siguen compilando; `EffectBelowNoiseFloorIsNeverAnImprovement` y `RegressionBeyondNoiseFloorIsDetected` no cambian porque `Compare` no se toca en esta tarea).

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.Infrastructure/Benchmarking/SystemBenchmarkRunner.cs tests/CA-O.Benchmark.Tests/BenchmarkWorkloadTests.cs
git commit -m "feat(bench): CPU ops/s multihilo, MEM sin ruido GC y CV por métrica"
```

### Task 2: Disco real + header completo + veredicto por categoría + Analyzer 3.0

**Files:**
- Modify: `src/CA-O.Infrastructure/Benchmarking/SystemBenchmarkRunner.cs`
- Modify: `src/CA-O.Shared/DTO/BenchmarkModels.cs`
- Modify: `src/CA-O.Core/Benchmark/BenchmarkAnalyzer.cs`
- Test: `tests/CA-O.Benchmark.Tests/BenchmarkWorkloadTests.cs` (añadir), `tests/CA-O.Benchmark.Tests/BenchmarkStatisticsTests.cs` (añadir assert 3.0)

**Interfaces:**
- Consumes: Task 1 (`SystemBenchmarkResult` con nuevos campos); `OptimizationCategory` (`CAO.Shared.Enums` — valores `Performance, PrivacySecurity, Gaming, Storage, Network, Cleanup`).
- Produces: `CompareFull(baseline, after, category?)` con veredicto por categoría + `ReasonEs`; `BenchmarkRunHeader` completo; `BenchmarkAnalyzer.Compare` con suelo 3.0 — Task 3 consume header+veredicto.

- [ ] **Step 1: Escribir los tests que fallan**

Añadir a `BenchmarkWorkloadTests.cs`:

```csharp
[Fact]
public void StorageCategory_WinsOnDisk_EvenWhenCpuFlat()
{
    var header = new BenchmarkRunHeader("b", DateTime.UtcNow, 26200, "", "", 0, "ac");
    var baseline = new SystemBenchmarkResult(header, CpuScore: 50_000, MemoryBandwidthGbs: 20, DiskReadMbs: 400, DiskWriteMbs: 350, Elapsed: TimeSpan.Zero);
    var after = baseline with { DiskReadMbs = 460, DiskWriteMbs = 350 }; // +15 % lectura

    var comparison = SystemBenchmarkRunner.CompareFull(baseline, after, OptimizationCategory.Storage);

    Assert.Equal("Mejora medible", comparison.VerdictEs);
    Assert.Contains("disco", comparison.ReasonEs, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void NullCategory_KeepsLegacyCpuMemoryVerdict()
{
    var header = new BenchmarkRunHeader("b", DateTime.UtcNow, 26200, "", "", 0, "ac");
    var baseline = new SystemBenchmarkResult(header, CpuScore: 50_000, MemoryBandwidthGbs: 20, DiskReadMbs: 400, DiskWriteMbs: 350, Elapsed: TimeSpan.Zero);
    var after = baseline with { DiskReadMbs = 800 }; // +100 % disco, CPU/MEM planas

    var comparison = SystemBenchmarkRunner.CompareFull(baseline, after);

    Assert.Equal("Sin mejora medible", comparison.VerdictEs); // compat: sin categoría, CPU/MEM mandan
}
```

Añadir a `BenchmarkStatisticsTests.cs`:

```csharp
[Fact]
public void ComparisonDefaultThreshold_IsPolicyThreePercent()
{
    var baseline = new FrameTimeStatistics(600, 120.0, 90.0, 70.0, 8.3, 11.0, 12.0, 2.5);
    var after = baseline with { AverageFps = 122.0, OnePercentLowFps = 92.0, P99FrameTimeMs = 11.6 }; // ~+1.7 % / -3.3 %

    var comparison = BenchmarkAnalyzer.Compare(baseline, after);

    // Con el viejo suelo 1.0 esto sería Improvement; con 3.0 no hay mejora consistente.
    Assert.Equal(BenchmarkVerdict.NoMeasurableImprovement, comparison.Verdict);
}
```

NOTA: verificar los números al implementar — `P99FrameTimeChange = (11.6-12)/12 = -3.33 %` supera 3.0 pero avgFPS +1.67 % y 1%low +2.2 % no llegan: Improvement exige las 3 condiciones, Regression exige alguna peor que -3.0 (avg no, low no, p99 mejora) → `NoMeasurableImprovement`. Correcto.

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release --filter "FullyQualifiedName~BenchmarkWorkloadTests|FullyQualifiedName~BenchmarkStatisticsTests.ComparisonDefaultThreshold_IsPolicyThreePercent"`
Expected: FAIL — `CompareFull` no acepta categoría, `ReasonEs` no existe, `Analyzer.Compare` usa 1.0.

- [ ] **Step 3: Implementación — disco, header, veredicto, suelo**

a) `BenchmarkModels.cs` — `BenchmarkRunHeader` crece al final con defaults (los tests existentes usan `GpuDriverVersion:`/`Resolution:`/`RefreshHz:`/`PowerState:` nombrados + 3 posicionales; añadir al final no rompe):

```csharp
public sealed record BenchmarkRunHeader(
    string WorkloadId,
    DateTime TimestampUtc,
    int WindowsBuild,
    string GpuDriverVersion,
    string Resolution,
    int RefreshHz,
    string PowerState,
    string OsUbr = "",
    string AppVersion = "",
    string MachineHash = "",
    string CpuName = "",
    double BgCpuPercent = 0,
    string GpuName = "");
```

b) `SystemBenchmarkFullComparison` += `ReasonEs = ""`:

```csharp
public sealed record SystemBenchmarkFullComparison(
    double CpuDeltaPercent,
    double MemoryDeltaPercent,
    double DiskReadDeltaPercent,
    double DiskWriteDeltaPercent,
    string VerdictEs,
    string ReasonEs = "");
```

c) `MeasureDisk` real — 256 MB prealocado en volumen sistema, `WriteThrough`, seq + 4K random con IOPS:

```csharp
private static (double ReadMbs, double WriteMbs, double ReadIops, double WriteIops) MeasureDisk(CancellationToken ct)
{
    // Volumen del sistema (donde viven Windows y la mayoría de juegos/apps), no solo %TEMP% cacheado.
    var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? Path.GetTempPath();
    var path = Path.Combine(systemRoot, $"cao-bench-{Guid.NewGuid():N}.tmp");
    try
    {
        const int seqBlocks = 256; // 256 MB secuencial
        const int payload4k = 4 * 1024;
        const int ops4k = 2000;
        var seqBuffer = new byte[1024 * 1024];
        Random.Shared.NextBytes(seqBuffer);

        double writeMbs, readMbs;
        var sw = Stopwatch.StartNew();
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   1024 * 1024, FileOptions.WriteThrough))
        {
            stream.SetLength((long)seqBlocks * seqBuffer.Length); // prealocado: mide escritura, no metadata
            for (var block = 0; block < seqBlocks; block++)
            {
                ct.ThrowIfCancellationRequested();
                stream.Write(seqBuffer, 0, seqBuffer.Length);
            }
            stream.Flush(true);
        }
        sw.Stop();
        writeMbs = seqBlocks / sw.Elapsed.TotalSeconds;

        using var verifyStream = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileOptions.SequentialScan);
        var readBuffer = new byte[seqBuffer.Length];
        sw.Restart();
        long totalRead = 0;
        while (totalRead < (long)seqBlocks * seqBuffer.Length)
        {
            ct.ThrowIfCancellationRequested();
            totalRead += RandomAccess.Read(verifyStream, readBuffer, totalRead);
        }
        sw.Stop();
        readMbs = (totalRead / (1024d * 1024)) / sw.Elapsed.TotalSeconds;

        // 4K aleatorio sobre el mismo fichero (IOPS reales).
        using var randStream = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None,
            FileOptions.WriteThrough);
        var tiny = new byte[payload4k];
        Random.Shared.NextBytes(tiny);
        var maxOffset = (long)seqBlocks * seqBuffer.Length - payload4k;
        sw.Restart();
        for (var op = 0; op < ops4k; op++)
        {
            ct.ThrowIfCancellationRequested();
            var offset = Random.Shared.NextInt64(0, maxOffset + 1);
            RandomAccess.Write(randStream, tiny, offset);
        }
        sw.Stop();
        var writeIops = ops4k / sw.Elapsed.TotalSeconds;
        sw.Restart();
        for (var op = 0; op < ops4k; op++)
        {
            ct.ThrowIfCancellationRequested();
            var offset = Random.Shared.NextInt64(0, maxOffset + 1);
            RandomAccess.Read(randStream, tiny, offset);
        }
        sw.Stop();
        var readIops = ops4k / sw.Elapsed.TotalSeconds;

        GC.KeepAlive(seqBuffer);
        GC.KeepAlive(readBuffer);
        return (readMbs, writeMbs, readIops, writeIops);
    }
    finally
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
```

REGLAS para el implementador: si la escritura en la raíz del volumen falla por permisos (FileMode.CreateNew en `C:\`), fallback a `Path.GetTempPath()` reintentando una vez; el fichero se borra siempre en `finally`. Añadir al summary del método el disclaimer: la lectura secuencial inmediata puede beneficiarse de caché — el test 4K + WriteThrough es la medida dura.

d) `RunAsync` usa la nueva tupla y completa el header (cpuName vía `System.Management` está en Infra? NO — `System.Management` lo usa Infra `SystemInterop/*Provider.cs`; verificar referencia del csproj `CA-O.Infrastructure` antes de usar WMI; si no está, obtener cpuName por variable de entorno `PROCESSOR_IDENTIFIER` — simple y sin deps):

```csharp
var header = new BenchmarkRunHeader(
    workloadId,
    DateTime.UtcNow,
    Environment.OSVersion.Version.Build,
    GpuDriverPlaceholder(),
    string.Empty, // Resolution: ya no se abusa para ProcessorCount
    0,
    PowerState(),
    OsUbr: Environment.OSVersion.Version.ToString(),
    AppVersion: AppVersion.Semantic, // CAO.Shared; si no accesible desde Infra, usar Assembly informacional con fallback ""
    MachineHash: BenchmarkStore.ComputeMachineHash(MachineId.Current(), CpuName()),
    CpuName: CpuName(),
    BgCpuPercent: 0, // Task 4 la rellena durante captura; aquí 0 = no medida
    GpuName: string.Empty);
```

`CpuName()` = `Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? string.Empty`. `MachineId.Current()` — Task 3 lo crea en `BenchmarkStore` (lee `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` con try/catch → `""`); Task 2 lo consume. Si Task 2 se implementa antes, declarar el `MachineId` mínimo en esta tarea dentro de `BenchmarkStore.cs` (el fichero se crea en Task 2 con `ComputeMachineHash` + `MachineId`, Task 3 añade persistencia).

e) `CompareFull` con categoría:

```csharp
public static SystemBenchmarkFullComparison CompareFull(
    SystemBenchmarkResult baseline, SystemBenchmarkResult after, OptimizationCategory? category = null)
{
    var baseCmp = Compare(baseline, after);
    var diskReadDelta = PercentChange(baseline.DiskReadMbs, after.DiskReadMbs);
    var diskWriteDelta = PercentChange(baseline.DiskWriteMbs, after.DiskWriteMbs);

    // Sin categoría: compat total (CPU/MEM deciden, disco informativo).
    if (category is null)
        return new(baseCmp.CpuDeltaPercent, baseCmp.MemoryDeltaPercent, diskReadDelta, diskWriteDelta, baseCmp.VerdictEs, "Veredicto clásico CPU/memoria; disco informativo.");

    // Storage/Cleanup: el disco (lectura+4K) decide.
    if (category is OptimizationCategory.Storage or OptimizationCategory.Cleanup)
    {
        var diskWins = diskReadDelta > NoiseFloorPercent || diskWriteDelta > NoiseFloorPercent;
        var diskLoses = diskReadDelta < -NoiseFloorPercent || diskWriteDelta < -NoiseFloorPercent;
        var verdict = diskWins ? "Mejora medible" : diskLoses ? "Regresión" : "Sin mejora medible";
        return new(baseCmp.CpuDeltaPercent, baseCmp.MemoryDeltaPercent, diskReadDelta, diskWriteDelta, verdict,
            $"Veredicto por disco para {category}: R {diskReadDelta:+0.0;-0.0}% W {diskWriteDelta:+0.0;-0.0}% (suelo ±{NoiseFloorPercent:0}%).");
    }

    // Network/Gaming/resto: CPU/MEM deciden (Network añade DNS en Task 5; Gaming añade fluidez en Task 4).
    return new(baseCmp.CpuDeltaPercent, baseCmp.MemoryDeltaPercent, diskReadDelta, diskWriteDelta, baseCmp.VerdictEs,
        $"Veredicto CPU/memoria para {category}: CPU {baseCmp.CpuDeltaPercent:+0.0;-0.0}% MEM {baseCmp.MemoryDeltaPercent:+0.0;-0.0}%.");
}
```

f) `BenchmarkAnalyzer.Compare`: `double significanceThresholdPercent = 1.0` → `double significanceThresholdPercent = Benchmark.BenchmarkPolicy.MinimumEffectPercent`. Añadir `using CAO.Core.Benchmark;` (nota: la clase vive en namespace `CAO.Core.Engine`; el using nuevo no colisiona).

- [ ] **Step 4: Correr tests**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release`
Expected: PASS — 10 tests (7 viejos + 3 nuevos). `RunAsync_ReportsPositiveCpuOpsPerSecond` ahora tarda más (disco 256 MB): aceptable en Release.

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.Infrastructure/Benchmarking/SystemBenchmarkRunner.cs src/CA-O.Shared/DTO/BenchmarkModels.cs src/CA-O.Core/Benchmark/BenchmarkAnalyzer.cs src/CA-O.Infrastructure/Benchmarking/BenchmarkStore.cs tests/CA-O.Benchmark.Tests/
git commit -m "feat(bench): disco real seq+4K, header completo y veredicto por categoría"
```

### Task 3: Persistencia con TTL/invalidación + timeout adaptativo en el ViewModel

**Files:**
- Modify (crear si Task 2 no lo hizo): `src/CA-O.Infrastructure/Benchmarking/BenchmarkStore.cs`
- Modify: `src/CA-O.UI/ViewModels/BenchmarkViewModel.cs`
- Test: `tests/CA-O.Benchmark.Tests/BenchmarkStoreTests.cs`

**Interfaces:**
- Consumes: Task 1-2 (resultados + header + `CompareFull` con categoría).
- Produces: `BenchmarkStore` (`ComputeMachineHash`, `BaselinePathFor`, `SaveBaseline/LoadBaseline/IsBaselineValid`, `SaveSession`, `Ttl = 7 días`, `TimeoutFor(drivePath)`); VM con `RunForOptimizationAsync(optId, category, isBaseline, ct)` — Task 5 consume la VM.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

public sealed class BenchmarkStoreTests
{
    private static SystemBenchmarkResult Result(string workload, string power = "ac", string hash = "abc123") =>
        new(new BenchmarkRunHeader(workload, DateTime.UtcNow, 26200, "", "", 0, power, MachineHash: hash),
            CpuScore: 50_000, MemoryBandwidthGbs: 20, DiskReadMbs: 500, DiskWriteMbs: 450, Elapsed: TimeSpan.FromSeconds(9));

    [Fact]
    public void MachineHash_IsStable_AndShort_AndHidesInput()
    {
        var a = BenchmarkStore.ComputeMachineHash("guid-1", "cpu-x");
        var b = BenchmarkStore.ComputeMachineHash("guid-1", "cpu-x");
        var c = BenchmarkStore.ComputeMachineHash("guid-2", "cpu-x");

        Assert.Equal(a, b);
        Assert.Equal(16, a.Length);
        Assert.NotEqual(a, c);
        Assert.DoesNotContain("guid-1", a);
    }

    [Fact]
    public void Baseline_ExpiresAfterTtl_OrOnPowerOrHashChange()
    {
        var baseline = Result("baseline");
        Assert.True(BenchmarkStore.IsBaselineValid(baseline, Result("after"), DateTime.UtcNow));
        Assert.False(BenchmarkStore.IsBaselineValid(baseline, Result("after", power: "battery"), DateTime.UtcNow));
        Assert.False(BenchmarkStore.IsBaselineValid(baseline, Result("after", hash: "otro"), DateTime.UtcNow));
        Assert.False(BenchmarkStore.IsBaselineValid(baseline, Result("after"), DateTime.UtcNow.AddDays(8)));
    }

    [Fact]
    public void Session_RoundTrips_WithCategory()
    {
        var session = new BenchmarkSession(Result("b"), null, null, null, "Storage", "cleanup-windows-temp");
        var json = System.Text.Json.JsonSerializer.Serialize(session);
        var back = System.Text.Json.JsonSerializer.Deserialize<BenchmarkSession>(json);

        Assert.NotNull(back);
        Assert.Equal("Storage", back!.Category);
        Assert.Equal("cleanup-windows-temp", back.OptimizationId);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release --filter "FullyQualifiedName~BenchmarkStoreTests"`
Expected: FAIL — `BenchmarkStore`/`BenchmarkSession` no existen.

- [ ] **Step 3: Implementación — `BenchmarkStore.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CAO.Shared;

namespace CAO.Infrastructure.Benchmarking;

/// <summary>Sesión A/B ligada a una optimización (o "manual").</summary>
public sealed record BenchmarkSession(
    SystemBenchmarkResult Before,
    SystemBenchmarkResult? After,
    IReadOnlyList<double>? DnsBeforeMs,
    IReadOnlyList<double>? DnsAfterMs,
    string Category,
    string OptimizationId);

/// <summary>Identidad máquina no reversible (solo comparación).</summary>
public static class MachineId
{
    public static string Current()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? string.Empty;
        }
        catch { return string.Empty; }
    }
}

public static class BenchmarkStore
{
    public static readonly TimeSpan BaselineTtl = TimeSpan.FromDays(7);

    /// <summary>SHA256 truncado a 16 hex de MachineGuid+CPU: estable, no reversible.</summary>
    public static string ComputeMachineHash(string machineGuid, string cpuName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"CA-O|{machineGuid}|{cpuName}"));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    public static string BaselinePath => Path.Combine(CaOPaths.BenchmarksDirectory, "baseline.json");

    public static string SessionPathFor(string optimizationId, DateTime utc)
    {
        var safe = string.Concat(optimizationId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        return Path.Combine(CaOPaths.BenchmarksDirectory, $"{safe}-{utc:yyyyMMddHHmmss}.json");
    }

    public static bool IsBaselineValid(SystemBenchmarkResult baseline, SystemBenchmarkResult current, DateTime utcNow)
    {
        if ((utcNow - baseline.Header.TimestampUtc) > BaselineTtl) return false;
        return string.Equals(baseline.Header.MachineHash, current.Header.MachineHash, StringComparison.Ordinal)
            && baseline.Header.WindowsBuild == current.Header.WindowsBuild
            && string.Equals(baseline.Header.OsUbr, current.Header.OsUbr, StringComparison.Ordinal)
            && string.Equals(baseline.Header.AppVersion, current.Header.AppVersion, StringComparison.Ordinal)
            && string.Equals(baseline.Header.PowerState, current.Header.PowerState, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task SaveJsonAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(CaOPaths.BenchmarksDirectory);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value), ct);
    }

    public static async Task<T?> LoadJsonAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch { return default; } // fichero ausente o corrupto: sin datos, sin crash
    }

    /// <summary>Timeout adaptativo: 120 s base SSD, 300 s si el volumen sistema es HDD.</summary>
    public static TimeSpan TimeoutForSystemDrive()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (string.Equals(drive.Name, root, StringComparison.OrdinalIgnoreCase))
                    return drive.DriveType == DriveType.Fixed && IsHdd(root) ? TimeSpan.FromSeconds(300) : TimeSpan.FromSeconds(120);
            }
        }
        catch { }
        return TimeSpan.FromSeconds(120);
    }

    private static bool IsHdd(string root)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT MediaType FROM Win32_DiskDrive");
            foreach (var disk in searcher.Get())
            {
                var media = disk["MediaType"]?.ToString() ?? string.Empty;
                if (media.Contains("Fixed", StringComparison.OrdinalIgnoreCase)) return true; // HDD clásico informa Fixed hard disk media
            }
        }
        catch { }
        return false;
    }
}
```

REGLA para el implementador: verificar que `CA-O.Infrastructure.csproj` referencia `System.Management`; si no, `IsHdd` usa fallback `return false` (120 s) SIN añadir NuGet — documentar la decisión en el report. `CaOPaths` ya es visible desde Infra (el runner usa `CAO.Shared`).

`BenchmarkSession` con `IReadOnlyList<double>?`: `System.Text.Json` lo deserializa como `List<double>` — el test round-trip lo cubre.

- [ ] **Step 4: ViewModel — sesiones por optimización (sin UI nueva aún)**

Refactor mínimo en `BenchmarkViewModel.cs` (la UI nueva llega en Task 5; aquí solo lógica + persistencia):

```csharp
public async Task RunForOptimizationAsync(string optimizationId, string category, bool isBaseline, CancellationToken ct)
{
    IsRunning = true;
    Status = "Midiendo…";
    try
    {
        var runner = new SystemBenchmarkRunner();
        var result = await runner.RunTrialsAsync(trials: 3, warmup: true,
            workloadId: $"{optimizationId}-{(isBaseline ? "baseline" : "after")}", ct);
        BaselineSummary = Describe(result);
        ContextNote = DescribeContext(result);
        if (isBaseline)
        {
            await BenchmarkStore.SaveJsonAsync(BenchmarkStore.BaselinePath, result, ct);
            await BenchmarkStore.SaveJsonAsync(BenchmarkStore.SessionPathFor(optimizationId, DateTime.UtcNow),
                new BenchmarkSession(result, null, null, null, category, optimizationId), ct);
            ComparisonSummary = "✓ Línea base guardada (mediana de 3 trials).";
            Verdict = "Línea base lista";
        }
        else
        {
            var baseline = await BenchmarkStore.LoadJsonAsync<SystemBenchmarkResult>(BenchmarkStore.BaselinePath, ct);
            if (baseline is null) { ComparisonSummary = "No hay línea base legible; mida primero la línea base."; Verdict = "Sin datos"; return; }
            if (!BenchmarkStore.IsBaselineValid(baseline, result, DateTime.UtcNow))
            {
                ComparisonSummary = "La línea base caducó o cambiaron las condiciones (TTL 7 días, misma máquina/SO/versión/alimentación). Mida de nuevo la línea base.";
                Verdict = "InsuficienteData";
                return;
            }
            if (result.CpuCvPercent > 5 || result.MemoryCvPercent > 5)
            {
                ComparisonSummary = $"Varianza alta entre trials (CV CPU {result.CpuCvPercent:0.0}% MEM {result.MemoryCvPercent:0.0}% > 5%): repita con el equipo en reposo.";
                Verdict = "InsuficienteData";
                return;
            }
            var parsed = Enum.TryParse<OptimizationCategory>(category, out var cat) ? cat : (OptimizationCategory?)null;
            var comparison = SystemBenchmarkRunner.CompareFull(baseline, result, parsed);
            ComparisonSummary = $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% | Disco R: {comparison.DiskReadDeltaPercent:+0.0;-0.0}% W: {comparison.DiskWriteDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} (suelo ±3%, mediana 3 trials).\n{comparison.ReasonEs}";
            Verdict = comparison.VerdictEs;
            await BenchmarkStore.SaveJsonAsync(BenchmarkStore.SessionPathFor(optimizationId, DateTime.UtcNow),
                new BenchmarkSession(baseline, result, null, null, category, optimizationId), ct);
        }
        Status = $"Benchmark completado — {Verdict}";
    }
    catch (OperationCanceledException) { Status = "Benchmark cancelado."; Verdict = "Cancelado"; }
    catch (Exception ex)
    {
        ComparisonSummary = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark no pudo completarse. [Técnico: {ex.GetType().Name}]";
        Status = $"{ErrorCodes.UiBenchmarkFailed}: benchmark fallido";
        Verdict = "Error";
        App.WriteCrashLog(ex);
    }
    finally { IsRunning = false; if (Status == "Midiendo…") Status = string.Empty; }
}
```

Mantener `RunAsync(bool, ct)` existente delegando a `RunForOptimizationAsync("manual", "", isBaseline, ct)` para no romper `BenchmarkPage` actual (Task 5 la reescribe). Mantener `Describe`/`DescribeContext` intactos.

- [ ] **Step 5: Correr tests**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release`
Expected: PASS — 13 tests.

- [ ] **Step 6: Commit**

```bash
git add src/CA-O.Infrastructure/Benchmarking/BenchmarkStore.cs src/CA-O.UI/ViewModels/BenchmarkViewModel.cs tests/CA-O.Benchmark.Tests/BenchmarkStoreTests.cs
git commit -m "feat(bench): sesiones por Tx con TTL 7d, invalidación y timeout adaptativo"
```

### Task 4: Fluidez gaming — IFrameCapture DXGI + DPC + cableado a Analyzer

**Files:**
- Create: `src/CA-O.Infrastructure/Benchmarking/IFrameCapture.cs`
- Create: `src/CA-O.Infrastructure/Benchmarking/DxgiFrameCapture.cs`
- Create: `src/CA-O.Infrastructure/Benchmarking/BootInfoProvider.cs`
- Test: `tests/CA-O.Benchmark.Tests/FrameCaptureTests.cs`

**Interfaces:**
- Consumes: `BenchmarkAnalyzer.AnalyzeFrameTimes` (existe), `DpcLatencySampler` (`src/CA-O.Infrastructure/SystemInterop/DpcLatencySampler.cs` — leer su API pública en el primer paso).
- Produces: `IFrameCapture.CaptureAsync` → `FrameCaptureResult?` (null = no disponible, la UI lo muestra honestamente); `BootInfoProvider.GetBootInfo()` — Task 5 consume ambos.

- [ ] **Step 0 (obligatorio AGENTS.md): research API antes de codificar**

Usar Context7 (`DXGI OutputDuplication`, `IDXGIOutput1.DuplicateOutput`, `AcquireNextFrame`) para confirmar firmas P/Invoke; nada de memoria del modelo para interop 2025-26. Si Context7 no cubre DXGI, usar websearch con cita. Registrar fuente en el report. Si la investigación concluye que el P/Invoke manual es demasiado frágil sin CsWin32: ruling documentado — implementar `DxgiFrameCapture` con el subconjunto mínimo (`D3D11CreateDevice` + `IDXGIOutput1.DuplicateOutput` + `AcquireNextFrame` con timeout + `ReleaseFrame`) y degradar a `null` ante CUALQUIER fallo (HRESULT != S_OK, timeout, excepción).

- [ ] **Step 1: Escribir los tests que fallan (sintéticos, sin GPU)**

```csharp
using CAO.Core.Engine;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

public sealed class FrameCaptureTests
{
    private sealed class FakeCapture : IFrameCapture
    {
        public Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct)
        {
            var frames = Enumerable.Repeat(16.7, 595).Concat(Enumerable.Repeat(50.0, 5)).ToList();
            return Task.FromResult<FrameCaptureResult?>(new(frames, DpcPercent: 0.4, InterruptPercent: 0.2));
        }
    }

    [Fact]
    public async Task CaptureResult_FeedsAnalyzer_AvgAndLows()
    {
        IFrameCapture capture = new FakeCapture();
        var result = await capture.CaptureAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.NotNull(result);
        var stats = BenchmarkAnalyzer.AnalyzeFrameTimes(result!.FrameTimesMs);

        Assert.Equal(600, stats.SampleCount);
        Assert.InRange(stats.AverageFps, 54, 60);
        Assert.True(stats.OnePercentLowFps < stats.AverageFps);
        Assert.True(stats.P99FrameTimeMs > stats.P95FrameTimeMs);
    }

    [Fact]
    public void BootInfo_ReturnsSaneUptime_OrNull()
    {
        var boot = BootInfoProvider.GetBootInfo();
        // En CI sin WMI puede ser null: el test solo exige no-crash y cordura si hay datos.
        if (boot is not null)
            Assert.True(boot.BootTimeUtc <= DateTime.UtcNow);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release --filter "FullyQualifiedName~FrameCaptureTests"`
Expected: FAIL — no existen `IFrameCapture`, `FrameCaptureResult`, `BootInfoProvider`.

- [ ] **Step 3: Implementación**

`IFrameCapture.cs`:

```csharp
namespace CAO.Infrastructure.Benchmarking;

/// <summary>Ventana de captura de fluidez (B ligero v1): frame-times + DPC%. Null = no disponible en este equipo.</summary>
public sealed record FrameCaptureResult(IReadOnlyList<double> FrameTimesMs, double DpcPercent, double InterruptPercent);

public interface IFrameCapture
{
    Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct);
}
```

`BootInfoProvider.cs` (WMI `Win32_OperatingSystem.LastBootUpTime`, formato `yyyyMMddHHmmss.ffffff+UUU`):

```csharp
using System.Management;

namespace CAO.Infrastructure.Benchmarking;

public sealed record BootInfo(DateTime BootTimeUtc, TimeSpan Uptime);

public static class BootInfoProvider
{
    public static BootInfo? GetBootInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (var os in searcher.Get())
            {
                var raw = os["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(raw))
                {
                    var boot = ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime();
                    if (boot <= DateTime.UtcNow)
                        return new(boot, DateTime.UtcNow - boot);
                }
            }
        }
        catch { }
        return null;
    }
}
```

REGLA: si `System.Management` no está referenciado en `CA-O.Infrastructure.csproj`, NO añadir NuGet: implementar el fallback por `Environment.TickCount64` (`boot ≈ UtcNow - uptime`; BootTimeUtc aproximado, documentado en el summary) — decisión en el report.

`DxgiFrameCapture.cs`: esqueleto honesto con degradación total (el detalle P/Invoke exacto lo fija el Step 0):

```csharp
namespace CAO.Infrastructure.Benchmarking;

/// <summary>Captura frame-times del escritorio vía DXGI OutputDuplication (10 s por defecto).
/// Sin dependencias externas: P/Invoke propio mínimo. Cualquier fallo → null (UI: "no disponible").</summary>
public sealed class DxgiFrameCapture : IFrameCapture
{
    public async Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct)
    {
        try
        {
            return await Task.Run(() => CaptureCore(duration, ct), ct);
        }
        catch { return null; }
    }

    private static FrameCaptureResult? CaptureCore(TimeSpan duration, CancellationToken ct)
    {
        // (Step 0) D3D11CreateDevice → IDXGIOutput1.DuplicateOutput → bucle AcquireNextFrame(timeout 500 ms)
        // registrando (now - lastPresent) en ms + ReleaseFrame; DPC% vía DpcLatencySampler si su API lo permite.
        // Cualquier HRESULT != S_OK o excepción → return null.
        throw new NotSupportedException("Rellenar tras Step 0; si DXGI no viable, devolver null.");
    }
}
```

RULING permitido: si el Step 0 demuestra que el P/Invoke mínimo no es fiable en el tiempo de la tarea, `CaptureCore` devuelve `null` siempre con comentario que cita la fuente, el test sintético sigue verde (usa `FakeCapture`), la UI muestra "fluidez no disponible en este equipo" y el plan sigue MERGEABLE — la puerta a PresentMon futuro queda en la interfaz. Documentarlo en el report, no en silencio.

DPC%: leer PRIMERO `DpcLatencySampler.cs` (líneas 1-80) y usar su API pública tal cual; si es interna obatible, medir `% DPC Time` con `PerformanceCounter("Processor", "% DPC Time", "_Total")` con 2 lecturas separadas 500 ms dentro de try/catch (0 si falla).

- [ ] **Step 4: Correr tests**

Run: `dotnet test tests/CA-O.Benchmark.Tests -c Release`
Expected: PASS — 15 tests.

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.Infrastructure/Benchmarking/IFrameCapture.cs src/CA-O.Infrastructure/Benchmarking/DxgiFrameCapture.cs src/CA-O.Infrastructure/Benchmarking/BootInfoProvider.cs tests/CA-O.Benchmark.Tests/FrameCaptureTests.cs
git commit -m "feat(bench): captura de fluidez DXGI + DPC y arranque vía WMI"
```

### Task 5: UI Benchmark — tabla, sparkline, CSV, DNS, arranque, fluidez + botón en Optimize

**Files:**
- Modify: `src/CA-O.UI/ViewModels/UiState.cs` (pendiente benchmark)
- Modify: `src/CA-O.UI/BenchmarkPage.xaml`, `src/CA-O.UI/BenchmarkPage.xaml.cs`
- Modify: `src/CA-O.UI/OptimizePage.xaml`, `src/CA-O.UI/OptimizePage.xaml.cs` (botón por optimización)
- Modify: `src/CA-O.UI/Resources/Localizer.cs` (claves es+en)
- Modify: `src/CA-O.UI/ViewModels/BenchmarkViewModel.cs` (DNS + fluidez + CSV + historial — ver Task 6 para historial)

**Interfaces:**
- Consumes: Task 3 (VM `RunForOptimizationAsync`), Task 4 (`IFrameCapture`, `BootInfoProvider`), `DnsBenchmarkProvider` (existe en Infra; `AnalyzePage.xaml.cs:532-575` muestra el uso), `CatMoodCatalog`/`CaoCat` (Plan 01).
- Produces: página Benchmark completa — Task 6 (historial) consume el `ComparisonSummary`/veredicto ya pintado.

Pasos previos obligatorios del implementador (leer antes de tocar): `AnalyzePage.xaml.cs:532-575` (uso DNS), `DpcLatencySampler.cs` API real, `OptimizePage.xaml` (dónde va el botón — junto a Aplicar/Revertir por fila), `UiState.cs` patrón `SetProperty`.

- [ ] **Step 1: UiState pendiente + Localizer (TDD donde aplique; Localizer es diccionario — directo)**

`UiState.cs` (seguir el patrón `MascotMood` de Task Plan01-1):

```csharp
[ObservableProperty] private string _pendingBenchmarkOptimizationId = string.Empty;
[ObservableProperty] private string _pendingBenchmarkCategory = string.Empty;
```

`Localizer.cs` es+en (insertar junto a `benchmark.compare`):

```
["benchmark.measureOpt"] / "Medir antes/después"
["benchmark.measuring"] / "Midiendo…"
["benchmark.dnsTitle"] / "DNS real"
["benchmark.bootTitle"] / "Arranque"
["benchmark.fluencyTitle"] / "Fluidez gaming (10 s)"
["benchmark.exportCsv"] / "Exportar CSV"
["benchmark.unavailable"] / "No disponible en este equipo"
["benchmark.ctxOpt"] / "Midiendo el impacto de"
(en: "Measure before/after", "Measuring…", "Real DNS", "Boot", "Gaming smoothness (10s)", "Export CSV", "Not available on this machine", "Measuring the impact of")
```

- [ ] **Step 2: BenchmarkPage XAML — 4 bloques nuevos (conservar stepper, cards pedagógicas, nav)**

a) Banner contexto (debajo del InfoBar, `Collapsed` por defecto, `x:Name="OptContextCard"`): texto `CtxOptText` + botón "Limpiar contexto".
b) Tabla deltas (`x:Name="DeltaTable"` Grid 3 cols Métrica/Antes/Después/Δ% — 4 filas CPU/MEM/DiscoR/DiscoW) dentro de la card Comparación, debajo de `ComparisonText`; `Visibility Collapsed` hasta haber comparación. Relleno desde code-behind con `TextBlock`s creados en XAML (no code-gen de controles salvo los 16 `TextBlock` de celdas con `x:Name`).
c) Sparkline: `Polyline x:Name="CpuSpark"` (Stroke accent, 120×36) + `Polyline x:Name="DiskSpark"` en la card Comparación; puntos = deltas por trial si hay sesión, si no `Visibility Collapsed`. Sin librerías.
d) Card DNS (`DnsCard`): botón `DnsButton` "Medir DNS" + `DnsResultText` + mini-barras con `Rectangle`s (seguir `RenderDnsBars` de Analyze como referencia visual, simplificado a 4 filas).
e) Card Arranque (`BootCard`): `BootText` (último arranque + uptime) — se rellena al navegar, informativo.
f) Card Fluidez (`FluencyCard`): botón `FluencyButton` "Medir fluidez 10 s" + `FluencyText` (avgFPS, 1% low, P99, DPC%) o `benchmark.unavailable`.
g) Botón `ExportCsvButton` en la card Comparación (`Collapsed` hasta haber sesión).
h) Gato: `controls:CaoCat x:Name="BenchCat"` en la card de medición (`CaoActionCardStyle` grid col 0 junto al icono, Width 64 Height 76) — Working al medir, Celebrate si mejora, Warn si regresión, Idle en reposo. Todo en try/catch (constraint Plan 01).

Estilos: solo `Cao*` existentes; `CardHover` en cards nuevas; `AutomationProperties.Name` en botones; textos vía `Localizer` en `ApplyTexts`.

- [ ] **Step 3: BenchmarkPage code-behind + VM (DNS, fluidez, CSV, auto-run, cancelar al salir)**

VM (añadir a `BenchmarkViewModel.cs`):

```csharp
[ObservableProperty] private string _dnsSummary = string.Empty;
[ObservableProperty] private string _fluencySummary = string.Empty;
[ObservableProperty] private string _bootSummary = string.Empty;

public async Task MeasureDnsAsync(CancellationToken ct)
{
    try
    {
        var provider = new DnsBenchmarkProvider();
        var results = await provider.BenchmarkAsync(null, ct);
        var best = DnsBenchmarkProvider.PickBest(results);
        DnsSummary = best is null ? "Sin respuesta DNS medible." :
            $"Mejor: {best.Resolver} {best.MedianLatencyMs:0.0} ms (jitter {best.JitterMs:0.0} ms, {best.Successes}/{best.Attempts})";
        DnsLastResults = results; // propiedad para pintar barras + guardar DnsAfterMs si hay sesión
    }
    catch (OperationCanceledException) { DnsSummary = "Medición DNS cancelada."; }
    catch (Exception ex) { DnsSummary = $"{ErrorCodes.UiBenchmarkFailed}: DNS no medido. [Técnico: {ex.GetType().Name}]"; }
}

public async Task MeasureFluencyAsync(IFrameCapture capture, CancellationToken ct)
{
    try
    {
        var result = await capture.CaptureAsync(TimeSpan.FromSeconds(10), ct);
        if (result is null) { FluencySummary = "No disponible en este equipo"; return; }
        var stats = BenchmarkAnalyzer.AnalyzeFrameTimes(result.FrameTimesMs);
        FluencySummary = $"avg {stats.AverageFps:0} FPS · 1% low {stats.OnePercentLowFps:0} · P99 {stats.P99FrameTimeMs:0.0} ms · DPC {result.DpcPercent:0.0}%";
    }
    catch (OperationCanceledException) { FluencySummary = "Medición cancelada."; }
    catch (Exception ex) { FluencySummary = $"{ErrorCodes.UiBenchmarkFailed}: fluidez no medida. [Técnico: {ex.GetType().Name}]"; }
}

public string ExportSessionCsv(BenchmarkSession session)
{
    var sb = new StringBuilder("metrica;antes;despues;delta_%\n");
    void Row(string name, double before, double after) =>
        sb.AppendLine($"{name};{before:0.00};{after:0.00};{SystemBenchmarkRunner.PercentChange(before, after):+0.00;-0.00}");
    Row("cpu_ops", session.Before.CpuScore, session.After?.CpuScore ?? 0);
    Row("mem_gbs", session.Before.MemoryBandwidthGbs, session.After?.MemoryBandwidthGbs ?? 0);
    Row("disk_r_mbs", session.Before.DiskReadMbs, session.After?.DiskReadMbs ?? 0);
    Row("disk_w_mbs", session.Before.DiskWriteMbs, session.After?.DiskWriteMbs ?? 0);
    return sb.ToString();
}
```

`using CAO.Core.Engine;` para `BenchmarkAnalyzer` (ya lo usan los tests; verificar using en VM). `DnsLastResults` como `IReadOnlyList<DnsBenchmarkResult>?` (propiedad normal con `SetProperty`, no observable salvo que la UI la necesite).

Code-behind:
- `CancellationTokenSource _runCts` por medición; `OnNavigatedFrom` → `_runCts?.Cancel()` (cancelación vinculada a navegación) + limpiar pendiente NO (el pendiente sobrevive para volver).
- `OnNavigatedTo`: `ApplyTexts()` + `PlayEntrance` (intacto) + rellenar `BootText` vía `BootInfoProvider` en try/catch + si `UiState.PendingBenchmarkOptimizationId` no vacío → mostrar banner + auto-arrancar baseline con `RunWithVm(isBaseline:true)` usando timeout `BenchmarkStore.TimeoutForSystemDrive()`.
- `RunWithVm(bool isBaseline, Button)`: CTS = `CancellationTokenSource.CreateLinkedTokenSource(_runCts.Token)` + `CancelAfter(BenchmarkStore.TimeoutForSystemDrive())` (sustituye los 120 s fijos); llama `_vm.RunForOptimizationAsync(optId, category, isBaseline, token)` donde `optId = pendiente o "manual"`, `category = pendiente o ""`; `SetCat` Working antes / Celebrate-Warn-Idle después según `Verdict` (try/catch); repintar tabla+sparkline desde la sesión guardada (recargar vía `LoadJsonAsync<BenchmarkSession>` del path de la sesión — la VM expone `LastSessionPath`).
- Export CSV: `FileSavePicker` WinUI (`WindowNative.GetWindowHandle` — comprobar cómo lo hace el proyecto; si no hay precedente, guardar en `%USERPROFILE%\Documents\cao-benchmark-{stamp}.csv` y mostrar la ruta; NO inventar interop sin precedente — decisión en report).
- DNS barras: pintar `DnsLastResults` (top 4 por mediana) como filas `TextBlock + Rectangle` (ancho proporcional, cap 160 px).
- Textos hardcodeados `:79` ("Midiendo 3 trials con warmup…") → `Localizer.Get("benchmark.measuring")`.

- [ ] **Step 4: OptimizePage — botón "Medir antes/después"**

En la fila de cada optimización (junto a Aplicar): `Button Content={Localizer benchmark.measureOpt}` → handler:

```csharp
private void OnMeasureImpactClick(string optimizationId, string category)
{
    var uiState = AppHost.Resolve<ViewModels.UiState>();
    uiState.PendingBenchmarkOptimizationId = optimizationId;
    uiState.PendingBenchmarkCategory = category;
    if (MainWindow.Current is not null) MainWindow.Current.SelectRoute("benchmark");
    else AppHost.Resolve<Navigation.INavigationService>().Select("benchmark");
}
```

(Adaptar firma al patrón real de botones por fila de `OptimizePage.xaml` — leer el DataTemplate antes.)

- [ ] **Step 5: Verificación**

Run: `dotnet build CA-O.sln -c Release` + `dotnet test tests/CA-O.UI.Tests -c Release` + `dotnet test tests/CA-O.Benchmark.Tests -c Release`
Expected: PASS todo. Captura manual de la página (Idle+ banner) imposible en CI — declarar `verificado-vía-build`.

- [ ] **Step 6: Commit**

```bash
git add src/CA-O.UI/
git commit -m "feat(bench): UI con tabla, sparkline, CSV, DNS, arranque y fluidez"
```

### Task 6: Historial alimentado + E2E + suite verde final

**Files:**
- Modify: `src/CA-O.UI/ViewModels/BenchmarkViewModel.cs` (log historial)
- Test: `tests/CA-O.Integration.Tests/E2EFlowsTests.cs` (ampliar dummy `:143-150` — leerlo primero)

**Interfaces:**
- Consumes: Task 3-5 (sesión + veredicto). `JsonHistoryLogger(string? filePath = null)` existe en Infra con default al history real.
- Produces: cada A/B completado deja `HistoryEntry{ Operation="benchmark", OptimizationId, Success, BenchmarkSummary }` visible como `bench:` en Historial (línea 88 ya lo muestra).

- [ ] **Step 1: Leer el E2E dummy y el ctor de JsonHistoryLogger, luego escribir el test**

Leer `tests/CA-O.Integration.Tests/E2EFlowsTests.cs:130-160` y `src/CA-O.Infrastructure/Persistence/JsonHistoryLogger.cs:1-60`. Después añadir test de integración (en el proyecto de integración que ya referencie Infra+Shared):

```csharp
[Fact]
public async Task BenchmarkSession_ProducesHistoryBenchLine()
{
    var dir = Path.Combine(Path.GetTempPath(), $"cao-bench-e2e-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    try
    {
        var header = new BenchmarkRunHeader("b", DateTime.UtcNow, 26200, "", "", 0, "ac", MachineHash: "h1");
        var baseline = new SystemBenchmarkResult(header, 50_000, 20, 400, 350, TimeSpan.Zero);
        var after = baseline with { DiskReadMbs = 460 };
        var comparison = SystemBenchmarkRunner.CompareFull(baseline, after, OptimizationCategory.Storage);

        var logger = new JsonHistoryLogger(Path.Combine(dir, "history.jsonl"));
        logger.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            OptimizationId = "cleanup-windows-temp",
            Operation = "benchmark",
            Success = comparison.VerdictEs == "Mejora medible",
            BenchmarkSummary = $"disco R {comparison.DiskReadDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs}",
        });

        var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "history.jsonl"));
        Assert.Single(lines);
        Assert.Contains("bench", lines[0], StringComparison.OrdinalIgnoreCase);
    }
    finally { try { Directory.Delete(dir, recursive: true); } catch { } }
}
```

(Verificar el using real: `SystemBenchmarkRunner` es `CAO.Infrastructure.Benchmarking`; si el proyecto Integration no referencia Infra, el test vive en `CA-O.Benchmark.Tests` con una aserción equivalente sobre el formato del summary — decisión del implementador con ruling en report.)

- [ ] **Step 2: VM escribe historial tras cada A/B**

Al final de la rama `else` (after) de `RunForOptimizationAsync`, tras guardar la sesión:

```csharp
try
{
    new JsonHistoryLogger().Log(new HistoryEntry
    {
        TimestampUtc = DateTime.UtcNow,
        AppVersion = AppVersion.Semantic, // si no accesible, omitir (tiene default null)
        OptimizationId = optimizationId,
        Operation = "benchmark",
        Success = comparison.VerdictEs != "Regresión",
        BenchmarkSummary = $"CPU {comparison.CpuDeltaPercent:+0.0;-0.0}% MEM {comparison.MemoryDeltaPercent:+0.0;-0.0}% R {comparison.DiskReadDeltaPercent:+0.0;-0.0}% W {comparison.DiskWriteDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs}",
    });
}
catch { /* el historial nunca rompe el benchmark */ }
```

`using CAO.Infrastructure.Persistence;` — verificar namespace real de `JsonHistoryLogger` (grep: `Persistence/JsonHistoryLogger.cs` → namespace probable `CAO.Infrastructure.Persistence`).

- [ ] **Step 3: Suite completa verde**

Run: `dotnet build CA-O.sln -c Release` + `dotnet test CA-O.sln -c Release`
Expected: PASS — 991+ tests previos + ~10 nuevos (total esperado ≈ 1000-1005; el número exacto va al report).

- [ ] **Step 4: Commit**

```bash
git add src/CA-O.UI/ViewModels/BenchmarkViewModel.cs tests/
git commit -m "feat(bench): A/B alimenta el historial y E2E lo verifica"
```

---

## Self-Review

**1. Spec coverage (§4.1-4.4, criterio §8-l112):**
- CPU ops/s multihilo + CpuMs/CV → Task 1. ✅
- MEM buffers reusados + NoGCRegion + CV>5% → InsufficientData → Tasks 1+3. ✅
- Disco 256 MB sistema + WriteThrough + seq + 4K IOPS → Task 2. ✅
- `RunTrialsAsync` respeta `warmup` + mediana + CV → Task 1. ✅
- Fases con tiempos + header completo (UBR, appVer, machineHash SHA256-16, cpuName, ProcessorCount sin abusar Resolution, power, bgCpu, gpu) → Task 2 (bgCpu se rellena en captura Task 4; header lleva el campo). ✅
- `Compare/CompareFull` veredicto por categoría; disco decide en Storage/Cleanup → Task 2. ✅
- `BenchmarkPolicy` intacto como fuente única; `Analyzer` default → 3.0 → Task 2. ✅
- `{txid}.json` + baseline Header + TTL 7 d + invalidación machineHash/osUBR/appVer/power + validación deserialización → Task 3 (sesiones `{optid}-{stamp}.json`; TxId-servicio no llega a la UI por el pipe — ruling documentado: la sesión por optimización+timestamp es la identidad A/B en UI). ✅
- Timeout adaptativo 120/300 + cancelación a navegación → Tasks 3+5. ✅
- Textos `:78` → Localizer → Task 5. ✅
- `IFrameCapture` + DXGI + avg/1%low/P99 + DPC + `BenchmarkResult{Header+FrameTimes+Metric+Extra}` con productor → Task 4 (BenchmarkResult se rellena en Task 5 fluidez: `new BenchmarkResult { Header = ..., FrameTimes = stats, Metric = avg, MetricName="avg_fps", MetricUnit="fps", ExtraMetrics={1%low,P99,DPC} }` — NOTA al implementador: añadir esto en Task 5 al medir fluidez). ✅
- Stepper conservado, tabla deltas, sparkline Polyline, export CSV, botón Optimize con TxId→optId, auto-run tras Commit (auto-run al navegar con pendiente + banner; ruling: auto-run literal tras Commit cruzaría procesos vía pipe sin TxId de vuelta — documentado) → Task 5. ✅
- `HistoryViewModel:88 bench:` alimentado → Task 6. ✅
- Cards OC vendor intactas (no se tocan) → ninguna tarea las modifica. ✅
- Tests: RunAsync real CpuScore>0, Compare disco-gated, header round-trip+TTL, Analyzer 3.0, IFrameCapture sintético → Tasks 1-4+6. ✅
- Boot `LastBootUpTime` + DNS en Benchmark → Tasks 4+5 (Analyze intacto: DNS se REUSA, no se mueve — ruling que honra "sin romper Analyze"). ✅

**2. Placeholder scan:** sin TBD/TODO; cada step trae código concreto, comandos exactos y expected. Los 3 rulings permitidos (System.Management, CSV picker, DXGI inviable, ubicación test E2E) exigen decisión documentada en report, no silencio. ✅

**3. Type consistency:** `SystemBenchmarkResult` nuevos campos con defaults usados igual en Tasks 1-3-5; `CompareFull(baseline, after, category?)` firma única; `BenchmarkSession(Before, After, DnsBeforeMs, DnsAfterMs, Category, OptimizationId)` posicional consistente en test Task 3 y uso Tasks 5-6; `FrameCaptureResult(FrameTimesMs, DpcPercent, InterruptPercent)` igual en test e interfaz; `BenchmarkRunHeader` nuevos campos nombrados en tests. `OptimizationCategory.Storage` existe en el enum (línea 8 Enums.cs). ✅
