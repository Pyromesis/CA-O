# Plan 03 — Limpieza ampliada + dedup honesto Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ampliar el catálogo de limpieza con 5 rutas nuevas seguras, fusionar el duplicado de Windows Update con alias de compatibilidad, limpiar el %TEMP% de todos los usuarios (no solo SYSTEM) y exponerlo en una 7ª tarjeta de Limpieza, con test contrato anti-duplicados.

**Architecture:** Todo en `CA-O.Core` (subclases de `TempFileCleanupOptimization`, sin tocar el pipe ni el protocolo) más UI declarativa en `LimpiezaPage`. El motor resuelve IDs retirados vía `CanonicalIdFor` para no romper historiales ni llamadas viejas.

**Tech Stack:** C# .NET 10, WinUI 3 / WindowsAppSDK 2.4, xUnit 2.9.2. Cero dependencias nuevas.

**Spec:** `docs/superpowers/specs/2026-09-17-cao-premium-cat-benchmark-cleanup-design.md` (sección 5.1, 5.2; criterio §8: dedup WU, fix %TEMP% usuario, ≥5 rutas nuevas con confirmación donde toca, 7ª card con MB, `NoDuplicateTargets` verde).

## Global Constraints

- Solo C# + BCL + `System.Management` (ya referenciado): cero `.csproj`, cero NuGet nuevo.
- `dotnet build CA-O.sln -c Release` (0 errores, 0 warnings) + `dotnet test CA-O.sln -c Release` en verde ANTES de cada commit.
- Commits en español, un commit por tarea (`feat(clean): ...` / `fix(clean): ...`). Sin tildes si la shell las corrompe (se vio en Plan 02 con PowerShell 5.1).
- Textos visibles de Limpieza en español hardcodeado en XAML (patrón existente de la página; esa página no usa `Localizer`). `AutomationProperties.Name` + `ToolTipService.ToolTip` en cada botón nuevo.
- NO tocar: `PrivilegedPipeService.cs`, `IpcProtocol.cs`, `CommandPolicy.cs`. NO tocar `QuickCleanIds` salvo el reemplazo WU de Task 1. NO tocar las 6 cards existentes salvo el renglón WU de Task 1.
- Solo ficheros (nunca carpetas), `TopDirectoryOnly` donde aplique, skip silencioso en uso, `NotReversible` en toda limpieza. PROHIBIDO: `DataStore.edb`, `Local Storage`/`IndexedDB` (sesiones), `Spotify/Storage` (offline), VPN/virtuales/loops.
- Confirmación (`ConfirmIfNeededAsync`) para lo destructivo: MEMORY.DMP / LiveKernelReports sí; prefetch/CBS/Outlook/navegadores no (cachés regenerables).

---

### Task 1: Dedup Windows Update (retirar `disk-cleanup-system-files` con alias)

**Files:**
- Modify: `src/CA-O.Core/Optimization/OptimizationCatalog.cs`
- Modify: `src/CA-O.Core/Optimization/OptimizationEngine.cs` (2 ediciones: línea ~102 y `Resolve()` ~843-849)
- Modify: `src/CA-O.UI/LimpiezaPage.xaml.cs` (`QuickCleanIds`)
- Modify: `src/CA-O.UI/LimpiezaPage.xaml` (renglón "Archivos del sistema" → canónico)
- Modify: `tests/CA-O.Core.Tests/OptimizationCatalogContractTests.cs` (conteo 88→87 + fila retired)
- Test: resto de tests que referencien el conteo o el ID (vía grep del Step 1)

**Interfaces:**
- Consumes: `CleanupWindowsUpdateCache` (canónica viva, Risk Low, existe en `All`).
- Produces: `OptimizationCatalog.CanonicalIdFor(string) -> string`; `OptimizationCatalog.RetiredAliases`; `All` con 87 entradas; `AllLegacy` con 67.

- [ ] **Step 1: grep de impacto (solo lectura)**

Run:
```
grep -rn "disk-cleanup-system-files" src tests --include="*.cs" --include="*.xaml"
grep -rn "Assert.Equal(88" tests --include="*.cs"
grep -rn "AllLegacy" tests --include="*.cs"
grep -rn "LegacyCatalogKeeps66\|Equal(66" tests --include="*.cs"
```
Expected: lista de todos los ficheros que nombran el ID retirado o los conteos 88/66. Cada uno se actualiza en los steps siguientes. Si aparece en `RecommendationEngine.cs` (usa `LegacyIds` como `StubIds`): no tocar, el retirado pasa a excluirse de recomendaciones automáticamente (deseado).

- [ ] **Step 2: test en rojo — el retirado debe estar fuera de producción pero trazable**

En `tests/CA-O.Core.Tests/OptimizationCatalogContractTests.cs`, añadir fila al theory existente (verbatim, tras la fila `set-best-performance-ac`):

```csharp
[InlineData("disk-cleanup-system-files", "cleanup-windows-update-cache")]
```

Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~RetiredDuplicates"`
Expected: FAIL (el ID sigue en `All`).

- [ ] **Step 3: catálogo — retirar + alias + legacy**

En `src/CA-O.Core/Optimization/OptimizationCatalog.cs`, aplicar estos 5 cambios exactos:

a) `LegacyIds`: añadir `"disk-cleanup-system-files",` con comentario:
```csharp
// cleanup-windows-update-cache == disk-cleanup-system-files (SoftwareDistribution\Download).
"disk-cleanup-system-files",
```

b) Añadir tras `IsProductionId` (línea 28):
```csharp
/// <summary>IDs retirados que siguen resolviendo a su canónica (historial y llamadas viejas).</summary>
public static readonly IReadOnlyDictionary<string, string> RetiredAliases =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["disk-cleanup-system-files"] = "cleanup-windows-update-cache",
    };

public static string CanonicalIdFor(string id) =>
    RetiredAliases.TryGetValue(id, out var canonical) ? canonical : id;
```

c) En `All`: borrar la línea `DiskCleanupSystemFiles,` (la del bloque Storage Phase 3). El campo estático `public static readonly DiskCleanupSystemFiles DiskCleanupSystemFiles = new();` SE CONSERVA (lo usa `AllLegacy`).

