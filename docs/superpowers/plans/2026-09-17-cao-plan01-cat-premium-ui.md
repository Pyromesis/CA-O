# Plan 01 — Gato CA-O + UI premium (mascota animada + pulido visual)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** La app muestra un gato-mascota vectorial animado (control `CaoCat`) con 5 moods, integrado en el hero del Dashboard y reutilizable, más pulido premium (entradas escalonadas y hovers) en Limpieza/Benchmark/Dashboard.

**Architecture:** `UiState.MascotMood` (string) es la única fuente de verdad del mood; un catálogo puro `CatMoodCatalog` mapea mood → caption + clave de animación (testeable sin hilo UI); el control `CaoCat` (UserControl XAML vectorial, Storyboards por mood, gateado por `ReducedMotion`) consume ambos. Sin dependencias nuevas, sin PNGs obligatorios.

**Tech Stack:** .NET 10, WinUI 3 / WindowsAppSDK 2.4, CommunityToolkit.Mvvm 8.4.0, xUnit 2.9.2.

**Spec:** `docs/superpowers/specs/2026-09-17-cao-premium-cat-benchmark-cleanup-design.md` (Sección 1 gato + premium UI).

## Global Constraints

- Sin dependencias/NuGet nuevos; solo XAML + Storyboard + código existente.
- Toda animación se desactiva con `CAO.UI.Accessibility.ReducedMotion.ShouldAnimate` (frame final estático).
- Nombres de estilo nuevos con prefijo `Cao`; geometría 8px; Semibold en títulos (no Bold).
- Textos visibles en español (es-ES primero); sin textos hardcodeados que pisen `Localizer` donde ya se usa.
- `dotnet build CA-O.sln -c Release` y `dotnet test tests/CA-O.UI.Tests -c Release` en verde antes de cada commit.
- Commits pequeños, uno por tarea, en español.

---

## File Structure

- Modify: `src/CA-O.UI/ViewModels/UiState.cs` — añade `MascotMood:string` (default `"Idle"`) con `SetProperty`.
- Create: `src/CA-O.UI/Controls/CatMoodCatalog.cs` — mapa puro mood → `(Caption, AnimationKey)`; moods válidos: `Idle, Working, Celebrate, Warn, Sleep`.
- Create: `src/CA-O.UI/Controls/CaoCat.xaml` + `src/CA-O.UI/Controls/CaoCat.xaml.cs` — UserControl gato vectorial (Ellipse/Path), `Mood` DependencyProperty, Storyboards por mood, método `SetMood(string)`.
- Create: `tests/CA-O.UI.Tests/MascotMoodTests.cs` — tests de `UiState.MascotMood` + `CatMoodCatalog`.
- Modify: `src/CA-O.UI/DashboardPage.xaml` (+ `.xaml.cs`) — inserta `CaoCat` en hero (`CaoHeroCardStyle`, junto a `ScoreArc`), caption bajo el gato, transiciones de mood según estado (analizando→Working, completado→Celebrate 4s→Idle, servicio caído→Warn, inactivo 10min→Sleep).
- Modify: `src/CA-O.UI/LimpiezaPage.xaml.cs`, `src/CA-O.UI/BenchmarkPage.xaml.cs` — `UiAnimations.PlayEntrance` en el contenedor raíz al cargar + `CardHover="True"` en tarjetas principales del XAML.
- Modify: `src/CA-O.UI/Resources/DesignTokens.xaml` — tokens `CaoCatSize` (120), `CaoCatCaptionStyle`, duraciones `CaoCatBounceMs` (900) si faltan.

---

### Task 1: Estado MascotMood + catálogo puro testeable

**Files:**
- Modify: `src/CA-O.UI/ViewModels/UiState.cs`
- Create: `src/CA-O.UI/Controls/CatMoodCatalog.cs`
- Test: `tests/CA-O.UI.Tests/MascotMoodTests.cs`

**Interfaces:**
- Consumes: `ObservableObject.SetProperty` (ya usado en UiState.cs:29), nada nuevo.
- Produces: `UiState.MascotMood { get; set; }` (string, default `"Idle"`); `CatMoodCatalog.Resolve(string mood) -> (string Caption, string AnimationKey)`; `CatMoodCatalog.ValidMoods -> IReadOnlyList<string>` con exactamente `Idle, Working, Celebrate, Warn, Sleep`; mood desconocido/nulo → fallback `Idle`.

- [ ] **Step 1: Añadir MascotMood a UiState**

En `src/CA-O.UI/ViewModels/UiState.cs`, tras el campo `_analysisAgeLabel` (líneas 25-26), añadir campo + propiedad:

