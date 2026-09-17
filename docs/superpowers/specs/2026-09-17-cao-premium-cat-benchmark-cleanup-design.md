# CA-O Premium: gato animado + benchmark útil + limpieza ampliada — Design Spec

Fecha: 2026-09-17 | Enfoque aprobado: 1 (nativo WinUI, cero dependencias nuevas) | Secciones 1-4 aprobadas en chat.

## 1. Objetivo y alcance

Convertir CA-O (WinUI 3 / WindowsAppSDK 2.4, .NET 10, 88 optimizaciones, 991 tests) en app premium, llamativa y única, con:
- (a) Mascota gato vectorial animada + imágenes premium coherentes Mica/accent.
- (b) Benchmark A+B útil: antes/después real (CPU/MEM/disco/boot/espacio) + fluidez gaming ligera (avgFPS, 1% low, P99, DPC).
- (c) Catálogo limpieza ampliado y saneado (dedup, fix %TEMP% usuario, nuevas rutas seguras).
- (d) Optimizaciones solo-verificadas, separando Repairs/Diagnostics/Restores del batch default.

Fuera de alcance v1: Lottie/Win2D externos, PresentMon bundleado, ETW kernel pesado, 3D Pixar, telemetría (sigue 0).

## 2. Arquitectura general

- Fuente única de estado mascota: `ViewModels/UiState.cs` += `MascotMood` (Idle/Working/Celebrate/Warn/Sleep) + `MascotMessage`. `AppHost.cs` registra `MascotViewModel` singleton si se necesita lógica; si basta binding directo a `UiState`, sin VM nueva.
- Nuevo control `Controls/MascotView.xaml/.cs` (UserControl, VisualStates + Storyboards Composition). Gate `Accessibility/ReducedMotion.cs`: si `AnimationsEnabled==false`, estado estático, sin loops.
- Tokens: `Resources/DesignTokens.xaml` += `CaoCat*Brush`, `CaoPremiumGradient`, `CaoMascotAnimationDuration`. Estilos existentes (`CaoCardStyle`, `CaoHeroCardStyle`, `CaoInteractiveCardStyle`) se reutilizan, no se duplican.
- Pipe privilegiado (`\\.\pipe\CA-O.Privileged.v1`, IpcProtocol v2) y transacción `PRECHECK→COMPAT→SNAPSHOT→APPLY→VERIFY→COMMIT` + rollback intactos. Sin PowerShell en hot-path (`CommandPolicy`).
- Benchmark y limpieza se comunican vía `TxId`: Optimize genera TxId → auto-benchmark antes → Apply → auto-benchmark después → `TransactionReport.Benchmark` → `HistoryEntry.BenchmarkSummary` (hoy vacío, se alimenta).

## 3. Mascota gato + imágenes premium (Sección 1)

### 3.1 MascotView (XAML vectorial, sin PNG pesado)
- Paths: cuerpo/cabeza/orejas/cola/patas (`Path`), ojos (`Ellipse`), mejillas. Colores por token para light/dark.
- VisualStates:
  - Idle: respiración ScaleY 1.0↔1.03 loop 2.4s + parpadeo ojos ScaleY→0.1 cada ~4s + cola sway.
  - Working: patita golpea (Rotate ±12°, 500ms loop) + cola menea + burbuja "…" + `ProgressRing` opcional al lado.
  - Celebrate: salto TranslateY -18px CubicEase + ojos ^ ^ + 6 confetti `Ellipse` Composition fade-out. Se dispara al completar limpieza/benchmark con mejora.
  - Warn: orejas atrás (Rotate), cola erizada (ScaleX 1.15), color accent cálido. Regresión benchmark / confirmación destructiva.
  - Sleep: ojos cerrados + `TextBlock` "Zzz" fade loop. Idle >5min o Settings/about.
- Transiciones 250ms entre estados. `AutomationProperties.Name="Mascota CA-O, estado X"`. Si falta asset de imagen, el gato XAML sigue visible (fallback).
- Tests UI (72 existentes + nuevos): MascotView respeta ReducedMotion (cero Storyboards activos), cambios de `MascotMood` disparan estado correcto.

### 3.2 Assets imagen
- `src/CA-O.UI/Assets/`: `mascot-hero.png` (Dashboard hero), `empty-history.png`, `empty-restore.png`, `header-limpieza.png`, `header-benchmark.png`, `cat-sleep.png`. Estilo: gradiente accent, Mica, bordes 12px, coherente con `Cao*SoftBrush 12-14%`.
- Referencia `ms-appx:///Assets/`. Registrar en `.csproj` como `Content`. `AutomationProperties` + tooltip. Fallback: `x:Load` falso / `ImageFailed` → colapsar imagen, mantener gato.
- Hero Dashboard: `CaoHeroCardStyle` + `CaoAccentRailStyle` + saludo + `AlertBars` existentes + gato a la derecha; `UiAnimations.PlayEntrance` extendido a stagger hero (26px+fade 380ms step 45ms ya existe).

