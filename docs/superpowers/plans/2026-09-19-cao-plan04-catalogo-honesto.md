# CA-O Plan 04 — Catálogo honesto Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separar el catálogo en proyecciones honestas (batch perf / repairs / diagnostics / restores), sacar los peligrosos del batch con restore-point obligatorio, dar Detect real a los que hoy devuelven siempre NotApplied/Unknown, y acotar los placebos — sin romper compat (All intacto, 92 IDs resuelven igual).

**Architecture:** Proyección, no borrado: `CatalogProjections` (fichero nuevo) filtra `OptimizationCatalog.All` por sets de IDs; el batch (Optimize + Analyze) consume `BatchDefault`; Preview/Resolve/motor siguen sobre `All`. Detect reales solo con señales registry/BCL o comandos read-only con try/catch→Unknown (Detect no tiene executor por firma).

**Tech Stack:** C# 13, .NET 10, WinUI 3 / WASDK 2.4 (sin cambios UI salvo tooltips), xUnit 2.9.2.

**Spec:** `docs/superpowers/specs/2026-09-17-cao-premium-cat-benchmark-cleanup-design.md` (§5.3 líneas 87-92, aceptación línea 114).

## Global Constraints

- Solo C# + BCL; cero `.csproj`/NuGet nuevos (System.Management ya referenciado si hiciera falta WMI; preferir registry/BCL).
- `dotnet build CA-O.sln -c Release` 0 errores/0 warnings + `dotnet test CA-O.sln -c Release` todo verde antes de cada commit.
- Commits en español, formato `feat(cat): …` / `fix(cat): …` (sin tildes si la shell las corrompe; documentarlo en el report).
- Textos visibles ES + EN a la par; no pisar `Localizer` (estas tasks no añaden claves salvo que un tooltip lo exija, y entonces es+en).
- `OptimizationCatalog.All` sigue con 92 entradas; `LegacyIds` 3; `AllLegacy` 66; alias WU intacto.
- `IpcProtocol`/`CommandPolicy`/`PrivilegedPipeService` intactos salvo allowlist estrictamente necesaria (no prevista).
- Detect nunca lanza: try/catch → `Unknown` (patrón `RecommendationEngine.Build` + `FindLiveWifiInterface`).
- Respetar `ReducedMotion`/mascota: N/A (sin cambios UI salvo tooltips).

---

### Task 1: Proyecciones del catálogo + test de partición

**Files:**
- Create: `src/CA-O.Core/Optimization/CatalogProjections.cs`
- Create: `tests/CA-O.Core.Tests/CatalogProjectionTests.cs`

**Interfaces:**
- Consumes: `OptimizationCatalog.All` (92 `IOptimization`, `Definition.Id`), `LegacyIds`, `RetiredAliases`/`CanonicalIdFor`.
- Produces: `CatalogProjections.RepairIds/DiagnosticIds/RestoreIds` (`IReadOnlySet<string>`, OrdinalIgnoreCase), `CatalogProjections.RepairActions/Diagnostics/Restores/BatchDefault` (`IReadOnlyList<IOptimization>`), usadas por Task 2 (batch) y Task 6 (docs).

**Contexto exacto (leer antes de codificar):**
- `src/CA-O.Core/Optimization/OptimizationCatalog.cs:17-40` (shape `All`, `LegacyIds`, `RetiredAliases`, `CanonicalIdFor`).
- Enumerar `src/CA-O.Core/Optimizations/Troubleshoot/*.cs` y clasificar cada `Id`: repair SÍ salvo `clear-icon-thumbnail-cache` (limpieza real, se queda en batch). Base `RestartWindowsServiceOptimization.cs:16` (Detect NotApplied siempre) confirma que son restarters.
- Diagnostics (4, verificar cada `Id` en su fichero): `gaming-display-refresh-rate-audit` (`Gaming/GamingDisplayRefreshRateAudit.cs:14`), `free-low-storage-space` (`Storage/FreeLowStorageSpace.cs:13`), `pending-reboot-maintenance` (`System/PendingRebootMaintenance.cs:11`), `optimize-startup-recovery-state` (`System/OptimizeStartupRecoveryState.cs:13`).
- Restores: enumerar todos los `Id = "restore-` en `src/CA-O.Core/Optimizations/` (vistos: `restore-tcp-checksum-offload`, `restore-udp-checksum-offload`, `restore-large-send-offload`, `restore-windows-tcp-congestion-default`, `restore-sysmain-default`, `restore-system-managed-pagefile`; el spec dice 5 pero el grep da 6 — incluir los que existan de verdad y documentar el conteo real en el report; NO inventar ni omitir ninguno).
- Repairs: restarters (`restart-windows-audio-services`, `restart-dns-client`, `restart-bluetooth-service`, `restart-print-spooler`, `restart-windows-search` + cualquier otra subclase de `RestartWindowsServiceOptimization` que aparezca en el grep) + `repair-windows-update`, `fix-microphone-access`, `restart-desktop-compositor`, `recover-windows-explorer`, `disable-bluetooth-absolute-volume`, `restart-windows-explorer`, `resync-system-clock`, `flush-dns-cache`, `reset-network-stack-repair`. Verificar cada Id contra su `Definition` (los listados vienen de grep de `Id =` y son literales, pero el implementador confirma uno por uno; si `Troubleshoot/` contiene algún fichero más no listado aquí, clasificarlo y documentarlo).