```csharp
private string _mascotMood = "Idle";

/// <summary>Mood actual de la mascota (Idle/Working/Celebrate/Warn/Sleep). Fuente de verdad para CaoCat.</summary>
public string MascotMood
{
    get => _mascotMood;
    set => SetProperty(ref _mascotMood, string.IsNullOrWhiteSpace(value) ? "Idle" : value);
}
```

- [ ] **Step 2: Crear CatMoodCatalog**

Crear `src/CA-O.UI/Controls/CatMoodCatalog.cs`:

```csharp
namespace CAO.UI.Controls;

/// <summary>Mapa puro mood → caption + clave de animación. Sin dependencias UI: testeable headless.</summary>
public static class CatMoodCatalog
{
    public static IReadOnlyList<string> ValidMoods { get; } =
        new[] { "Idle", "Working", "Celebrate", "Warn", "Sleep" };

    public static (string Caption, string AnimationKey) Resolve(string? mood) =>
        mood switch
        {
            "Working" => ("Manos a la obra…", "Working"),
            "Celebrate" => ("¡Listo! Buen trabajo.", "Celebrate"),
            "Warn" => ("Ojo, algo necesita atención.", "Warn"),
            "Sleep" => ("Zzz… aquí estaré.", "Sleep"),
            _ => ("¿Qué optimizamos hoy?", "Idle"),
        };
}
```

- [ ] **Step 3: Escribir los tests (failing first)**

Crear `tests/CA-O.UI.Tests/MascotMoodTests.cs`:

```csharp
using CAO.UI.Controls;
using CAO.UI.ViewModels;

public sealed class MascotMoodTests
{
    [Fact]
    public void UiState_MascotMood_DefaultsToIdle()
    {
        var state = new UiState();
        Assert.Equal("Idle", state.MascotMood);
    }

    [Fact]
    public void UiState_MascotMood_BlankFallsBackToIdle()
    {
        var state = new UiState { MascotMood = "   " };
        Assert.Equal("Idle", state.MascotMood);
    }

    [Theory]
    [InlineData("Idle", "¿Qué optimizamos hoy?")]
    [InlineData("Working", "Manos a la obra…")]
    [InlineData("Celebrate", "¡Listo! Buen trabajo.")]
    [InlineData("Warn", "Ojo, algo necesita atención.")]
    [InlineData("Sleep", "Zzz… aquí estaré.")]
    public void Catalog_Resolve_KnownMoods(string mood, string caption)
    {
        var (actualCaption, key) = CatMoodCatalog.Resolve(mood);
        Assert.Equal(caption, actualCaption);
        Assert.Equal(mood, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bailando")]
    public void Catalog_Resolve_UnknownFallsBackToIdle(string? mood)
    {
        var (caption, key) = CatMoodCatalog.Resolve(mood);
        Assert.Equal("Idle", key);
        Assert.Equal("¿Qué optimizamos hoy?", caption);
    }

    [Fact]
    public void Catalog_ValidMoods_HasExactlyFive()
    {
        Assert.Equal(new[] { "Idle", "Working", "Celebrate", "Warn", "Sleep" }, CatMoodCatalog.ValidMoods);
    }
}
```

NOTA: verificar que `tests/CA-O.UI.Tests/CA-O.UI.Tests.csproj` referencia el proyecto `CA-O.UI` (lo hace `ViewModelTests.cs`; si no compila, añadir `<ProjectReference Include="..\..\src\CA-O.UI\CA-O.UI.csproj" />` igual que los vecinos).

- [ ] **Step 4: Correr tests, verlos fallar (aún no existe el código de Steps 1-2 si se hacen en orden inverso; si ya se crearon, deben pasar)**