d) En `AllLegacy`: añadir `DiskCleanupSystemFiles,` (p. ej. tras `WindowsComponentStoreResetBase,`) y actualizar el comentario `/// Catálogo completo histórico (66)` → `(67)`.

- [ ] **Step 4: motor — normalizar IDs retirados en los 2 puntos de resolución**

En `src/CA-O.Core/Optimization/OptimizationEngine.cs`:

a) Línea ~102 (verbatim, reemplazar 1 línea por 2):
```csharp
var canonicalId = OptimizationCatalog.CanonicalIdFor(optimizationId);
var definition = OptimizationCatalog.All.FirstOrDefault(o => o.Definition.Id.Equals(canonicalId, StringComparison.OrdinalIgnoreCase));
```

b) `Resolve()` (~843-849, verbatim):
```csharp
private IOptimization Resolve(string optimizationId, IOptimization? instance = null)
{
    if (instance is not null) return instance;
    var canonicalId = OptimizationCatalog.CanonicalIdFor(optimizationId);
    var found = OptimizationCatalog.All.FirstOrDefault(o =>
        o.Definition.Id.Equals(canonicalId, StringComparison.OrdinalIgnoreCase));
    return found ?? throw new InvalidOperationException($"Unknown optimization '{optimizationId}'");
}
```

c) Verificar: `grep -n "OptimizationCatalog.All.FirstOrDefault" src/CA-O.Core/Optimization/OptimizationEngine.cs` — si hay MÁS puntos de resolución directa por ID además de los 2 editados, aplicarles el mismo `CanonicalIdFor`. (Se conocen 2: línea 102 y 846. Si `Detect/Verify/Revert/Capture` pasan por `Resolve()`, ya están cubiertos.)

- [ ] **Step 5: UI — apuntar a la canónica**

a) `src/CA-O.UI/LimpiezaPage.xaml.cs`, en `QuickCleanIds`: reemplazar `"disk-cleanup-system-files",` por `"cleanup-windows-update-cache",`.

b) `src/CA-O.UI/LimpiezaPage.xaml`, renglón "Archivos del sistema" (líneas ~83-90): cambiar
`Tag="disk-cleanup-system-files"` → `Tag="cleanup-windows-update-cache"`,
título `Text="Archivos del sistema"` → `Text="Caché Windows Update"`,
`AutomationProperties.Name="Limpiar archivos del sistema"` → `AutomationProperties.Name="Limpiar caché Windows Update"`.
Subtítulo y tooltip se conservan (ya describen `SoftwareDistribution\Download`).

- [ ] **Step 6: actualizar conteos en tests**

a) `OptimizationCatalogContractTests.cs` línea 46: `Assert.Equal(88, ids.Count);` → `Assert.Equal(87, ids.Count);` y actualizar el comentario a `// 88 previas - 1 duplicado WU retirado (disk-cleanup-system-files → alias)`.

b) `LegacyCatalogKeeps66ForTraceability` → renombrar a `LegacyCatalogKeeps67ForTraceability`, `Assert.Equal(66, ...)` → `Assert.Equal(67, ...)`.

c) Cada otro fichero del grep del Step 1 que aserte 88/66 o nombre el ID: actualizar igual (conteo o referencia). Si un test DATA-DRIVEN itera `All` sin conteo fijo, no tocar.

- [ ] **Step 7: test en verde**

Run: `dotnet build CA-O.sln -c Release` (0 err/0 warn) + `dotnet test tests/CA-O.Core.Tests -c Release` (todo verde).
Expected: PASS.

- [ ] **Step 8: commit**

```bash
git add src/CA-O.Core/Optimization/OptimizationCatalog.cs src/CA-O.Core/Optimization/OptimizationEngine.cs src/CA-O.UI/LimpiezaPage.xaml.cs src/CA-O.UI/LimpiezaPage.xaml tests/CA-O.Core.Tests/OptimizationCatalogContractTests.cs [otros tests tocados del Step 6c]
git commit -m "feat(clean): retira duplicado WU con alias de compatibilidad"
```

---

### Task 2: Fix %TEMP% de todos los usuarios + helper de perfiles

**Files:**
- Modify: `src/CA-O.Core/Optimizations/Storage/TempFileCleanupOptimization.cs` (helpers + `FormatSize` virtual)
- Modify: `src/CA-O.Core/Optimizations/Storage/CleanupWindowsTemp.cs` (Targets computados + textos)
- Test: `tests/CA-O.Core.Tests/UserTempDirsTests.cs` (nuevo)

**Interfaces:**
- Consumes: nada nuevo.
- Produces: `TempFileCleanupOptimization.ProfileSubDirs(params string[]) -> IReadOnlyList<string>` (internal), `InteractiveUserTempDirs()` (internal), `FormatSize(long)` virtual (protected).

- [ ] **Step 1: helpers de perfiles + FormatSize en la base (verbatim)**

En `src/CA-O.Core/Optimizations/Storage/TempFileCleanupOptimization.cs`, añadir tras `ExpandDir` (líneas 27-30):

```csharp
/// <summary>Subdirectorios existentes bajo cada perfil de C:\Users. El servicio
/// corre como SYSTEM y %TEMP% solo le da el suyo: así se alcanza el Temp real
/// de cada usuario. Solo rutas absolutas existentes bajo \Users (nunca se
/// elevan rutas de usuario sin validar).</summary>
internal static IReadOnlyList<string> ProfileSubDirs(params string[] relativeParts)
{
    var found = new List<string>();
    try
    {
        var usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
        foreach (var profile in Directory.GetDirectories(usersRoot))
        {
            try
            {
                var dir = profile;
                foreach (var part in relativeParts) dir = Path.Combine(dir, part);
                if (Directory.Exists(dir) && dir.Length > 3) found.Add(dir);
            }
            catch { }
        }
    }
    catch { }
    return found;
}

/// <summary>Temp de cada usuario interactivo (AppData\Local\Temp existente).</summary>
internal static IReadOnlyList<string> InteractiveUserTempDirs() =>
    ProfileSubDirs("AppData", "Local", "Temp");

/// <summary>Formato de tamaño para mensajes. Base = KB (mensajes históricos intactos).</summary>
protected virtual string FormatSize(long bytes) => $"{bytes / 1024} KB";
```