- [ ] **Step 1: Escribir `CatalogProjections.cs`**

```csharp
namespace CAO.Core.Catalog;

/// <summary>Proyecciones honestas sobre OptimizationCatalog.All (spec §5.3).
/// Proyección, no borrado: All sigue intacto y todo ID sigue resolviendo.</summary>
public static class CatalogProjections
{
    public static readonly IReadOnlySet<string> RepairIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "restart-windows-audio-services", "restart-dns-client", "restart-bluetooth-service",
        "restart-print-spooler", "restart-windows-search", "repair-windows-update",
        "fix-microphone-access", "restart-desktop-compositor", "recover-windows-explorer",
        "disable-bluetooth-absolute-volume", "restart-windows-explorer", "resync-system-clock",
        "flush-dns-cache", "reset-network-stack-repair",
    };

    public static readonly IReadOnlySet<string> DiagnosticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gaming-display-refresh-rate-audit", "free-low-storage-space",
        "pending-reboot-maintenance", "optimize-startup-recovery-state",
    };

    // Completar con el grep real de "restore- (ver contexto): incluir TODOS los existentes.
    public static readonly IReadOnlySet<string> RestoreIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "restore-tcp-checksum-offload", "restore-udp-checksum-offload",
        "restore-large-send-offload", "restore-windows-tcp-congestion-default",
        "restore-sysmain-default", "restore-system-managed-pagefile",
    };

    public static IReadOnlyList<IOptimization> RepairActions =>
        OptimizationCatalog.All.Where(o => RepairIds.Contains(o.Definition.Id)).ToList();

    public static IReadOnlyList<IOptimization> Diagnostics =>
        OptimizationCatalog.All.Where(o => DiagnosticIds.Contains(o.Definition.Id)).ToList();

    public static IReadOnlyList<IOptimization> Restores =>
        OptimizationCatalog.All.Where(o => RestoreIds.Contains(o.Definition.Id)).ToList();

    /// <summary>Batch default: perf real. Excluye repairs/diagnostics/restores.</summary>
    public static IReadOnlyList<IOptimization> BatchDefault =>
        OptimizationCatalog.All.Where(o =>
            !RepairIds.Contains(o.Definition.Id) &&
            !DiagnosticIds.Contains(o.Definition.Id) &&
            !RestoreIds.Contains(o.Definition.Id)).ToList();
}
```

NOTA: `using CAO.Core.Abstractions;` para `IOptimization`. Si algún Id del set no existe en `All`, la proyección lo ignora en silencio — por eso el test de partición (Step 2) es obligatorio: caza sets desincronizados.

- [ ] **Step 2: Escribir `CatalogProjectionTests.cs` (falla sin el fichero del Step 1)**

```csharp
using CAO.Core.Catalog;
using Xunit;

namespace CAO.Core.Tests;

public sealed class CatalogProjectionTests
{
    [Fact]
    public void Projections_Partition_All_Exactly()
    {
        var all = OptimizationCatalog.All.Select(o => o.Definition.Id).ToList();
        var projected = CatalogProjections.RepairActions
            .Concat(CatalogProjections.Diagnostics)
            .Concat(CatalogProjections.Restores)
            .Concat(CatalogProjections.BatchDefault)
            .Select(o => o.Definition.Id).ToList();
        Assert.Equal(all.Count, projected.Count);
        Assert.Empty(all.Except(projected, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(projected.Except(all, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(projected.Count, projected.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_Projected_Id_Resolves_In_All()
    {
        foreach (var id in CatalogProjections.RepairIds
            .Concat(CatalogProjections.DiagnosticIds)
            .Concat(CatalogProjections.RestoreIds))
        {
            Assert.Contains(OptimizationCatalog.All, o =>
                o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void BatchDefault_Has_No_Repairs_Diagnostics_Restores()
    {
        foreach (var o in CatalogProjections.BatchDefault)
        {
            Assert.DoesNotContain(o.Definition.Id, CatalogProjections.RepairIds);
            Assert.DoesNotContain(o.Definition.Id, CatalogProjections.DiagnosticIds);
            Assert.DoesNotContain(o.Definition.Id, CatalogProjections.RestoreIds);
        }
    }
}
```

