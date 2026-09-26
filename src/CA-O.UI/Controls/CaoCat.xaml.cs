using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;

namespace CAO.UI.Controls;

/// <summary>
/// Mascota CA-O: silueta de gato animada frame a frame (flipbook de PNG generados
/// con el motor local). El mood decide el ciclo y su velocidad; el tema de la
/// ventana decide el conjunto de arte (silueta oscura u clara).
/// El flipbook siempre anima: es el contenido de la mascota, no una
/// transición decorativa (ReducedMotion solo gobierna el resto de la UI).
/// Nunca lanza: la mascota no puede romper la página.
/// </summary>
public sealed partial class CaoCat : UserControl
{
    public static readonly DependencyProperty MoodProperty =
        DependencyProperty.Register(nameof(Mood), typeof(string), typeof(CaoCat),
            new PropertyMetadata("Idle", (d, _) => ((CaoCat)d).ApplyMood()));

    public static readonly DependencyProperty ShowCaptionProperty =
        DependencyProperty.Register(nameof(ShowCaption), typeof(bool), typeof(CaoCat),
            new PropertyMetadata(true, (d, _) => ((CaoCat)d).ApplyCaptionVisibility()));

    // Frames predecodificados e inmutables, compartidos (Panel y Benchmark
    // usan el mismo mood). BitmapImage compone siempre en este entorno;
    // SoftwareBitmapSource, no. Ancho de decodificación = display máximo.
    private const int FramePx = 132;
    private static readonly Dictionary<string, List<BitmapImage>> Frames = new();

    private DispatcherTimer? _timer;
    private List<BitmapImage>? _frames;
    private int _cells;
    // Ping-pong de opacidad: dos imágenes siempre Visible (nunca Collapsed,
    // que no compone en este entorno); una a opacidad 1 y otra a 0.01
    // (0.01 compone igual que visible pero es invisible al ojo; a 0 el
    // compositor puede saltarla y el reveal muestra un blanco de 1 frame). Por tick
    // se revela lo preparado el tick anterior y se prepara el siguiente en
    // la oculta. Sin eventos, sin swaps de Source en visible: sin blancos.
    private Image? _front;
    private Image? _back;
    private int _index;
    private int _staged = -1;
    private int _loaded;
    private int _cycles;
    private int _loadGen;
    private string? _currentKey;
    private INotifyPropertyChanged? _moodSource;
    private Storyboard? _breathBoard;
    private DispatcherTimer? _groomArm;
    private DispatcherTimer? _groomBack;