Y en `ApplyAsync` línea 91, reemplazar el mensaje por (verbatim):
```csharp
return Task.FromResult(deleted > 0
    ? OperationResult.Ok($"Limpieza completada: {deleted} fichero(s), {FormatSize(bytes)} liberados.")
    : OperationResult.Ok("No quedaban ficheros antiguos para limpiar."));
```
(El texto resultante es byte-idéntico al anterior para la base: `"1024 KB liberados."`.)

- [ ] **Step 2: `CleanupWindowsTemp` con Targets computados (verbatim completo del fichero)**

Reemplazar `src/CA-O.Core/Optimizations/Storage/CleanupWindowsTemp.cs` por:

```csharp
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes temp files older than 1 day: Windows Temp + service TEMP +
/// every interactive user's Temp (the service runs as SYSTEM, so %TEMP% alone
/// only covers its own profile). Files in use are skipped.</summary>
public sealed class CleanupWindowsTemp : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        var list = new List<(string Directory, string Pattern, int OlderThanDays)>
        [
            (@"%SystemRoot%\Temp", "*.*", 1),
            (@"%TEMP%", "*.*", 1),
        ];
        // PendingFiles() ya deduplica (seenDirs), omite inexistentes y solo
        // borra ficheros TopDirectoryOnly: añadir por perfil es seguro.
        foreach (var dir in InteractiveUserTempDirs()) list.Add((dir, "*.*", 1));
        return list;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-temp",
        NameEs = "Limpiar archivos temporales de Windows",
        NameEn = "Clean Windows temporary files",
        DescriptionEs = "Borra temporales de Windows y de todos los usuarios con más de un día. Omite los que estén en uso.",
        DescriptionEn = "Deletes Windows and every user's temp files older than one day. Skips files in use.",
        TooltipEs = "Borra Windows\\Temp, TEMP del servicio y Temp de cada usuario. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
```

- [ ] **Step 3: tests (verbatim, fichero nuevo `tests/CA-O.Core.Tests/UserTempDirsTests.cs`)**

```csharp
using CAO.Core.Optimizations.Storage;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>El fix %TEMP%-usuario: solo dirs existentes bajo \Users + humo de la base.</summary>
public sealed class UserTempDirsTests
{
    [Fact]
    public void ProfileSubDirs_ReturnsOnlyExistingDirsUnderUsers()
    {
        var dirs = TempFileCleanupOptimization.ProfileSubDirs("AppData", "Local", "Temp");
        foreach (var dir in dirs)
        {
            Assert.True(Path.IsPathRooted(dir));
            Assert.True(Directory.Exists(dir));
            Assert.Contains("Users", dir, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("AppData", "Local", "Temp"), dir, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CleanupWindowsTemp_TargetsKeepSystemAndServiceEntries()
    {
        var optimization = new CleanupWindowsTemp();
        // PreviewAsync ejercita Targets+ExpandDir sin borrar nada.
        var preview = optimization.PreviewAsync(new TestDoubles.StubRegistry(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.Contains(preview.Lines, l => l.Target.Contains("Temp", StringComparison.OrdinalIgnoreCase));
    }
}
```

NOTA: `TestDoubles.StubRegistry` — verificar que existe en `tests/CA-O.Core.Tests/TestDoubles.cs` con ese nombre exacto. Si se llama distinto (p. ej. `FakeRegistry`/`StubRegistryAccessor`), usar el nombre real (grep en el Step de implementación, cambio mecánico de 1 palabra). La firma usada es `PreviewAsync(IRegistryAccessor, CancellationToken)` — Brief-verified en `TempFileCleanupOptimization.cs:129`.

- [ ] **Step 4: test en rojo → verde**

Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~UserTempDirs"`
Expected: primero FAIL si el nombre del stub es distinto (ajustar), luego PASS. Si `PreviewAsync` lanza en esta máquina, reportar como concern (no silenciar con try/catch en el test).

- [ ] **Step 5: build + suite Core en verde**

Run: `dotnet build CA-O.sln -c Release` + `dotnet test tests/CA-O.Core.Tests -c Release`. Expected: PASS (los mensajes históricos no cambian: `FormatSize` base devuelve el mismo `"N KB"`).

- [ ] **Step 6: commit**

```bash
git add src/CA-O.Core/Optimizations/Storage/TempFileCleanupOptimization.cs src/CA-O.Core/Optimizations/Storage/CleanupWindowsTemp.cs tests/CA-O.Core.Tests/UserTempDirsTests.cs
git commit -m "feat(clean): temporales de todos los usuarios, no solo SYSTEM"
```

---

### Task 3: 5 limpiezas nuevas seguras

**Files:**
- Create: `src/CA-O.Core/Optimizations/Storage/CleanupPrefetchStale.cs`
- Create: `src/CA-O.Core/Optimizations/Storage/CleanupCbsLogs.cs`
- Create: `src/CA-O.Core/Optimizations/Storage/CleanupCrashDumpsExtended.cs`
- Create: `src/CA-O.Core/Optimizations/Storage/CleanupOutlookCache.cs`
- Create: `src/CA-O.Core/Optimizations/Storage/CleanupBrowserCodeCache.cs`
- Modify: `src/CA-O.Core/Optimization/OptimizationCatalog.cs` (5 campos + 5 entradas en `All`)
- Modify: `tests/CA-O.Core.Tests/OptimizationCatalogContractTests.cs` (`Assert.Equal(87` → `Assert.Equal(92` + comentario)
- Test: `tests/CA-O.Core.Tests/NewCleanupsSafetyTests.cs` (nuevo)

**Interfaces:**
- Consumes: `TempFileCleanupOptimization` (Targets, `ProfileSubDirs` de Task 2), `OptimizationCategory.Storage`, `OptimizationFlags.NotReversible`.
- Produces: 5 IDs nuevos (`cleanup-prefetch-stale`, `cleanup-cbs-logs`, `cleanup-crash-dumps-extended`, `cleanup-outlook-cache`, `cleanup-browser-code-cache`); `All` con 92 entradas.

- [ ] **Step 1: grep de mensajes exactos (1 minuto, decide override de formato)**