NOTA: `Assert.DoesNotContain(string, IReadOnlySet<string>)` — xUnit resuelve el overload de colección (`IEnumerable<T>`); compila porque `HashSet<string>` es enumerable. Si el compilador se queja, usar `Assert.False(set.Contains(id))`.

- [ ] **Step 3: Ejecutar y ver en rojo**

Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~CatalogProjectionTests"`
Expected: FAIL con `CS0246 CatalogProjections` (no existe todavía).

- [ ] **Step 4: Crear el fichero del Step 1 y pasar a verde**

Run: mismo comando.
Expected: PASS (3/3). Si `Partition_All_Exactly` falla por un Id inexistente o un `restore-` no listado, ajustar los sets a la realidad del grep (documentar en el report), nunca relajar el test.

- [ ] **Step 5: Suite Core verde + commit**

Run: `dotnet build CA-O.sln -c Release` (0 err/0 warn) + `dotnet test tests/CA-O.Core.Tests -c Release` (todo verde).
Expected: PASS.

```bash
git add src/CA-O.Core/Optimization/CatalogProjections.cs tests/CA-O.Core.Tests/CatalogProjectionTests.cs
git commit -m "feat(cat): proyecciones Repairs/Diagnostics/Restores/BatchDefault con test de particion"
```

---

### Task 2: El batch consume BatchDefault (Optimize + Analyze)

**Files:**
- Modify: `src/CA-O.UI/ViewModels/OptimizeViewModel.cs:108`
- Modify: `src/CA-O.Infrastructure/Services/SystemAnalysisService.cs:60-61`
- Modify: `tests/CA-O.Core.Tests/` (nuevo `BatchDefaultRecommendationTests.cs`; si existe test que aserte recomendaciones sobre restarters/diagnostics, actualizarlo — buscar `BuildAll` en tests antes de tocar)

**Interfaces:**
- Consumes: `CatalogProjections.BatchDefault` (Task 1).
- Produces: recomendaciones del batch sin repairs/diagnostics/restores; `PreviewAsync` y `Resolve` intactos sobre `All` (compat Solucionar/Limpieza/pipe).

**Contexto exacto:**
- `OptimizeViewModel.cs:103-110`: `RefreshRecommendationsAsync` hace `BuildAll(catalog, _registry, context)` con `catalog = All`. Cambiar SOLO esa línea a `CatalogProjections.BatchDefault` (+ using `CAO.Core.Catalog`; el fichero ya referencia el namespace por nombre completo — añadir `using` o nombre completo, lo que rompa menos).
- `SystemAnalysisService.cs:60-61`: mismo cambio (usa `Core.Catalog`/`Core.Engine` ya importados por alias — verificar usings del fichero).
- NO tocar: `OptimizeViewModel.PreviewAsync(:36)` (preview individual debe resolver cualquier ID incl. repairs), `OptimizationEngine` líneas 102-103 y 847-848 (Resolve sobre All), `AppHost.cs:62`, `GamingViewModel.cs:56` (perfil gaming, fuera del batch default), `SolucionarPage`/`LimpiezaPage` (llaman al pipe por ID directo; el motor resuelve sobre All).
- Buscar en `tests/` usos de `BuildAll(` con `OptimizationCatalog.All` que aserten buckets de restarters/diagnostics/restores y actualizarlos a la nueva expectativa (batch ya no los incluye; la compat se verifica vía Resolve/Preview, no vía recomendaciones).

- [ ] **Step 1: Test batch-sin-repairs (falla antes del cambio)**

```csharp
using CAO.Core.Catalog;
using CAO.Core.Engine;
using CAO.Core.Tests.TestDoubles;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

public sealed class BatchDefaultRecommendationTests
{
    [Fact]
    public void BatchDefault_Excludes_Repairs_Diagnostics_Restores()
    {
        var context = new SystemContext(); // verificar ctor/propiedades requeridas en DTO/SystemContext.cs
        var recs = RecommendationEngine.BuildAll(CatalogProjections.BatchDefault, new MemoryRegistry(), context);
        var ids = recs.Select(r => r.OptimizationId).ToList();
        Assert.DoesNotContain(ids, id => CatalogProjections.RepairIds.Contains(id));
        Assert.DoesNotContain(ids, id => CatalogProjections.DiagnosticIds.Contains(id));
        Assert.DoesNotContain(ids, id => CatalogProjections.RestoreIds.Contains(id));
    }

    [Fact]
    public void Engine_Still_Resolves_Repair_Id_For_Solucionar_Compat()
    {
        var found = OptimizationCatalog.All.FirstOrDefault(o =>
            o.Definition.Id.Equals("flush-dns-cache", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(found);
    }
}
```