### 3.3 Anclajes por página
- Dashboard: saludo + mood según salud (Idle/Warn) + hero image.
- Limpieza: Working durante `RunCleanupAsync`, Celebrate + total MB liberados (CountUp existente, `DispatcherTimer` 30ms).
- Benchmark: Working midiendo, Celebrate (mejora >3%) / Warn (regresión) según veredicto.
- History/Restore vacíos: empty-state imagen + gato Sleep + CTA.
- `CardHover` attached (Scale 1.02/150ms) se aplica a cards nuevas.

## 4. Benchmark útil A+B (Sección 2)

### 4.1 Fix cargas (archivos exactos)
- `Infrastructure/Benchmarking/SystemBenchmarkRunner.cs`:
  - CPU `:146`: `return count` → `ops/sec = count / sw.Elapsed.TotalSeconds`; `Parallel.For` con `Environment.ProcessorCount`; reporta `CpuScore, CpuMs, CV%`. Multi-thread refleja plan energía/HAGS/Game Mode.
  - MEM `:171-188`: buffers estáticos reusados (evita `Random.Shared.NextBytes` por trial + `Array.Copy x4` con GC), warmup fuera de crono, `GC.TryStartNoGCRegion` con fallback, mediana + CV; si `CV>5%` → `InsufficientData`.
  - Disco `:190-231`: 256MB prealocado en unidad sistema (no solo `%TEMP%` cacheado), `FileOptions.WriteThrough` (+ NO_BUFFERING vía P/Invoke donde sea seguro), seq + 4K random, `Flush(true)` + disclaimer si caché; mide R/W MB/s + IOPS 4K.
  - `RunTrialsAsync:44-53`: respeta param `warmup` (hoy ignorado, siempre 1), `trials=3` + mediana, calcula stddev/CV, descarta varianza alta.
  - `RunAsync:96-114`: fases con tiempos por métrica (no solo `Elapsed` total); `Header` completo: `{WorkloadId, TimestampUtc, Build+UBR, appVer, machineHash (no identificable), cpuName, ProcessorCount, power AC/battery (kernel32 GetSystemPowerStatus), bgCpu%, gpuName/driver}`. `Resolution` deja de abusarse para ProcessorCount.
  - `Compare:123` + `CompareFull:116-141`: veredicto por categoría (ver 4.2). Disco deja de ser informativociego.
- `Core/Benchmark/BenchmarkPolicy.cs`: fuente única `MinimumEffectPercent=3.0`, `MeasuredRuns=3`, `WarmupRuns=1`.
- `Core/Benchmark/BenchmarkAnalyzer.cs:38`: default `significanceThresholdPercent=1.0` → `BenchmarkPolicy.MinimumEffectPercent` (3.0). Sin llamadas prod hoy; se cablea (4.3).

### 4.2 Veredicto por categoría
- `CompareFull` vota según `OptimizationCategory` de la Tx: Storage/Cleanup→disco+IOPS+espacio liberado; Network→DNS (`DnsBenchmarkProvider`, hoy exiliado en AnalyzePage `:532-575`); Startup→boot (`Win32_OperatingSystem.LastBootUpTime` + launch-latency app); resto→CPU/MEM. Regla: `>3%` mejora medible, `<-3%` regresión, else sin mejora; con CV alta o cambio power/build → `InsufficientData`, nunca veredicto falso.
- `BenchmarkViewModel.cs`: `baseline.json` único → `benchmarks/{txid}.json` + `baseline.json` con `Header` + TTL 7d; invalida si cambia machineHash/osUBR/appVer/power. Definiciones: `machineHash` = SHA256 truncado 16 chars de `MachineGuid + CPU name` (no reversible, solo comparación); timeout adaptativo = 120s base SSD, 300s si `MediaType==HDD` o `Verify` previo >90s, con cancelación vinculada a navegación. Validación deserialización.
- Textos hardcodeados `:78` → `Localizer` (`benchmark.baseline/after` ya existen, ampliar).