Run: `grep -rn "Limpieza completada" tests src --include="*.cs"`
Expected: si algún test aserta el texto exacto del mensaje de `ApplyAsync` de la base, NO tocar el formato base y las clases nuevas usan KB como la base (quitar el `FormatSize` override del Step 3). Si nadie lo aserta, proceder con override MB.

- [ ] **Step 2: crear las 5 clases (verbatim)**

Común a las 5: `using CAO.Shared;`, namespace `CAO.Core.Optimizations.Storage`, `sealed class ... : TempFileCleanupOptimization`, `Flags = OptimizationFlags.NotReversible`, `SecurityImpact = SecurityImpact.None`, `AntiCheatImpact = AntiCheatImpact.None`, `Compatibility = CompatibilityStatus.Compatible`. `protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";` en las 5 (7ª card informa MB; solo si el Step 1 lo permite).

a) `CleanupPrefetchStale.cs`:
```csharp
/// <summary>Deletes stale prefetch traces (*.pf older than 30 days). Space
/// maintenance only: Windows rebuilds prefetch, so this never speeds anything up.</summary>
public sealed class CleanupPrefetchStale : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\Prefetch", "*.pf", 30),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-prefetch-stale",
        NameEs = "Limpiar prefetch antiguo",
        NameEn = "Clean stale prefetch",
        DescriptionEs = "Borra rastros prefetch (*.pf) de más de 30 días. Solo libera espacio: no acelera nada.",
        DescriptionEn = "Deletes prefetch traces (*.pf) older than 30 days. Frees space only: never speeds anything up.",
        TooltipEs = "Solo ficheros *.pf con más de 30 días. Windows lo reconstruye solo. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
```

b) `CleanupCbsLogs.cs`: igual con `Targets = [(@"%SystemRoot%\Logs\CBS", "*.log", 30)]`, Id `cleanup-cbs-logs`, NameEs `Limpiar registros CBS`, NameEn `Clean CBS logs`, DescriptionEs `Borra registros CBS (*.log) de más de 30 días. Solo libera espacio.`, DescriptionEn `Deletes CBS logs (*.log) older than 30 days. Frees space only.`, TooltipEs `Solo *.log con más de 30 días en Windows\\Logs\\CBS. Mantenimiento no reversible.`, ExpectedImpact None, Empirical/Medium, Low.

c) `CleanupCrashDumpsExtended.cs` (Targets computados por los perfiles):
```csharp
/// <summary>Extended crash-dump maintenance: LiveKernelReports (*.dmp &gt;30d),
/// MEMORY.DMP (&gt;30d) and per-user CrashDumps. Destructive for debugging:
/// the UI asks for confirmation (ConfirmIfNeededAsync).</summary>
public sealed class CleanupCrashDumpsExtended : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        var list = new List<(string Directory, string Pattern, int OlderThanDays)>
        [
            (@"%SystemRoot%\LiveKernelReports", "*.dmp", 30),
            (@"%SystemRoot%", "MEMORY.DMP", 30),
        ];
        foreach (var dir in ProfileSubDirs("AppData", "Local", "CrashDumps")) list.Add((dir, "*.dmp", 30));
        return list;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-crash-dumps-extended",
        NameEs = "Limpiar volcados extendidos",
        NameEn = "Clean extended crash dumps",
        DescriptionEs = "Borra LiveKernelReports, MEMORY.DMP y CrashDumps de usuario de más de 30 días. Pide confirmación.",
        DescriptionEn = "Deletes 30+ day LiveKernelReports, MEMORY.DMP and per-user CrashDumps. Asks for confirmation.",
        TooltipEs = "Volcados del sistema y de apps de +30 días. Dificulta depurar fallos viejos: pide confirmación. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.NotReversible,
    };
}
```

d) `CleanupOutlookCache.cs` (Targets computados):
```csharp
/// <summary>Deletes Outlook secure-temp attachments (INetCache\Content.Outlook)
/// older than 7 days for every profile. Locked files are skipped.</summary>
public sealed class CleanupOutlookCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        var list = new List<(string Directory, string Pattern, int OlderThanDays)>();
        foreach (var dir in ProfileSubDirs("AppData", "Local", "Microsoft", "Windows", "INetCache", "Content.Outlook"))
            list.Add((dir, "*.*", 7));
        return list;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-outlook-cache",
        NameEs = "Limpiar caché de Outlook",
        NameEn = "Clean Outlook cache",
        DescriptionEs = "Borra adjuntos temporales de Outlook (Content.Outlook) de más de 7 días, de todos los usuarios.",
        DescriptionEn = "Deletes Outlook temp attachments (Content.Outlook) older than 7 days, for every user.",
        TooltipEs = "Adjuntos que Outlook guarda al abrirlos. Se regeneran solos. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
```

e) `CleanupBrowserCodeCache.cs` (Targets computados; NUNCA Local Storage/IndexedDB):
```csharp
/// <summary>Deletes regenerable browser code caches (Chrome/Edge Default +
/// classic Teams): Cache/Code Cache/GPUCache/ShaderCache only. NEVER Local
/// Storage, IndexedDB, Login Data or history: sessions live there. Files in
/// use (open browser) are skipped: close browsers first.</summary>
public sealed class CleanupBrowserCodeCache : TempFileCleanupOptimization
{
    private static readonly string[] SafeSubdirs = ["Cache", "Code Cache", "GPUCache", "ShaderCache"];
    private static readonly string[] TeamsSubdirs = ["Cache", "Code Cache", "GPUCache"];

    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        var list = new List<(string Directory, string Pattern, int OlderThanDays)>();
        foreach (var sub in SafeSubdirs)
        {
            foreach (var dir in ProfileSubDirs("AppData", "Local", "Google", "Chrome", "User Data", "Default", sub))
                list.Add((dir, "*.*", 1));
            foreach (var dir in ProfileSubDirs("AppData", "Local", "Microsoft", "Edge", "User Data", "Default", sub))
                list.Add((dir, "*.*", 1));
        }
        foreach (var sub in TeamsSubdirs)
        {
            foreach (var dir in ProfileSubDirs("AppData", "Roaming", "Microsoft", "Teams", sub))
                list.Add((dir, "*.*", 1));
        }
        return list;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-browser-code-cache",
        NameEs = "Limpiar cachés de navegadores",
        NameEn = "Clean browser caches",
        DescriptionEs = "Vacía cachés regenerables de Chrome, Edge y Teams. Nunca toca sesiones, contraseñas ni historial. Cierra cada app antes.",
        DescriptionEn = "Empties regenerable Chrome, Edge and Teams caches. Never touches sessions, passwords or history. Close each app first.",
        TooltipEs = "Solo Cache/Code Cache/GPUCache/ShaderCache del perfil Default. Lo que esté en uso se omite. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
```