NOTAS: `MemoryRegistry` existe en `TestDoubles.cs` (verificado en Plan 03). `SystemContext` — leer su definición para construirlo (puede requerir props requeridas; si es complejo, reutilizar el helper que usen los tests existentes de `RecommendationEngine` — buscar `new SystemContext` en tests y copiar el patrón). `BuildAll` degrada Detect que lanza a Unknown (líneas 39-47), así que el test es hermético aunque algún Detect toque FS.

- [ ] **Step 2: Ejecutar en rojo**

Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~BatchDefaultRecommendationTests"`
Expected: FAIL — `CS0246` o, si el test compila (clase existe por Task 1), el primer test falla porque el batch aún usa `All`… NO: el test consume `BatchDefault` directamente, pasaría en verde sin el cambio. El rojo real es: escribir primero una aserción contra `RefreshRecommendationsAsync`/`SystemAnalysisService` es inviable en Core.Tests (son UI/Infra). Por tanto el rojo de esta task es el grep de alcance: verificar que `OptimizeViewModel.cs:108` y `SystemAnalysisService.cs:61` aún dicen `OptimizationCatalog.All` (documentar las 2 líneas en el report como evidencia pre-cambio). Si algún test existente aserta repairs en recomendaciones, ese test en rojo tras el cambio es el FAIL esperado — listar esos tests en el report.

- [ ] **Step 3: Aplicar los 2 cambios (una línea cada uno)**

```csharp
// OptimizeViewModel.cs:108 — antes:
var catalog = CAO.Core.Catalog.OptimizationCatalog.All;
// después:
var catalog = CAO.Core.Catalog.CatalogProjections.BatchDefault;
```

```csharp
// SystemAnalysisService.cs:60-61 — antes:
var catalog = Core.Catalog.OptimizationCatalog.All;
// después:
var catalog = Core.Catalog.CatalogProjections.BatchDefault;
```

- [ ] **Step 4: Suite verde (Core + Infra + UI)**

Run: `dotnet build CA-O.sln -c Release` + `dotnet test tests/CA-O.Core.Tests -c Release` + `dotnet test tests/CA-O.Infrastructure.Tests -c Release` + `dotnet test tests/CA-O.UI.Tests -c Release`.
Expected: PASS todo. Si un test existente esperaba repairs/diagnostics/restores en recomendaciones, actualizar su expectativa (batch ya no los incluye) citando el test y el cambio en el report — nunca borrar el test.

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.UI/ViewModels/OptimizeViewModel.cs src/CA-O.Infrastructure/Services/SystemAnalysisService.cs tests/CA-O.Core.Tests/BatchDefaultRecommendationTests.cs
git commit -m "feat(cat): el batch recomienda solo BatchDefault, Resolve sigue sobre All"
```

---

### Task 3: Peligrosos fuera del batch + restore-point obligatorio

**Files:**
- Modify: `src/CA-O.Core/Optimizations/Performance/DisableVbs.cs` (Definition: añadir `RequiresRestorePoint = true`)
- Modify: `src/CA-O.Core/Optimizations/Storage/WindowsComponentStoreResetBase.cs` (Definition: añadir `RequiresRestorePoint = true` + `ExpertOnly` si falta — verificar flags actuales en el fichero)
- Modify: `src/CA-O.Core/Optimizations/Network/ResetNetworkStackRepair.cs` (Definition: añadir `RequiresRestorePoint = true`; ya es Repair → fuera de batch por Task 2)
- Modify: `src/CA-O.Core/Optimizations/Performance/DisableDynamicTick.cs` (Definition: `Flags |= ExpertOnly` + tooltip ES que documente placebo moderno: "En Windows 10/11 moderno el tick dinámico en idle apenas afecta; placebo probable. Solo Expertos.")
- Create: `tests/CA-O.Core.Tests/DangerousGatingTests.cs`

**Interfaces:**
- Consumes: `OptimizationDefinition.RequiresRestorePoint` + enforcement existente `OptimizationEngine.cs:101-104` (leer esas ~20 líneas antes: qué hace el motor cuando el flag está activo — el test lo verifica, no lo reimplementa).
- Produces: 4 Definitions con gating honesto; tests que lo fijan.

**Contexto exacto:**
- `DisableVbs.cs:30,34`: ya `Risk Critical` + `ExpertOnly | SecurityTradeoff | RequiresReboot` → solo falta `RequiresRestorePoint = true` (rompe WSL2/Docker/Vanguard: ya documentado en tooltip, verificar).
- `WindowsComponentStoreResetBase.cs`: leer Definition completa (Detect NotApplied siempre en :33 — se queda; es destructivo irreversible y ya tiene gate `ExpertMode` en `LimpiezaPage.xaml.cs:111` + confirm en `:99` — el flag `ExpertOnly` en Definition debe reflejar ese gate si falta).
- `ResetNetworkStackRepair.cs:31`: Detect NotApplied siempre (correcto para repair: siempre aplicable manualmente, nunca recomendado).
- `DisableDynamicTick.cs:30,34,37`: `Risk Moderate` + `RequiresReboot`, Detect Unknown. Añadir `ExpertOnly` (mantener `RequiresReboot`: `Flags = ExpertOnly | RequiresReboot`).
- Enforcement motor (`OptimizationEngine.cs:101-104`): leer cómo exige el restore-point (¿bloquea Apply sin `create-restore-point-before-optimization-batch` previo? ¿lo crea solo?). El test invoca ese camino real, no duplica su lógica.

- [ ] **Step 1: Leer enforcement del motor (sin código aún)**

Leer `OptimizationEngine.cs` líneas ~95-135. Anotar en el report: qué condición dispara la exigencia, qué error devuelve si no hay restore-point, y si existe test previo que lo cubra (buscar `RequiresRestorePoint` en `tests/` — hoy cero matches, verificado en la escritura del plan).

- [ ] **Step 2: Tests de gating (fallan antes del cambio)**

```csharp
using CAO.Core.Catalog;
using Xunit;