Run: `dotnet test tests/CA-O.UI.Tests -c Release --filter "FullyQualifiedName~MascotMood"`
Expected: PASS (si Steps 1-2 ya hechos) — si se escribió el test antes que el código: FAIL con "type or namespace not found".

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.UI/ViewModels/UiState.cs src/CA-O.UI/Controls/CatMoodCatalog.cs tests/CA-O.UI.Tests/MascotMoodTests.cs
git commit -m "feat(ui): MascotMood en UiState + CatMoodCatalog con tests"
```

Verificación: `dotnet build CA-O.sln -c Release` verde + test del Step 4 en verde.

---

### Task 2: Control CaoCat (gato vectorial animado, 5 moods)

**Files:**
- Create: `src/CA-O.UI/Controls/CaoCat.xaml`
- Create: `src/CA-O.UI/Controls/CaoCat.xaml.cs`
- Test: `tests/CA-O.UI.Tests/MascotMoodTests.cs` (ampliar: ciclo mood→clave; el XAML se verifica con build + captura)

**Interfaces:**
- Consumes: `CatMoodCatalog.Resolve` (Task 1), `ReducedMotion.ShouldAnimate` (`src/CA-O.UI/Accessibility/ReducedMotion.cs:26`).
- Produces: `CaoCat : UserControl` con `DependencyProperty Mood (string)`, método `SetMood(string mood)`, caption interno actualizado vía `CatMoodCatalog`.

- [ ] **Step 1: Crear CaoCat.xaml (gato vectorial ~120px)**

Gato sentado de frente, construido con formas: cuerpo (Ellipse 84x96), cabeza (Ellipse 72x64), 2 orejas (Path triángulos), 2 ojos (Ellipse 10x14, color acento sistema), hocico (Path), 3 bigotes por lado (Line), cola (Path curvo, x:Name="Tail" para animarla), barriga (Ellipse clara). Todo con `ThemeResource` (TextFill/SystemAccent) para tema claro/oscuro. Estructura:

```xml
<UserControl x:Class="CAO.UI.Controls.CaoCat" ... Width="120" Height="140">
  <StackPanel Spacing="4" HorizontalAlignment="Center">
    <Grid x:Name="CatBody" Width="120" Height="120" RenderTransformOrigin="0.5,1">
      <Grid.RenderTransform><CompositeTransform/></Grid.RenderTransform>
      <!-- cola, cuerpo, cabeza, orejas, ojos (x:Name="EyeL/EyeR"), bigotes -->
      <Grid.Resources>
        <Storyboard x:Name="IdleBoard" RepeatBehavior="Forever">...respiración: escala Y 1.0→1.03 1600ms...</Storyboard>
        <Storyboard x:Name="WorkingBoard" RepeatBehavior="Forever">...meneo ±4° 700ms + parpadeo...</Storyboard>
        <Storyboard x:Name="CelebrateBoard">...salto TranslateY -18px 2 veces + cola alta...</Storyboard>
        <Storyboard x:Name="WarnBoard" RepeatBehavior="Forever">...leve temblor ±1.5px 300ms, ojos más abiertos...</Storyboard>
        <Storyboard x:Name="SleepBoard" RepeatBehavior="Forever">...ojos cerrados (escala Y 0.1), "Z" flotante fade...</Storyboard>
      </Grid.Resources>
    </Grid>
    <TextBlock x:Name="CaptionText" Style="{StaticResource CaoFontSizeCaption}" Opacity="0.75" TextWrapping="Wrap" TextAlignment="Center"/>
  </StackPanel>
</UserControl>
```

Parpadeo Idle: animar `EyeL/EyeR.ScaleY` 1→0.08→1 cada ~4s (DoubleAnimationUsingKeyFrames). Mantener cada Storyboard < 15 líneas de keyframes; reutilizar `CubicEase EaseOut`.

- [ ] **Step 2: Crear CaoCat.xaml.cs**

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace CAO.UI.Controls;

public sealed partial class CaoCat : UserControl
{
    public static readonly DependencyProperty MoodProperty =
        DependencyProperty.Register(nameof(Mood), typeof(string), typeof(CaoCat),
            new PropertyMetadata("Idle", (d, _) => ((CaoCat)d).ApplyMood()));

    public string Mood { get => (string)GetValue(MoodProperty); set => SetValue(MoodProperty, value); }

    public CaoCat() { InitializeComponent(); Loaded += (_, _) => ApplyMood(); }

    public void SetMood(string? mood)
    {
        var next = string.IsNullOrWhiteSpace(mood) ? "Idle" : mood!;
        if (!CatMoodCatalog.ValidMoods.Contains(next)) next = "Idle";
        Mood = next;
    }

    private void ApplyMood()
    {
        var (caption, key) = CatMoodCatalog.Resolve(Mood);
        CaptionText.Text = caption;
        foreach (var name in new[] { "IdleBoard", "WorkingBoard", "CelebrateBoard", "WarnBoard", "SleepBoard" })
            if (FindName(name) is Storyboard b) b.Stop();
        if (!Accessibility.ReducedMotion.ShouldAnimate) return; // frame estático + caption
        if (FindName(key + "Board") is Storyboard run) run.Begin();
    }
}
```

- [ ] **Step 3: Build del proyecto UI**