- [ ] **Step 3: registrar en catálogo (verbatim)**

En `OptimizationCatalog.cs`, 5 campos tras `public static readonly CleanupAppCaches CleanupAppCaches = new();` (línea 96):
```csharp
public static readonly CleanupPrefetchStale CleanupPrefetchStale = new();
public static readonly CleanupCbsLogs CleanupCbsLogs = new();
public static readonly CleanupCrashDumpsExtended CleanupCrashDumpsExtended = new();
public static readonly CleanupOutlookCache CleanupOutlookCache = new();
public static readonly CleanupBrowserCodeCache CleanupBrowserCodeCache = new();
```
Y en `All`, tras `CleanupAppCaches,` (línea 200):
```csharp
CleanupPrefetchStale,
CleanupCbsLogs,
CleanupCrashDumpsExtended,
CleanupOutlookCache,
CleanupBrowserCodeCache,
```

- [ ] **Step 4: conteo 87→92**

`Assert.Equal(87, ids.Count);` → `Assert.Equal(92, ids.Count);`, comentario → `// 87 + 5 limpiezas nuevas (prefetch, CBS, volcados ext, Outlook, navegadores)`.

- [ ] **Step 5: tests de seguridad (verbatim, `tests/CA-O.Core.Tests/NewCleanupsSafetyTests.cs`)**

```csharp
using CAO.Core.Optimizations.Storage;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Las 5 limpiezas nuevas: solo rutas seguras, nunca sesiones ni datos.</summary>
public sealed class NewCleanupsSafetyTests
{
    [Fact]
    public void BrowserCache_NeverTouchesSessionsOrPasswords()
    {
        var banned = new[] { "Local Storage", "IndexedDB", "Session Storage", "Login Data", "History", "Bookmarks", "Storage" };
        var preview = new CleanupBrowserCodeCache()
            .PreviewAsync(new TestDoubles.StubRegistry(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.NotEmpty(preview.Lines);
        foreach (var line in preview.Lines)
        {
            var leaf = line.Target.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Last();
            Assert.DoesNotContain(leaf, banned, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CrashDumpsExtended_OnlyDumpPatterns()
    {
        var preview = new CleanupCrashDumpsExtended()
            .PreviewAsync(new TestDoubles.StubRegistry(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.NotEmpty(preview.Lines);
        // Contains (no EndsWith): PreviewLine.Target lleva sufijo " (>Nd)".
        foreach (var line in preview.Lines)
            Assert.True(line.Target.Contains("*.dmp", StringComparison.OrdinalIgnoreCase)
                || line.Target.Contains("MEMORY.DMP", StringComparison.OrdinalIgnoreCase),
                $"patrón inesperado: {line.Target}");
    }

    [Fact]
    public void PrefetchAndCbs_OnlyTheirPatterns()
    {
        var prefetch = new CleanupPrefetchStale()
            .PreviewAsync(new TestDoubles.StubRegistry(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.All(prefetch.Lines, l => Assert.Contains("*.pf", l.Target, StringComparison.Ordinal));
        var cbs = new CleanupCbsLogs()
            .PreviewAsync(new TestDoubles.StubRegistry(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.All(cbs.Lines, l => Assert.Contains("*.log", l.Target, StringComparison.Ordinal));
    }

    [Fact]
    public void OutlookCache_OnlyContentOutlook()
    {
        var preview = new CleanupOutlookCache()
            .PreviewAsync(new TestDoubles.StubRegistry(), CancellationToken.None).GetAwaiter().GetResult();
        foreach (var line in preview.Lines)
            Assert.Contains("Content.Outlook", line.Target, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewCleanups_AreStorageNotReversible()
    {
        IOptimization[] opts =
        [
            new CleanupPrefetchStale(), new CleanupCbsLogs(), new CleanupCrashDumpsExtended(),
            new CleanupOutlookCache(), new CleanupBrowserCodeCache(),
        ];
        var ids = new[] { "cleanup-prefetch-stale", "cleanup-cbs-logs", "cleanup-crash-dumps-extended", "cleanup-outlook-cache", "cleanup-browser-code-cache" };
        Assert.Equal(ids, opts.Select(o => o.Definition.Id));
        foreach (var o in opts)
        {
            Assert.Equal(CAO.Shared.OptimizationCategory.Storage, o.Definition.Category);
            Assert.True(o.Definition.Flags.HasFlag(CAO.Shared.OptimizationFlags.NotReversible));
        }
    }
}
```

NOTA StubRegistry: mismo ajuste mecánico que Task 2 Step 3 (usar el nombre real de `TestDoubles.cs`). Los asserts usan `Contains` (no `EndsWith`) porque `PreviewLine.Target` lleva sufijo `" (>Nd)"` (base línea 133).

Hmm — mejor lo dejo ya corregido: reescribo ese test con Contains. Lo corrijo inline ahora (self-review): cambiar el bloque CrashDumpsExtended a Contains. Lo haré en la revisión antes de commitear el plan... no, el plan ya está escrito abajo. Lo corrijo con edit tras escribir. Anoto: SDD preflight debe cazarlo igual; pero lo corrijo yo ya.

- [ ] **Step 6: build + tests Core en verde**

Run: `dotnet build CA-O.sln -c Release` + `dotnet test tests/CA-O.Core.Tests -c Release`. Expected: PASS (incluye los tests contrato que iteran `All`: metadata completa exigida — las 5 la traen).

- [ ] **Step 7: commit**