### 4.3 FPS/fluidez gaming (B ligero v1)
- Nueva `IFrameCapture` + impl DXGI frame-times (sin PresentMon externo): avgFPS, 1% low, P99 stutter + `%DPC/%Interrupt` (PerformanceCounter ya referenciado) durante ventana de 10s. Puerta abierta a PresentMon futuro sin cambiar interfaz.
- `BenchmarkModels.cs:32-60` (`BenchmarkRunHeader/FrameTimeStatistics/BenchmarkResult`) por fin tiene productor: `BenchmarkResult{Header+FrameTimes+Metric+Extra}` por TxId.
- UI BenchmarkPage: stepper 1-4 conservado; grid Baseline/Comparación + tabla deltas + sparkline (línea `Polyline`, sin lib) + export CSV; botón "Medir antes/después de esta optimización" en Optimize pasa TxId; auto-run tras Commit; `HistoryViewModel:88` alimenta `bench:`; cards pedagógicas (OC vendor MSI/AMD/XTU) intactas.
- `BenchmarkPage.xaml.cs:81` CTS frágil → cancelación vinculada a navegación + timeout adaptativo.

### 4.4 Tests benchmark
- Existentes: `BenchmarkTrialsTests`, `BenchmarkStatisticsTests` (7 tests mediana/suelo 3%), `E2EFlowsTests:143-150` dummy.
- Nuevos: `RunAsync` real assert `CpuScore>0` y varía con carga; `Compare` disco-gated (Storage gana por disco aunque CPU 0%); `Header` round-trip + TTL/invalidación; `Analyzer.Compare` usa 3.0; `IFrameCapture` sintético avg/1%low/P99.

## 5. Limpieza ampliada + catálogo saneado (Sección 3)

### 5.1 Dedup + fix usuario
- Fusionar `cleanup-windows-update-cache` (Risk Low, chequea Exists, rollback servicios) con `disk-cleanup-system-files` (Risk Moderate, mismo `SoftwareDistribution\Download` + stop/start wuauserv+bits). Quedarse con uno, alias legacy al otro para compat. Conteo 88→87 honesto.
- Fix `%TEMP%` usuario: `CleanupWindowsTemp.cs` hoy resuelve a SYSTEM. Añadir resolución usuario interactivo en `CA-O.Privileged` (WTS/env usuario) con fallback seguro a SYSTEM. Nunca elevar rutas usuario sin validación `CommandPolicy` + `PathRoot.Length>3` (ya en `CleanupService:87`).

### 5.2 Nuevas rutas seguras (patrón CleanupAppCaches)
- `Prefetch/*.pf>30d` solo ficheros; `Windows\Logs\CBS\*.log>30d`; `Minidump>30d` + `LiveKernelReports\*.dmp>30d` + `MEMORY.DMP>30d` con confirmación; `INetCache\Content.Outlook`; Edge/Chrome/Teams `Cache/Code Cache/GPUCache` (nunca `Local Storage/IndexedDB`, nunca `Spotify/Storage` offline). `WinSxS` solo `StartComponentCleanup` (nunca `ResetBase` en batch). PROHIBIDO: `DataStore.edb`, tocar VPN/virtuales/loops en Nagle, `iconcache` ya cubierto.
- Implementar como nuevas `OptimizationDefinition` en `Core/Optimizations/Storage/` heredando `TempFileCleanupOptimization` (TopDirectoryOnly, cutoff, skip en uso, `NotReversible`, Verify `_lastDeleted` — documentar volatilidad: `Unknown` tras reinicio).
- UI: 6 cards existentes intactas (`CardTemp/CardDisk/CardWinSxS/CardTimer/CardSense/MiniStack`, grid 2col Wide≥1100/1col Narrow, `CaoCardStyle`, Automation+ToolTip). Nueva 7ª card "Limpieza profunda" + MB por card + `ConfirmIfNeededAsync` para destructivas + gate `ExpertMode` ResetBase. `QuickCleanIds[5]` + `HeavyIds` + timeouts 90s/20min intactos. `SHEmptyRecycleBin` en hilo STA dedicado intacto. `SetTimerResolutionAsync{5000,10000,156250}` intacto.

### 5.3 Catálogo honesto (sin romper compat)
- `OptimizationCatalog.All` se proyecta en: `Optimizations` (perf real, batch default) / `RepairActions` (restarters `RestartWindowsServiceOptimization` + flush-dns + reset-network-stack, fuera de batch) / `Diagnostics` (4 read-only) / `Restores` (5 restores). Proyección, no borrado: IDs legacy siguen resolviendo.
- `Detect` real: `EnsureTrim` relee `fsutil`, `EnableRss` `netsh int tcp show global` + prop NIC, `WifiScan` `netsh wlan show settings`, `RemoveUnusedPowerPlans` `powercfg /L` + snapshot GUIDs (hoy `NotApplied` siempre → nagging).
- Peligrosos fuera de batch + exigen restore-point (`create-restore-point` vía `SRSetRestorePoint`): `disable-vbs` (rompe WSL2/Docker/Vanguard), `ResetBase` (irreversible), `reset-network-stack` (precondición síntoma), `disable-dynamic-tick` degradar a ExpertOnly/placebo documentado, `mouse-driver-queue-trim`/`mmcss`/`disable-nagle` acotar (solo físicas, excluir latencia>100ms, documentar tradeoffs audio/eventos).
- Nuevo test contrato `NoDuplicateTargets` (mismo registry path / mismo dir / mismo `SystemCommandKey`) para impedir futuros duplicados en CI.
- Docs: `OPTIMIZATION_REFACTORING_STATUS.md` + `audit-*.ps1` marcar históricos (hablan de 19/21); `OPTIMIZATION_INVENTORY.md` regenerar desde `All`.