namespace CAO.Core.Tests;

public sealed class DangerousGatingTests
{
    [Theory]
    [InlineData("disable-vbs")]
    [InlineData("windows-component-store-resetbase")]
    [InlineData("reset-network-stack-repair")]
    public void Dangerous_Requires_Restore_Point(string id)
    {
        var def = OptimizationCatalog.All
            .First(o => o.Definition.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase)).Definition;
        Assert.True(def.RequiresRestorePoint, id + " debe exigir restore-point (FASE 12).");
    }

    [Theory]
    [InlineData("disable-vbs")]
    [InlineData("windows-component-store-resetbase")]
    [InlineData("disable-dynamic-tick")]
    public void Dangerous_Is_ExpertOnly(string id)
    {
        var def = OptimizationCatalog.All
            .First(o => o.Definition.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase)).Definition;
        Assert.True(def.Flags.HasFlag(CAO.Shared.OptimizationFlags.ExpertOnly), id + " solo Expertos.");
    }

    [Fact]
    public void Dangerous_Never_Recommended_In_Batch()
    {
        var batch = CatalogProjections.BatchDefault.Select(o => o.Definition.Id).ToList();
        foreach (var id in new[] { "disable-vbs", "windows-component-store-resetbase", "disable-dynamic-tick" })
        {
            if (!batch.Contains(id, System.StringComparer.OrdinalIgnoreCase)) continue;
            var def = OptimizationCatalog.All
                .First(o => o.Definition.Id.Equals(id, System.StringComparer.OrdinalIgnoreCase)).Definition;
            var blocked = def.Flags.HasFlag(CAO.Shared.OptimizationFlags.ExpertOnly)
                || def.Flags.HasFlag(CAO.Shared.OptimizationFlags.SecurityTradeoff)
                || def.SecurityImpact == CAO.Shared.SecurityImpact.ReducedProtection;
            Assert.True(blocked, id + " en batch debe estar bloqueado por policy (ExpertOnly/SecurityTradeoff).");
        }
    }
}
```

- [ ] **Step 3: Ejecutar en rojo**

Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~DangerousGatingTests"`
Expected: FAIL en `RequiresRestorePoint` (hoy nadie lo pone) y posiblemente en `ExpertOnly` de ResetBase/DynamicTick.

- [ ] **Step 4: Aplicar los 4 cambios de Definition + verde**