```bash
git add src/CA-O.Core/Optimizations/Storage/Cleanup*.cs src/CA-O.Core/Optimization/OptimizationCatalog.cs tests/CA-O.Core.Tests/OptimizationCatalogContractTests.cs tests/CA-O.Core.Tests/NewCleanupsSafetyTests.cs
git commit -m "feat(clean): 5 limpiezas nuevas seguras (prefetch, CBS, volcados, Outlook, navegadores)"
```
(Cuidado con el glob `Cleanup*.cs`: solo debe incluir los 5 nuevos. Si `git add` coge `CleanupWindowsTemp.cs`/`CleanupAppCaches.cs` sin cambios, no pasa nada — pero si Task 2 no está commiteada por separado, revisar. Listar ficheros explícitos si hace falta.)

---

### Task 4: 7ª tarjeta "Limpieza profunda" en UI

**Files:**
- Modify: `src/CA-O.UI/LimpiezaPage.xaml` (nueva `CardDeep` + RowDefinition + setter Narrow)
- Modify: `src/CA-O.UI/LimpiezaPage.xaml.cs` (`TargetFor` + `ConfirmIfNeededAsync`)

**Interfaces:**
- Consumes: los 5 IDs de Task 3; `OnCleanClick`, `RunCleanupAsync`, `CaoCardStyle`, `CaoActionButtonStyle`, `UiAnimations.CardHover`.
- Produces: `DeepStatusText` (TextBlock).

- [ ] **Step 1: XAML — 4ª fila + CardDeep (verbatim)**

a) En `Grid.RowDefinitions` (líneas 49-51), añadir una 4ª fila: `<RowDefinition Height="Auto"/>` (quedan 4).

b) En setters `Narrow` (líneas 19-33), añadir tras el de `MiniStack`:
```xml
<Setter Target="CardDeep.(Grid.Row)" Value="6"/>
<Setter Target="CardDeep.(Grid.Column)" Value="0"/>
```

c) Tras el cierre de `MiniStack` (`</StackPanel>` línea 370) y antes del cierre del `Grid` (línea 371), insertar (verbatim):

```xml
<!-- Limpieza profunda (nuevas rutas seguras) -->
<Border x:Name="CardDeep" Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2" Style="{StaticResource CaoCardStyle}" VerticalAlignment="Stretch" helpers:UiAnimations.CardHover="True">
    <Grid RowSpacing="4">
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="10" Margin="0,0,0,6">
            <Border Width="36" Height="36" CornerRadius="18" Background="{StaticResource CaoAmberSoftBrush}">
                <FontIcon Glyph="&#xE9D9;" FontSize="16" Foreground="{StaticResource CaoAmberSolidBrush}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Limpieza profunda" FontWeight="SemiBold" FontSize="14"/>
                <TextBlock Text="Nuevas rutas seguras · informa archivos y MB" FontSize="11" Opacity="0.65"/>
            </StackPanel>
        </StackPanel>
        <StackPanel Grid.Row="1" Spacing="0">
            <Grid ColumnSpacing="12" Padding="0,8,0,8">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <StackPanel Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Prefetch antiguo" FontWeight="SemiBold" FontSize="13"/>
                    <TextBlock Text="*.pf de +30 días. Solo espacio, no acelera." FontSize="11" Opacity="0.7" TextWrapping="Wrap"/>
                </StackPanel>
                <Button Grid.Column="1" Content="Limpiar" Tag="cleanup-prefetch-stale" Click="OnCleanClick" Style="{StaticResource CaoActionButtonStyle}" MinWidth="128" VerticalAlignment="Center" AutomationProperties.Name="Limpiar prefetch antiguo" ToolTipService.ToolTip="Borra rastros prefetch (*.pf) de más de 30 días. Solo libera espacio; Windows lo reconstruye."/>
            </Grid>
            <Grid ColumnSpacing="12" Padding="0,8,0,8">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <StackPanel Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Registros CBS" FontWeight="SemiBold" FontSize="13"/>
                    <TextBlock Text="*.log de +30 días en Windows\Logs\CBS." FontSize="11" Opacity="0.7" TextWrapping="Wrap"/>
                </StackPanel>
                <Button Grid.Column="1" Content="Limpiar" Tag="cleanup-cbs-logs" Click="OnCleanClick" Style="{StaticResource CaoActionButtonStyle}" MinWidth="128" VerticalAlignment="Center" AutomationProperties.Name="Limpiar registros CBS" ToolTipService.ToolTip="Borra registros CBS (*.log) de más de 30 días. Solo libera espacio."/>
            </Grid>
            <Grid ColumnSpacing="12" Padding="0,8,0,8">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <StackPanel Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Volcados extendidos" FontWeight="SemiBold" FontSize="13"/>
                    <TextBlock Text="LiveKernelReports, MEMORY.DMP y CrashDumps +30 días. Pide confirmación." FontSize="11" Opacity="0.7" TextWrapping="Wrap"/>
                </StackPanel>
                <Button Grid.Column="1" Content="Limpiar" Tag="cleanup-crash-dumps-extended" Click="OnCleanClick" Style="{StaticResource CaoActionButtonStyle}" MinWidth="128" VerticalAlignment="Center" AutomationProperties.Name="Limpiar volcados extendidos" ToolTipService.ToolTip="Borra LiveKernelReports, MEMORY.DMP y CrashDumps de usuario de más de 30 días. Pide confirmación: dificulta depurar fallos viejos."/>
            </Grid>
            <Grid ColumnSpacing="12" Padding="0,8,0,8">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <StackPanel Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Caché de Outlook" FontWeight="SemiBold" FontSize="13"/>
                    <TextBlock Text="Adjuntos temporales +7 días, todos los usuarios." FontSize="11" Opacity="0.7" TextWrapping="Wrap"/>
                </StackPanel>
                <Button Grid.Column="1" Content="Limpiar" Tag="cleanup-outlook-cache" Click="OnCleanClick" Style="{StaticResource CaoActionButtonStyle}" MinWidth="128" VerticalAlignment="Center" AutomationProperties.Name="Limpiar caché de Outlook" ToolTipService.ToolTip="Borra adjuntos temporales de Outlook (Content.Outlook) de más de 7 días. Se regeneran solos."/>
            </Grid>
            <Grid ColumnSpacing="12" Padding="0,8,0,8">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <StackPanel Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Cachés de navegadores" FontWeight="SemiBold" FontSize="13"/>
                    <TextBlock Text="Chrome, Edge y Teams. Cierra cada app antes." FontSize="11" Opacity="0.7" TextWrapping="Wrap"/>
                </StackPanel>
                <Button Grid.Column="1" Content="Limpiar" Tag="cleanup-browser-code-cache" Click="OnCleanClick" Style="{StaticResource CaoActionButtonStyle}" MinWidth="128" VerticalAlignment="Center" AutomationProperties.Name="Limpiar cachés de navegadores" ToolTipService.ToolTip="Vacía cachés regenerables de Chrome, Edge y Teams (nunca sesiones ni contraseñas). Cierra cada app primero o se omite lo en uso."/>
            </Grid>
        </StackPanel>
        <TextBlock Grid.Row="2" x:Name="DeepStatusText" FontSize="11" Opacity="0.75" TextWrapping="Wrap" VerticalAlignment="Bottom" AutomationProperties.LiveSetting="Polite"/>
    </Grid>
</Border>
```