## 6. Optimizaciones verificadas + flujo + pruebas (Sección 4)

- Research: Context7 primero para APIs, luego fuentes citadas; nada de memoria para APIs 2025-26. Cada optimización cita doc Microsoft o se marca `Empirical` con tradeoff explícito.
- Se mantienen: `CommandPolicy`, transacción + `CrashRecoveryService`, `GameCompatibilityPolicy(24)` + anti-cheat, `OptimizationConflicts.cs` (power plans), 0 telemetría, `requireAdministrator` + servicio `CAO.Privileged` SYSTEM `start=demand`, IPC 17 ops allowlist + `ReplayCache` + `AdministratorsOnlyAuthorizer`.
- Flujo: Optimize(TxId) → auto-bench antes → Apply pipe → auto-bench después → `TransactionReport.Benchmark` → Historial sparkline + revertir un click (Restore). Fallos: `InsufficientData`, timeout adaptativo, `ConfirmIfNeededAsync`, rollback auto.
- Pruebas/verificación: `dotnet build + test + lint` reales del repo; 991 tests deben seguir verdes + nuevos (4.4, 5.3, 3.1). Si no corre en verde, no está terminado. `verification-before-completion` antes de cada commit lógico. Memoria duradera al cerrar (comandos, decisiones, gotchas).

## 7. Archivos a tocar (lista cerrada v1)

- UI: `DesignTokens.xaml`, `UiState.cs`, `AppHost.cs`, nuevo `Controls/MascotView.xaml/.cs`, `Helpers/UiAnimations.cs`, `DashboardPage.xaml`, `LimpiezaPage.xaml(.cs)`, `BenchmarkPage.xaml(.cs)`, `OptimizePage`, `HistoryViewModel.cs`, `Resources/Localizer.cs`, `src/CA-O.UI/Assets/*`, `MainWindow.xaml` (anclaje hero, sin cambiar nav salvo que haga falta).
- Benchmark: `SystemBenchmarkRunner.cs`, `BenchmarkPolicy.cs`, `BenchmarkAnalyzer.cs`, `BenchmarkModels.cs`, nueva `IFrameCapture.cs` + `DxgiFrameCapture.cs`, `BenchmarkViewModel.cs`, `DnsBenchmarkProvider.cs` (mover uso a Benchmark sin romper Analyze).
- Limpieza/catálogo: `OptimizationCatalog.cs`, `TempFileCleanupOptimization.cs`, nuevas `Storage/*.cs` (Prefetch, CbsLogs, CrashDumpsExtended, OutlookCache, BrowserCodeCache), `CleanupWindowsTemp.cs` (fix usuario), fusión WU-cache, `OptimizationConflicts.cs` si hace falta, `CaOPaths.cs` (rutas benchmarks por TxId).
- Tests: `CA-O.Benchmark.Tests/*`, nuevo `NoDuplicateTargetsTests`, nuevos limpieza, nuevos UI.
- Docs: `OPTIMIZATION_INVENTORY.md` (regenerar), este spec. No tocar: `PrivilegedPipeService.cs`, `IpcProtocol.cs`, `CommandPolicy.cs` salvo allowlist estrictamente necesaria.

## 8. Criterios de aceptación

- [ ] Gato visible en Dashboard/Limpieza/Benchmark/vacíos, 5 moods animados, estático con ReducedMotion, sin dependencias nuevas, build verde.
- [ ] Benchmark CPU `ops/sec>0` multihilo, MEM sin GC-noise, disco sistema seq+4K, veredicto por categoría, `Analyzer` a 3.0, FPS avg/1%low/P99 visibles, historial por TxId + TTL/invalidación + CSV.
- [ ] Limpieza: dedup WU, fix %TEMP% usuario, ≥5 nuevas rutas seguras con confirmación donde toca, 7ª card con MB, `NoDuplicateTargets` verde.
- [ ] Catálogo separado Repairs/Diagnostics/Restores, peligrosos fuera de batch + restore-point, 991 tests previos verdes + nuevos verdes, inventario regenerado.
- [ ] `dotnet build -c Release + dotnet test -c Release` verdes. Sin telemetría nueva. Sin PowerShell en hot-path.