    public CaoCat()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
        try
        {
            // Ping-pong de opacidad con frames predecodificados: dos imágenes
            // siempre visibles (una a opacidad 0); por tick se revela lo ya
            // compuesto y se prepara el siguiente. El layout se ajusta al
            // Width real del control (48/64/96/132 según la página).
            SizeChanged += OnSizeChanged;
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] ctor: {ex.Message}"); }
    }

    public string Mood
    {
        get => (string)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    /// <summary>
    /// Muestra u oculta el caption bajo el gato. Las tarjetas compactas usan
    /// <c>False</c>: el gato queda cuadrado del ancho fijado y no se encoge.
    /// </summary>
    public bool ShowCaption
    {
        get => (bool)GetValue(ShowCaptionProperty);
        set => SetValue(ShowCaptionProperty, value);
    }

    /// <summary>Cambia el mood normalizando valores desconocidos a <c>Idle</c>.</summary>
    public void SetMood(string? mood) => Mood = MascotFlipbook.Normalize(mood);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log("Loaded");
        AttachGlobalMood();
        ApplyCaptionVisibility();
        InitBuffers();
        LayoutStrip();
        ApplyMood();
    }

    /// <summary>
    /// Inicializa el ping-pong (frontal + oculta, ambas siempre Visible; la
    /// oculta va a opacidad 0 en XAML; aquí se reasegura). Nunca lanza.
    /// </summary>
    private void InitBuffers()
    {
        try
        {
            _front = FrameA;
            _back = FrameB;
            if (_front is not null) _front.Opacity = 1;
            if (_back is not null) _back.Opacity = 0.01;
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] InitBuffers: {ex.Message}"); }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachGlobalMood();
        StopTimer();
        StopBreath();
        DisarmGroom();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => LayoutStrip();

    /// <summary>
    /// Ajusta el viewport al Width real y dimensiona ambas imágenes del doble
    /// búfer (cuadradas). Nunca lanza.
    /// </summary>
    private void LayoutStrip()
    {
        try
        {
            var d = double.IsNaN(Width) ? 64 : Width;
            if (d <= 0) d = 64;
            Log($"Layout size={d} frames={_frames?.Count ?? 0}");
            if (Viewport is not null)
            {
                Viewport.Width = d;
                Viewport.Height = d;
                Viewport.Clip = new RectangleGeometry { Rect = new Rect(0, 0, d, d) };
            }
            if (FrameA is not null)
            {
                FrameA.Width = d;
                FrameA.Height = d;
            }
            if (FrameB is not null)
            {
                FrameB.Width = d;
                FrameB.Height = d;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] LayoutStrip: {ex.Message}"); }
    }

    // CurrentChunkCells/LocalIndex del filmstrip retirados con ShowFrame.

    /// <summary>
    /// Sincronización global: todos los gatos visibles siguen a
    /// <c>UiState.MascotMood</c>, así el mismo ciclo se ve en todas las
    /// pestañas. Nunca lanza (diseñador/tests sin AppHost configurado).
    /// </summary>
    private void AttachGlobalMood()
    {
        try
        {
            DetachGlobalMood();
            var state = AppHost.Resolve<ViewModels.UiState>();
            _moodSource = state;
            state.PropertyChanged += OnGlobalMoodChanged;
            var global = MascotFlipbook.Normalize(state.MascotMood);
            if (MascotFlipbook.Normalize(Mood) != global)
                SetMood(global);
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] AttachGlobalMood: {ex.Message}"); }
    }

    private void DetachGlobalMood()
    {
        try
        {
            if (_moodSource is not null)
                _moodSource.PropertyChanged -= OnGlobalMoodChanged;
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] DetachGlobalMood: {ex.Message}"); }
        finally { _moodSource = null; }
    }

    private void OnGlobalMoodChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName != nameof(ViewModels.UiState.MascotMood)) return;
            if (sender is not ViewModels.UiState state) return;
            var global = MascotFlipbook.Normalize(state.MascotMood);
            if (MascotFlipbook.Normalize(Mood) == global) return;
            var queue = DispatcherQueue;
            if (queue is null) return;
            queue.TryEnqueue(() =>
            {
                try { SetMood(global); } catch { }
            });
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] OnGlobalMoodChanged: {ex.Message}"); }
    }

    private void ApplyCaptionVisibility()
    {
        try
        {
            if (CaptionText is null) return;
            CaptionText.Visibility = ShowCaption ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] ApplyCaptionVisibility: {ex.Message}"); }
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => ApplyMood();

    private void ApplyMood()
    {
        try
        {
            if (CaptionText is null) return;
            ApplyCaptionVisibility();
            var (caption, _) = CatMoodCatalog.Resolve(Mood);
            CaptionText.Text = caption;
            var mood = MascotFlipbook.Normalize(Mood);
            Log($"ApplyMood raw='{Mood}' norm='{mood}' key='{CurrentThemeKey()}/{mood}'");
            AutomationProperties.SetName(this, $"Mascota CA-O, estado {mood}");
            var key = $"{CurrentThemeKey()}/{mood}";
            if (key == _currentKey && _loaded > 1)
            {
                // Mismo ciclo ya en marcha: solo se reasegura el timer para que
                // la animación siga avanzando.
                StartTimer(); RestartBreath();
                return;
            }
            _currentKey = key;
            _index = 0;
            _staged = -1;
            _cycles = 0;
            if (Frames.TryGetValue(key, out var cached) && cached.Count > 0)
            {
                // Frames ya predecodificados: cambio instantáneo; primer frame
                // directo y el ping-pong de opacidad toma el relevo en OnTick.
                _frames = cached;
                _loaded = cached.Count;
                _cells = cached.Count;
                Log($"Frames hit {key}: n={cached.Count}");
                LayoutStrip();
                Present(cached[0], 0);
                if (_loaded > 1) { StartTimer(); RestartBreath(); }
                else { StopTimer(); StopBreath(); }
            }
            else
            {
                // Set nuevo: sigue mostrando el ciclo actual mientras se
                // predecodifica en segundo plano; al terminar se cambia.
                _ = LoadFramesAsync(key, CurrentThemeKey(), mood);
            }
            ArmGroom(mood);
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] ApplyMood: {ex.Message}"); }
    }

    private string CurrentThemeKey() =>
        MascotFlipbook.NormalizeTheme(ActualTheme == ElementTheme.Dark ? "dark" : "light");

    /// <summary>
    /// Construye los tramos del mood: cada PNG se decodifica a celda de
    /// 256px y se concatena en un solo bitmap ancho. El timer solo arranca
    /// con el strip en memoria. Si otro mood gana la carrera, el perdedor se
    /// descarta por generación. Nunca lanza.
    /// </summary>
    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "caocat.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private async Task LoadFramesAsync(string key, string theme, string mood)
    {
        var gen = ++_loadGen;
        try
        {
            var count = MascotFlipbook.FrameCount(mood);
            var list = new List<BitmapImage>();
            for (var i = 0; i < count; i++)
            {
                var bmp = await TryDecodeFrameAsync(theme, mood, i);
                if (gen != _loadGen || _currentKey != key) return;
                if (bmp is not null) list.Add(bmp);
            }
            Log($"Frames done {key}: n={list.Count}");
            if (gen != _loadGen || _currentKey != key) return;
            if (list.Count == 0)
            {
                Log($"sin frames para {key}: la mascota quedará estática.");
                Debug.WriteLine($"[CaoCat] sin frames para {key}: la mascota quedará estática.");
                StopTimer();
                StopBreath();
                return;
            }
            Frames[key] = list;
            _frames = list;
            _loaded = list.Count;
            _cells = list.Count;
            _staged = -1;
            Log($"Frames OK {key}: n={list.Count}");
            LayoutStrip();
            Present(list[0], 0);
            StartTimer();
            RestartBreath();
        }
        catch (Exception ex)
        {
            Log($"LoadFramesAsync {key}: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[CaoCat] LoadFramesAsync {key}: {ex.Message}");
        }
    }

    /// <summary>
    /// Decodifica un frame a <c>BitmapImage</c> (vía <c>ms-appx</c> y, si
    /// falla, vía ruta absoluta de archivo). Nunca lanza: devuelve
    /// <c>null</c>.
    /// </summary>
    private static async Task<BitmapImage?> TryDecodeFrameAsync(string theme, string mood, int index)
    {
        try
        {
            StorageFile? file = null;
            try
            {
                file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri(MascotFlipbook.AppxUri(theme, mood, index)));
            }
            catch
            {
                var relative = MascotFlipbook.RelativePath(theme, mood, index)
                    .Replace('/', Path.DirectorySeparatorChar);
                var full = Path.Combine(AppContext.BaseDirectory, relative);
                if (!File.Exists(full))
                {
                    Debug.WriteLine($"[CaoCat] falta frame: {full}");
                    return null;
                }
                file = await StorageFile.GetFileFromPathAsync(full);
            }
            var bmp = new BitmapImage { DecodePixelWidth = FramePx };
            using var stream = await file.OpenReadAsync();
            await bmp.SetSourceAsync(stream);
            return bmp;
        }
        catch (Exception ex)
        {
            Log($"decode {theme}/{mood}/f{index:00}: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[CaoCat] decode {theme}/{mood}/f{index:00}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Primera pintura: mismo frame en AMBAS imagenes (la oculta a 0.01
    /// compone igual, invisible al ojo) y opacidades reaseguradas. El tick
    /// siguiente revela superficie ya compuesta: sin blancos. Nunca lanza.
    /// </summary>
    private void Present(BitmapImage frame, int index)
    {
        try
        {
            _index = index;
            _staged = -1;
            if (_front is not null) { _front.Source = frame; _front.Opacity = 1; }
            if (_back is not null) { _back.Source = frame; _back.Opacity = 0.01; }
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] Present: {ex.Message}"); }
    }

    private void OnFrameOpened(object sender, RoutedEventArgs e)
    {
        // Presentación directa: sin swaps. Se conserva la firma.
        _ = sender;
        _ = e;
    }

    private void OnFrameFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // Presentación directa: sin swaps. Se conserva la firma.
        _ = sender;
        _ = e;
    }

    private void StartTimer()
    {
        _timer ??= new DispatcherTimer();
        _timer.Stop();
        _timer.Interval = TimeSpan.FromMilliseconds(MascotFlipbook.FrameMs(Mood));
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        try { _timer?.Stop(); } catch (Exception ex) { Debug.WriteLine($"[CaoCat] StopTimer: {ex.Message}"); }
    }

    private void OnTick(object? sender, object e)
    {
        try
        {
            if (_frames is null || _cells == 0) { StopTimer(); return; }
            // Ping-pong de opacidad con 1 tick de settle: se revela lo
            // preparado el tick anterior (ya compuesto) y se prepara el
            // siguiente en la oculta. Siempre hay una imagen con contenido
            // listo a opacidad 1: estructuralmente sin blancos. Sin eventos.
            if (_staged >= 0 && _staged < _frames.Count && _front is not null && _back is not null)
            {
                _back.Opacity = 1;
                _front.Opacity = 0.01;
                (_front, _back) = (_back, _front);
                _index = _staged;
                _staged = -1;
            }
            var target = (_index + 1) % Math.Max(1, _cells);
            // Los moods de ocasión vuelven solos a Idle tras sus ciclos: cada
            // animación es para su momento y no se queda pegada. Solo local:
            // el mood global no se toca. Sleep persiste hasta que cambie.
            if (target == 0 && MascotFlipbook.ReturnsToIdle(Mood) && ++_cycles >= MascotFlipbook.OneShotCycles(Mood))
            {
                try { SetMood("Idle"); } catch { }
                return;
            }
            if (target < _frames.Count && _frames[target] is BitmapImage f && _back is not null)
            {
                _back.Source = f;
                _staged = target;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] OnTick: {ex.Message}"); StopTimer(); }
    }

    // ShowFrame del filmstrip retirado: primera pintura con Present() y relevo por ping-pong de opacidad en OnTick.

    /// <summary>
    /// Respiración por composición (TranslateY + escala leve, AutoReverse en
    /// bucle): movimiento visible permanente aunque dos frames consecutivos se
    /// parezcan, sin coste de decodificación. El ritmo depende del mood.
    /// Nunca lanza: la mascota no puede romper la página.
    /// </summary>
    private void RestartBreath()
    {
        try
        {
            StopBreath();
            if (BreathTransform is null) return;
            var (liftPx, grow, ms) = BreathParams(MascotFlipbook.Normalize(Mood));
            var board = new Storyboard { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            var up = new DoubleAnimation { From = 0, To = -liftPx, Duration = TimeSpan.FromMilliseconds(ms) };
            Storyboard.SetTarget(up, BreathTransform);
            Storyboard.SetTargetProperty(up, "TranslateY");
            var sx = new DoubleAnimation { From = 1, To = 1 + grow, Duration = TimeSpan.FromMilliseconds(ms) };
            Storyboard.SetTarget(sx, BreathTransform);
            Storyboard.SetTargetProperty(sx, "ScaleX");
            var sy = new DoubleAnimation { From = 1, To = 1 + grow, Duration = TimeSpan.FromMilliseconds(ms) };
            Storyboard.SetTarget(sy, BreathTransform);
            Storyboard.SetTargetProperty(sy, "ScaleY");
            board.Children.Add(up);
            board.Children.Add(sx);
            board.Children.Add(sy);
            _breathBoard = board;
            board.Begin();
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] RestartBreath: {ex.Message}"); }
    }

    private void StopBreath()
    {
        try { _breathBoard?.Stop(); } catch (Exception ex) { Debug.WriteLine($"[CaoCat] StopBreath: {ex.Message}"); }
        finally { _breathBoard = null; }
    }

    private static (double liftPx, double grow, double ms) BreathParams(string mood) => mood switch
    {
        "Working" => (2, 0.015, 900),
        "Celebrate" => (6, 0.05, 700),
        "Warn" => (2, 0.02, 1200),
        "Sleep" => (4, 0.03, 2600),
        "Groom" => (3, 0.02, 1200),
        _ => (3, 0.02, 1800),
    };

    /// <summary>
    /// Auto-aseo: si el gato lleva 25 s en Idle (local y global), reproduce un
    /// ciclo de Groom y vuelve al mood global. Solo local: el resto de gatos y
    /// las páginas no se enteran. Nunca lanza.
    /// </summary>
    private void ArmGroom(string mood)
    {
        try
        {
            DisarmGroom();
            if (mood != "Idle") return;
            _groomArm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
            _groomArm.Tick += OnGroomArm;
            _groomArm.Start();
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] ArmGroom: {ex.Message}"); }
    }

    private void DisarmGroom()
    {
        try { if (_groomArm is not null) { _groomArm.Stop(); _groomArm.Tick -= OnGroomArm; } } catch { }
        finally { _groomArm = null; }
        try { if (_groomBack is not null) { _groomBack.Stop(); _groomBack.Tick -= OnGroomBack; } } catch { }
        finally { _groomBack = null; }
    }

    private void OnGroomArm(object? sender, object e)
    {
        try
        {
            DisarmGroom();
            if (MascotFlipbook.Normalize(Mood) != "Idle") return;
            if (GlobalMood() != "Idle") return;
            SetMood("Groom");
            _groomBack = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(MascotFlipbook.CycleMs("Groom") + 200),
            };
            _groomBack.Tick += OnGroomBack;
            _groomBack.Start();
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] OnGroomArm: {ex.Message}"); }
    }

    private void OnGroomBack(object? sender, object e)
    {
        try
        {
            DisarmGroom();
            if (MascotFlipbook.Normalize(Mood) != "Groom") return;
            SetMood(GlobalMood());
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] OnGroomBack: {ex.Message}"); }
    }

    private static string GlobalMood()
    {
        try { return MascotFlipbook.Normalize(AppHost.Resolve<ViewModels.UiState>().MascotMood); }
        catch { return "Idle"; }
    }
}