Añadir `RequiresRestorePoint = true` (3 ficheros), `ExpertOnly` donde falte (ResetBase si falta; DynamicTick seguro), tooltip placebo en DynamicTick (ES+EN a la par: `TooltipEs` existe — verificar si hay `TooltipEn` en esa clase; si la clase solo tiene `TooltipEs`, añadir ambas o solo ES siguiendo el patrón del fichero, documentar).
Run: mismo filtro → PASS; luego suite Core completa verde.

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.Core/Optimizations/Performance/DisableVbs.cs src/CA-O.Core/Optimizations/Storage/WindowsComponentStoreResetBase.cs src/CA-O.Core/Optimizations/Network/ResetNetworkStackRepair.cs src/CA-O.Core/Optimizations/Performance/DisableDynamicTick.cs tests/CA-O.Core.Tests/DangerousGatingTests.cs
git commit -m "feat(cat): peligrosos con restore-point obligatorio y solo Expertos"
```

---

### Task 4: Detect reales (adiós nagging)

**Files:**
- Modify: `src/CA-O.Core/Optimizations/Power/RemoveUnusedCustomPowerPlans.cs` (Detect vía registry)
- Modify: `src/CA-O.Core/Optimizations/Network/EnableRss.cs` (override Detect vía registry)
- Modify: `src/CA-O.Core/Optimizations/Storage/EnsureTrimEnabled.cs` (Detect vía `fsutil behavior query` read-only + parser compartido con Verify)
- Modify: `src/CA-O.Core/Optimizations/Network/DisableWifiBackgroundScan.cs` (Detect best-effort, fallback Unknown)
- Create: `tests/CA-O.Core.Tests/RealDetectTests.cs`

**Interfaces:**
- Consumes: `IRegistryAccessor.GetSubKeyNames/GetValue` (`src/CA-O.Core/Interfaces/IRegistryAccessor.cs:12-39`), `PowerSchemes` (`SchemesKey` + `ReadActiveScheme`, `PowerSchemes.cs:19-20,32`), `MemoryRegistry` (tests).
- Produces: 4 Detect que distinguen aplicado/no-aplicado/desconocido; `RemoveUnusedPowerPlans` deja de naggear.

**Contexto exacto + reglas:**
- `Detect(IRegistryAccessor)` NO tiene executor por firma: solo registry, FS/BCL read-only, o proceso read-only con timeout + try/catch→Unknown. No PowerShell nunca. No `CommandPolicy` (es solo-lectura en proceso UI admin; documentar en el report).
- `RemoveUnusedCustomPowerPlans.cs:11-17` (`BuiltInSchemes`), `:41` (Detect NotApplied siempre → nagging: Risk Low + Tiny → Recommended eterno), `PowerSchemes.ReadActiveScheme(registry)` devuelve el GUID activo, `GetSubKeyNames(HKLM, SchemesKey)` enumera. Detect nuevo: `var subs = registry.GetSubKeyNames(HKLM, SchemesKey)` con try/catch→Unknown; `var stale = subs.Where(s => Guid.TryParse + !BuiltInSchemes.Contains + != active)`; `stale.Any() ? NotApplied : AppliedByCao`. `ParseSchemes` (regex `powercfg /L`) se queda para Apply — no duplicar lógica, solo el Detect cambia.
- `EnableRss.cs:9-19` (Targets `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\EnableRss=1`), hereda `RegistryOptimizationBase` (sin override Detect visible → confirma qué devuelve la base/default de `IOptimization.Detect` leyendo `IOptimization.cs` primero). Override nuevo: leer ese valor vía `registry.GetValue`; `==1` → `AppliedByCao`, otro/missing → `NotApplied`. Mantener `Compatibility Conditional` (sigue yendo a Experimental por regla 5 — correcto: sin no-op silencioso en batch, pero Detect honesto para quien lo abre).
- `EnsureTrimEnabled.cs:29` (NotApplied siempre → nagging: Low+Small → Recommended eterno), `:61-75` (Verify ya ejecuta `fsutil behavior query DisableDeleteNotify` vía executor y parsea con `StdOut.Contains('0')` — frágil: cualquier '0' del texto vale). Parser compartido `internal static bool ParseDeleteNotifyOff(string output)`: true solo si alguna línea matchea `DisableDeleteNotify\s*=\s*0` (regex, IgnoreCase, Multiline). Usarlo en Verify (reemplaza el `Contains('0')` — verificar antes que ningún test existente aserte el parse frágil; si existe, actualizarlo citándolo) y en Detect. Detect nuevo: `Process.Start(fsutil behavior query DisableDeleteNotify)` con timeout 5s, `try/catch→Unknown`, `ParseDeleteNotifyOff(stdout) ? AppliedByCao : NotApplied`. `fsutil` ruta fija `"%SystemRoot%\System32\fsutil.exe"` (no depender del PATH). Detect es sync: `process.WaitForExit(5000)` + `StandardOutput.ReadToEnd()` (cuidado deadlock: redirigir stdout+stderr y leer antes de WaitForExit, o `WaitForExit` + timeout y matar; patrón mínimo documentado en comentarios).
- `DisableWifiBackgroundScan.cs:53` (Unknown hoy — ya no naggea: Moderate → Optional; aun así el spec pide Detect vía `netsh wlan show settings`). Best-effort con tope: `FindLiveWifiInterface()` (BCL, ya existe :43-51) + `netsh wlan show settings interface="<iface>"`? — el implementador verifica contra MS Learn (Context7 primero) qué comando read-only expone el estado autoconfig por interfaz; si hay parse fiable → AppliedByCao/NotApplied, si no → Unknown con motivo (como hoy) + report que documente el intento. RULING: prohibido dejarlo en `NotApplied` hardcoded (reintroduciría nagging si algún día baja a Low); Unknown o real, nunca NotApplied ciego.
- Tests herméticos: registry-vía-`MemoryRegistry` para power-plans (sembrar subkeys — verificar que `MemoryRegistry` soporta `GetSubKeyNames`; si no, sembrar vía `SetValue` no sirve: leer `TestDoubles.cs` y, si falta, usar solo asserts de no-lanzar + parser unitario) y RSS (sembrar valor 1/0/ausente); parser `ParseDeleteNotifyOff` unitario con 3 salidas reales de fsutil (on/off/ES+EN); fsutil/netsh en vivo: SOLO test de no-lanzar (`Detect` devuelve un enum válido, sin asertar estado de la máquina).

- [ ] **Step 1: Leer `IOptimization.Detect` default + `TestDoubles.MemoryRegistry`**

Leer `src/CA-O.Core/Interfaces/IOptimization.cs` (default de Detect) y `TestDoubles.cs` (¿soporta `GetSubKeyNames`?). Anotar en el report qué soporta antes de escribir tests.

- [ ] **Step 2: Tests (rojo)**

`RealDetectTests.cs`: power-plans con perfiles sembrados (NotApplied si hay custom inactivo / AppliedByCao si solo built-ins+activo), RSS 1→Applied/ausente→NotApplied, parser fsutil (3 casos), no-lanzar en vivo (fsutil+netsh devuelven enum válido).
Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~RealDetectTests"` → FAIL (Detect actuales no cumplen).