Run: `dotnet build src/CA-O.UI -c Release`
Expected: 0 errores. (Errores típicos: `FindName` en Resources — mover Storyboards a `<Grid.Resources>` con `x:Name` funciona con FindName solo si están en el árbol de nombres del UserControl; alternativa: guardar referencias en campos vía `Resources["IdleBoard"]`. Si FindName falla, usar `((Storyboard)CatBody.Resources[key + "Board"])`.)

- [ ] **Step 4: Commit**

```bash
git add src/CA-O.UI/Controls/CaoCat.xaml src/CA-O.UI/Controls/CaoCat.xaml.cs
git commit -m "feat(ui): control CaoCat, gato vectorial con 5 moods animados"
```

---

### Task 3: Integrar el gato en el hero del Dashboard + transiciones de mood

**Files:**
- Modify: `src/CA-O.UI/DashboardPage.xaml` (hero `CaoHeroCardStyle`, línea 33; columna del score `ScoreArc`, líneas 79-106)
- Modify: `src/CA-O.UI/DashboardPage.xaml.cs` (leer el archivo primero: buscar `OnAnalyzeClick`, `AnalyzeStatusText`, render de `UiState`)
- Test: verificación manual con captura (screenshot) + tests existentes en verde.

**Interfaces:**
- Consumes: `CaoCat.SetMood`, `UiState.MascotMood`, `CatMoodCatalog` (Tasks 1-2).
- Produces: hero con gato visible; reglas de transición: al pulsar Analizar → `Working`; al completar análisis → `Celebrate` 4s → `Idle`; servicio privilegiado caído → `Warn`; 10 min sin interacción → `Sleep` (DispatcherTimer, solo si `ShouldAnimate`).

- [ ] **Step 1: Leer DashboardPage.xaml.cs (anclas exactas)**

Run: `grep -n "OnAnalyzeClick\|AnalyzeStatusText\|UiState\|AnalyzingRing" src/CA-O.UI/DashboardPage.xaml.cs | head -30`
Anotar: nombre del método que completa el análisis y dónde se obtiene `UiState` (probablemente `AppHost.Resolve<UiState>()`).

- [ ] **Step 2: Insertar CaoCat en el hero XAML**

Junto a la columna del score (grid que contiene `VerdictPill`/`ScoreArc`, líneas ~79-106), añadir columna o StackPanel con:

```xml
<controls:CaoCat x:Name="HeroCat" Width="120" Height="140"
                 AutomationProperties.Name="Mascota de CA-O"/>
```

Añadir `xmlns:controls="using:CAO.UI.Controls"` al Page si no existe. Caption ya incluido en el control. En `Narrow` (<1100px) el gato se apila sobre el score (ponerlo dentro del StackPanel principal del hero, no en columna fija).

- [ ] **Step 3: Transiciones de mood en code-behind**

```csharp
private DispatcherTimer? _sleepTimer;
private void SetCat(string mood)
{
    try
    {
        HeroCat.SetMood(mood);
        AppHost.Resolve<UiState>().MascotMood = mood;
        ArmSleepTimer();
    }
    catch { /* la mascota nunca rompe la página */ }
}
```

Llamar `SetCat("Working")` al iniciar análisis; al completar: `SetCat("Celebrate")` + `Task.Delay(4000)` → `SetCat("Idle")` (guardar con try/catch si la página ya se cerró); si el estado del servicio es caído al renderizar: `SetCat("Warn")`. `_sleepTimer` 10 min → `SetCat("Sleep")`, se rearma en cada `SetCat` y en `PointerMoved` de la página.

- [ ] **Step 4: Verificar build + tests UI**

Run: `dotnet build CA-O.sln -c Release`
Expected: verde.
Run: `dotnet test tests/CA-O.UI.Tests -c Release`
Expected: verde (incluye MascotMoodTests).

- [ ] **Step 5: Captura manual**

Ejecutar la app (`dotnet run --project src/CA-O.UI -c Release` o F5), captura del Dashboard con el gato en Idle y durante análisis (Working). Adjuntar/confirmar visualmente antes del commit.

- [ ] **Step 6: Commit**

```bash
git add src/CA-O.UI/DashboardPage.xaml src/CA-O.UI/DashboardPage.xaml.cs
git commit -m "feat(ui): gato en hero del Dashboard con transiciones de mood"
```

---

### Task 4: Pulido premium global (entradas + hovers + tokens)

**Files:**
- Modify: `src/CA-O.UI/LimpiezaPage.xaml` (+ `.xaml.cs`), `src/CA-O.UI/BenchmarkPage.xaml` (+ `.xaml.cs`), `src/CA-O.UI/DashboardPage.xaml.cs`
- Modify: `src/CA-O.UI/Resources/DesignTokens.xaml` (solo si falta algún token)
- Test: build + suite UI verde; sin cambios de comportamiento (solo visual).