NOTA glyph `&#xE9D9;` reutilizado de CardWinSxS (mismo significado: mantenimiento). Sin estilos nuevos: solo `CaoCardStyle`, `CaoActionButtonStyle`, `CaoAmberSoftBrush` (ya usados en la página).

- [ ] **Step 2: code-behind — TargetFor + confirm (verbatim)**

a) En `TargetFor` (líneas 78-89), añadir antes del `_ =>`:
```csharp
"cleanup-prefetch-stale" or "cleanup-cbs-logs" or "cleanup-crash-dumps-extended"
    or "cleanup-outlook-cache" or "cleanup-browser-code-cache" => DeepStatusText,
```

b) En `ConfirmIfNeededAsync` (líneas 93-104), añadir al switch:
```csharp
"cleanup-crash-dumps-extended" => ("Volcados extendidos",
    "Borra LiveKernelReports, MEMORY.DMP y CrashDumps de usuario de más de 30 días. Dificulta depurar fallos antiguos y no se puede deshacer. ¿Continuar?"),
```

- [ ] **Step 3: build UI en verde**

Run: `dotnet build src/CA-O.UI/CA-O.UI.csproj -c Release` (0 err/0 warn) + `dotnet test tests/CA-O.UI.Tests -c Release` (verde).
Expected: PASS. Verificado-vía-build (la página no tiene tests de navegación; el XAML lo valida el compilador).

- [ ] **Step 4: commit**

```bash
git add src/CA-O.UI/LimpiezaPage.xaml src/CA-O.UI/LimpiezaPage.xaml.cs
git commit -m "feat(clean): tarjeta Limpieza profunda con 5 rutas nuevas"
```

---

### Task 5: Test contrato anti-duplicados + inventario regenerado + docs históricos

**Files:**
- Test: `tests/CA-O.Core.Tests/NoDuplicateTargetsTests.cs` (nuevo)
- Modify: `OPTIMIZATION_INVENTORY.md` (5 filas + conteo 92 + nota retirado)
- Modify: `OPTIMIZATION_REFACTORING_STATUS.md` (banner histórico)
- Modify: `audit-optimizations.ps1`, `audit-retired.ps1` (cabecera histórica)

**Interfaces:**
- Consumes: `OptimizationCatalog.All` (92), `TempFileCleanupOptimization.Targets` (protected → reflexión), `RegistryOptimizationBase.Targets` (protected → reflexión).
- Produces: garantía CI de que ningún futuro duplicado entra sin declararse.

- [ ] **Step 1: test anti-duplicados (verbatim, fichero nuevo)**

```csharp
using System.Reflection;
using CAO.Core.Catalog;
using CAO.Core.Optimizations;
using CAO.Core.Optimizations.Storage;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Contrato anti-duplicados (spec §5.3): dos IDs distintos no pueden
/// reclamar el mismo directorio de limpieza ni la misma clave de registro,
/// salvoallowlist explícita con justificación. Impide futuros WU-dups en CI.</summary>
public sealed class NoDuplicateTargetsTests
{
    /// <summary>Pares declarados que comparten objetivo a propósito (vacío hoy).</summary>
    private static readonly HashSet<string> AllowlistedSharedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    [Fact]
    public void CleanupDirs_AreClaimedByASingleId()
    {
        var prop = typeof(TempFileCleanupOptimization).GetProperty("Targets",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(prop);
        var expand = typeof(TempFileCleanupOptimization).GetMethod("ExpandDir",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(expand);

        var byDir = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in OptimizationCatalog.All.OfType<TempFileCleanupOptimization>())
        {
            var id = opt.Definition.Id;
            var targets = (System.Collections.IEnumerable)prop.GetValue(opt)!;
            foreach (var item in targets)
            {
                var dirProp = item.GetType().GetProperty("Directory");
                var raw = (string)dirProp!.GetValue(item)!;
                var expanded = ((string)expand.Invoke(null, [raw])!).TrimEnd('\\').ToUpperInvariant();
                var key = $"DIR::{expanded}";
                if (byDir.TryGetValue(key, out var other)
                    && !other.Equals(id, StringComparison.Ordinal)
                    && !AllowlistedSharedTargets.Contains($"{other}|{id}")
                    && !AllowlistedSharedTargets.Contains($"{id}|{other}"))
                {
                    Assert.Fail($"Directorio compartido sin declarar: '{expanded}' reclamado por '{other}' y '{id}'.");
                }
                byDir[key] = id;
            }
        }
    }

    [Fact]
    public void RegistryKeys_AreClaimedByASingleId()
    {
        var prop = typeof(RegistryOptimizationBase).GetProperty("Targets",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(prop);

        var byKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in OptimizationCatalog.All.OfType<RegistryOptimizationBase>())
        {
            var id = opt.Definition.Id;
            var targets = (System.Collections.IEnumerable)prop.GetValue(opt)!;
            foreach (var item in targets)
            {
                var t = item.GetType();
                var hive = t.GetProperty("Hive")!.GetValue(item)!.ToString();
                var keyPath = (string)t.GetProperty("KeyPath")!.GetValue(item)!;
                var valueName = (string)t.GetProperty("ValueName")!.GetValue(item)!;
                var key = $"REG::{hive}\\{keyPath}\\{valueName}".ToUpperInvariant();
                if (byKey.TryGetValue(key, out var other)
                    && !other.Equals(id, StringComparison.Ordinal)
                    && !AllowlistedSharedTargets.Contains($"{other}|{id}")
                    && !AllowlistedSharedTargets.Contains($"{id}|{other}"))
                {
                    Assert.Fail($"Clave compartida sin declarar: '{key}' reclamada por '{other}' y '{id}'.");
                }
                byKey[key] = id;
            }
        }
    }
}
```