- [ ] **Step 3: Implementar los 4 Detect + parser compartido**

Verbatim según reglas de arriba. `ParseDeleteNotifyOff` `internal static` en `EnsureTrimEnabled` (mismo assembly que tests vía `InternalsVisibleTo` pre-existente, patrón Plan 03 Task 2).

- [ ] **Step 4: Verde + commit**

Run: filtro + suite Core completa.
Expected: PASS.

```bash
git add src/CA-O.Core/Optimizations/Power/RemoveUnusedCustomPowerPlans.cs src/CA-O.Core/Optimizations/Network/EnableRss.cs src/CA-O.Core/Optimizations/Storage/EnsureTrimEnabled.cs src/CA-O.Core/Optimizations/Network/DisableWifiBackgroundScan.cs tests/CA-O.Core.Tests/RealDetectTests.cs
git commit -m "feat(cat): Detect reales para trim, RSS, planes y wifi (fin del nagging)"
```

---

### Task 5: Acotar placebos (Nagle por interfaz + tradeoffs documentados)

**Files:**
- Modify: `src/CA-O.Core/Optimizations/Network/DisableNagleTcpAcks.cs` (filtro por interfaz física)
- Modify: `src/CA-O.Core/Optimizations/Gaming/MmcssSystemResponsiveness.cs` (tooltip tradeoff audio)
- Modify: `src/CA-O.Core/Optimizations/Gaming/MouseDriverQueueTrim.cs` (tooltip tradeoff eventos)
- Modify/crear tests: `tests/CA-O.Core.Tests/` (`NagleScopeTests.cs` nuevo; si existen tests de estas 3 clases, extenderlos — buscar `DisableNagle|Mmcss|MouseDriverQueue` en tests primero)

**Interfaces:**
- Consumes: `FindWifiInterface`-style BCL (`NetworkInterface.GetAllNetworkInterfaces()`, precedente `DisableWifiBackgroundScan.cs:43-51`).
- Produces: Nagle aplicado solo a físicas; tooltips honestos; tests que fijan el alcance.

**Contexto exacto:**
- `DisableNagleTcpAcks.cs`: leer entero primero (13 líneas de clase aprox + Definition). Hoy escribe `TcpAckFrequency`+`TCPNoDelay=1` en TODAS las interfaces incl. VPN/virtuales/loops (audit). Filtro nuevo `internal static bool IsPhysicalCandidate(NetworkInterface nic)`: `OperationalStatus.Up`, `NetworkInterfaceType` en `{Ethernet, Wireless80211, GigabitEthernet?}` — verificar miembros reales del enum en compilación (FastEthernet etc.), excluir `Loopback`/`Tunnel`, y excluir por descripción/nombre los patrones `loop|virtual|vpn|pseudo|tap|tun|hyper-v|vmware|virtualbox|wsl` (OrdinalIgnoreCase). Captura/aplicación itera solo candidatas; si cero candidatas → `Fail("Sin adaptadores físicos.")` en vez de no-op silencioso. Snapshot por interfaz preservado (verificar cómo captura hoy: si usa snapshot por valor, mantenerlo; no cambiar formato de snapshot).
- `MmcssSystemResponsiveness.cs:11` (base registry `20→10`): añadir al `TooltipEs` (y `TooltipEn` a la par): tradeoff "puede recortar audio en segundo plano durante juego". Solo texto, sin cambiar valores (cambiar 10→otro valor sería tuning sin evidencia — prohibido aquí).
- `MouseDriverQueueTrim.cs:12` (`MouseDataQueueSize 100→32`): tooltip tradeoff "en hardware antiguo puede perder eventos de ratón". Solo texto.
- Tests: `IsPhysicalCandidate` unitario con tuplas sintéticas (física Up→true; loop→false; tunnel→false; nombre "TAP-Windows"→false; Down→false); Nagle sin candidatas→Fail (con `OptimizationContext` sin executor o con stub — copiar el patrón de construcción de contexto de los tests existentes de Network); tooltips contienen "audio"/"eventos o hardware" (`String.Contains` OrdinalIgnoreCase sobre `Definition.TooltipEs`).