**Interfaces:**
- Consumes: `UiAnimations.PlayEntrance(Panel)` (`src/CA-O.UI/Helpers/UiAnimations.cs:23`), attached `UiAnimations.CardHover` (línea 108), `CaoSpace*` tokens.
- Produces: mismas páginas, con entrada en cascada y hover 1.02 en tarjetas.

- [ ] **Step 1: Limpieza — entrada + hovers**

En `LimpiezaPage.xaml`: añadir `xmlns:helpers="using:CAO.UI.Helpers"` y `helpers:UiAnimations.CardHover="True"` a las 6 tarjetas (`CardTemp/CardDisk/CardWinSxS/CardTimer/CardSense/MiniStack` — verificar nombres exactos en el XAML). En `LimpiezaPage.xaml.cs`, al final del `Loaded`/constructor tras `InitializeComponent`: `UiAnimations.PlayEntrance(<contenedor raíz x:Name>)` (si el raíz no tiene x:Name, añadir `x:Name="PageContent"`).

- [ ] **Step 2: Benchmark — entrada + hovers**

Igual que Step 1 en `BenchmarkPage.xaml` / `.xaml.cs`: `CardHover` en las 4 cards de métrica + StepCard; `PlayEntrance` en el contenedor (`PageContent` o equivalente).

- [ ] **Step 3: Dashboard — entrada del hero**

`PlayEntrance(PageContent)` en `DashboardPage.xaml.cs` tras el render inicial (respetar que `PlayEntrance` ya gatea `ReducedMotion`).

- [ ] **Step 4: Verificar**

Run: `dotnet build CA-O.sln -c Release; if ($?) { dotnet test tests/CA-O.UI.Tests -c Release }`
Expected: todo verde.

- [ ] **Step 5: Commit**

```bash
git add src/CA-O.UI/LimpiezaPage.xaml src/CA-O.UI/LimpiezaPage.xaml.cs src/CA-O.UI/BenchmarkPage.xaml src/CA-O.UI/BenchmarkPage.xaml.cs src/CA-O.UI/DashboardPage.xaml.cs src/CA-O.UI/Resources/DesignTokens.xaml
git commit -m "feat(ui): entradas en cascada y hover premium en Dashboard/Limpieza/Benchmark"
```

---

## Self-Review (chequeo del plan contra el spec)

1. **Cobertura:** Spec §gato (5 moods, vectorial, ReducedMotion, hero Dashboard) → Tasks 1-3. Spec §premium (animaciones, imágenes) → Task 4 (animaciones) + gato vectorial como "imagen" nativa; PNGs opcionales quedan fuera de este plan a propósito (cero dependencias, cero binarios) — se anotan como follow-up, no como hueco: el spec pedía "imágenes" y este plan entrega ilustración vectorial propia en vez de bitmaps.
2. **Placeholders:** ninguno — cada step trae código o comando exacto. Nombres XAML de Limpieza a confirmar con grep antes de editar (Step 1 Task 4 lo exige).
3. **Consistencia de tipos:** `MascotMood:string`, `Resolve(string?)→(string,string)`, `SetMood(string?)`, claves `"IdleBoard"` = mood + `"Board"` — coherente en Tasks 1-2. `DispatcherTimer` existe en WinUI (`Microsoft.UI.Xaml`), igual que en `UiAnimations.cs:84`.
4. **Riesgo detectado:** `FindName` sobre Storyboards en Resources puede fallar → Step 3 Task 2 documenta la alternativa (`CatBody.Resources[...]`). El ejecutor la aplica si el build avisa.

## Planes siguientes (no parte de este plan)

- **Plan 02 — Benchmark útil:** `MeasureCpu`→ops/s multi-thread, MEM con `GC.TryStartNoGCRegion`, disco 256MB WriteThrough + 4K, veredicto por categoría, suelo 3% en `BenchmarkAnalyzer`, header+TTL+`{txid}.json`, auto-bench tras commit, DNS en página Benchmark.
- **Plan 03 — Limpieza ampliada:** Prefetch/CBS/LiveKernelReports/Content.Outlook/Code Cache, `%TEMP%` usuario real, fusión duplicado WU-cache, test `NoDuplicateTargets`.
- **Plan 04 — Catálogo honesto:** separar Repairs/Diagnostics/Restores, `Detect` reales, gates ExpertOnly, docs sincronizados.