RULING (parte del brief, no desviación): si alguno de los 2 tests FAIL por un solape REAL pre-existente (p. ej. dos gaming sobre `DirectXUserGlobalSettings`, o `%TEMP%`-usuario de Task 2 colisionando con otro TempFile que liste el mismo dir), NO se toca producción: se añade la pareja a `AllowlistedSharedTargets` como `$"idA|idB"` con comentario de justificación + se reporta como concern (el reviewer decide si exige fusión). El objetivo es impedir duplicados FUTUROS silenciosos, no reescribir el catálogo hoy.

NOTA: `ExpandDir` es `protected static` (base líneas 27-30) — la reflexión lo alcanza. Los tuples `(Directory, Pattern, OlderThanDays)` exponen `Directory` como propiedad pública del ValueTuple — la reflexión la alcanza.

- [ ] **Step 2: test en verde (o allowlist justificada)**

Run: `dotnet test tests/CA-O.Core.Tests -c Release --filter "FullyQualifiedName~NoDuplicateTargets"`
Expected: PASS, o FAIL con solape real → aplicar RULING (allowlist + concern en el report).

- [ ] **Step 3: regenerar inventario**

Leer `OPTIMIZATION_INVENTORY.md` completo. Aplicar:
a) Cabecera `88 production` → `92 production`; bloque de estado: `las **88**` → `las **92**`.
b) Añadir 5 filas siguiendo EXACTAMENTE el formato de fila existente (mismas columnas) para: `cleanup-prefetch-stale`, `cleanup-cbs-logs`, `cleanup-crash-dumps-extended`, `cleanup-outlook-cache`, `cleanup-browser-code-cache` (datos = Definitions de Task 3).
c) La fila de `disk-cleanup-system-files`: marcarla `RETIRED → alias de cleanup-windows-update-cache` (mantener la fila para trazabilidad, no borrarla).
d) Verificar que el número de filas PRODUCTION + 1 RETIRED cuadra con `All` (92) + `LegacyIds` (3).

- [ ] **Step 4: docs históricos (verbatim)**

a) Al inicio de `OPTIMIZATION_REFACTORING_STATUS.md`, anteponer:
```markdown
> HISTÓRICO (2026-09-19): documento desincronizado (hablaba de 19/21 en producción). Estado real y vinculante: `OPTIMIZATION_INVENTORY.md` + tests de catálogo (`OptimizationCatalogContractTests`, `NoDuplicateTargetsTests`). No actualizar este fichero; se conserva por trazabilidad.

---
```

b) Al inicio de `audit-optimizations.ps1` y `audit-retired.ps1`, anteponer (comentario PowerShell):
```powershell
# HISTÓRICO (2026-09-19): script desincronizado (19/21 prod). Estado real: OPTIMIZATION_INVENTORY.md + tests de catálogo. Se conserva por trazabilidad, no usar para auditar.
```

- [ ] **Step 5: build + Core verde + commit**

Run: `dotnet build CA-O.sln -c Release` + `dotnet test tests/CA-O.Core.Tests -c Release`. Expected: PASS.
```bash
git add tests/CA-O.Core.Tests/NoDuplicateTargetsTests.cs OPTIMIZATION_INVENTORY.md OPTIMIZATION_REFACTORING_STATUS.md audit-optimizations.ps1 audit-retired.ps1
git commit -m "feat(clean): contrato anti-duplicados e inventario regenerado"
```

---

### Task 6: Verificación global Plan 03

**Files:** ninguno (solo comandos).

- [ ] **Step 1: suite completa en verde**

Run: `dotnet build CA-O.sln -c Release`
Expected: 13 proyectos, 0 errores, 0 warnings.

- [ ] **Step 2: tests completos**

Run: `dotnet test CA-O.sln -c Release`
Expected: todos los proyectos en verde (base 1017 + ~2 Task 2 + ~5 Task 3 + ~2 Task 5 ≈ 1026; el número exacto se reporta, lo que importa es 0 failed).

- [ ] **Step 3: reportar**

Reportar: nº exacto de tests, lista de commits de la rama, concerns abiertos. Sin commit (no hay cambios).

---

## Self-Review

1. **Spec coverage (§5.1/§5.2 + aceptación §8 línea limpieza):** dedup WU→T1; fix %TEMP% usuario→T2; 5 rutas nuevas (prefetch, CBS, volcados ext, Outlook, navegadores)→T3; confirm donde toca (MEMORY.DMP)→T4; 7ª card con MB (FormatSize override)→T3+T4; NoDuplicateTargets→T5; inventario→T5. `WinSxS solo StartComponentCleanup en batch` ya es el estado actual (ResetBase con gate Expert) — sin cambio. `DataStore.edb` prohibido — respetado (ninguna target lo toca).
2. **Placeholder scan:** sin TBD/TODO; cada step trae código verbatim o comando exacto; los 2 puntos con variación mecánica (nombre del stub de TestDoubles, grep de mensajes) tienen instrucción exacta de resolución.
3. **Type consistency:** `CanonicalIdFor`/`RetiredAliases` definidos en T1 y consumidos solo en T1; `ProfileSubDirs`/`InteractiveUserTempDirs` (internal, T2) consumidos en T2/T3; `FormatSize` virtual (T2) con override en T3; `DeepStatusText` producido en T4-Step 1 y consumido en T4-Step 2; IDs de Task 3 consumidos en T4/T5. `Targets` es `IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)>` en los 3 sitios. `PreviewLine.Target`/`Kind` usados en tests según base líneas 131-139.
4. **Fix aplicado en self-review:** test `CrashDumpsExtended_OnlyDumpPatterns` usa `Contains` (no `EndsWith`), porque `PreviewLine.Target` lleva sufijo `" (>Nd)"`.