- [ ] **Step 1: Leer las 3 clases + tests existentes**

Leer `DisableNagleTcpAcks.cs` completo, ambas Definitions gaming, y grep de tests que las cubran. Anotar en el report: cómo itera interfaces hoy, formato de snapshot, y qué tests existen.

- [ ] **Step 2: Tests en rojo** (`NagleScopeTests.cs` según Step 1; si existen tests previos, primero ejecutarlos en verde para tener base).
- [ ] **Step 3: Filtro + 2 tooltips, verde.**
- [ ] **Step 4: Suite Core verde + commit**

```bash
git add src/CA-O.Core/Optimizations/Network/DisableNagleTcpAcks.cs src/CA-O.Core/Optimizations/Gaming/MmcssSystemResponsiveness.cs src/CA-O.Core/Optimizations/Gaming/MouseDriverQueueTrim.cs tests/CA-O.Core.Tests/NagleScopeTests.cs
git commit -m "feat(cat): Nagle solo en fisicas y tradeoffs documentados"
```

---

### Task 6: Inventario regenerado + verificación global

**Files:**
- Modify: `OPTIMIZATION_INVENTORY.md` (conteos + sección de proyecciones)
- Modify: `docs/OPTIMIZATION-CATALOG.md` (marcar Repairs/Diagnostics/Restores por sección; sin reescribir el doc)
- Modify: `README.md` (conteos si el plan cambió totales — NO cambian: All sigue 92; solo añadir 1 línea sobre proyecciones donde ya se habla del catálogo)

**Interfaces:**
- Consumes: `CatalogProjections` (conteos reales), Tasks 1-5.
- Produces: docs coherentes; suite global verde (~1055-1065 tests: 1043 + ~12-20 nuevos).

**Contexto exacto:**
- `OPTIMIZATION_INVENTORY.md`: cabecera y tabla por categoría (Plan 03: 92 PROD). Añadir bloque "Proyecciones (§5.3)": `BatchDefault N` / `RepairActions N` / `Diagnostics 4` / `Restores N` con N contados del código (no a ojo: ejecutar un conteo o leer los sets; RepairIds ~14, RestoreIds 5-6 según Task 1). Total PROD sigue 92.
- `docs/OPTIMIZATION-CATALOG.md`: anotar junto a cada ID afectado su proyección (`[Repair]`, `[Diagnostic]`, `[Restore]`, `[Gated: ExpertOnly+RestorePoint]`) — edición mínima por línea, sin reestructurar.
- NO tocar: `OPTIMIZATION_REFACTORING_STATUS.md` ni `audit-*.ps1` (ya marcados históricos en Plan 03), conteos de tests del README (stales preexistentes, diferidos en Plan 03).

- [ ] **Step 1: Contar proyecciones reales** (del código Task 1, no del plan) y editar los 3 docs.
- [ ] **Step 2: `dotnet build CA-O.sln -c Release`** → 0 err/0 warn.
- [ ] **Step 3: `dotnet test CA-O.sln -c Release`** → todo verde (anotar total en el report).
- [ ] **Step 4: Commit**

```bash
git add OPTIMIZATION_INVENTORY.md docs/OPTIMIZATION-CATALOG.md README.md
git commit -m "docs(cat): inventario con proyecciones del catalogo honesto"
```

---

## Self-Review (hecho por el autor del plan)

1. **Spec coverage:** §5.3a proyecciones → T1+T2; §5.3b Detect reales (trim/rss/wifi/powerplans) → T4; §5.3c peligrosos + restore-point + ExpertOnly/placebos + Nagle → T3+T5; §5.3d NoDuplicateTargets → ya existe (Plan 03, se reutiliza sin tocar); §5.3e docs → T6 + Plan 03 (banners históricos ya hechos). Sin gaps.
2. **Placeholder scan:** sin TBD/TODO/"similar a"; cada step trae código literal o instrucción de lectura con líneas exactas. Los puntos que dependen de lectura (Step 1 de T3/T4/T5) devuelven anotación en report, no código ciego.
3. **Type consistency:** `CatalogProjections.*` se consume igual en T1-tests, T2 y T6; `MemoryRegistry`/`SystemContext` con nota de verificación previa; overloads xUnit con fallback documentado. `BatchDefault` es `IReadOnlyList<IOptimization>` igual que `All` → los call sites cambian una línea.
