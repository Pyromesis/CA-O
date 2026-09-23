using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;

namespace CAO.UI.Controls;

/// <summary>
/// Mascota CA-O: silueta de gato animada frame a frame (flipbook de PNG generados
/// con el motor local). El mood decide el ciclo y su velocidad; el tema de la
/// ventana decide el conjunto de arte (silueta oscura u clara).
/// Respeta <see cref="Accessibility.ReducedMotion" /> (frame final estático) y
/// nunca lanza: la mascota no puede romper la página.
/// </summary>
public sealed partial class CaoCat : UserControl
{
    public static readonly DependencyProperty MoodProperty =
        DependencyProperty.Register(nameof(Mood), typeof(string), typeof(CaoCat),
            new PropertyMetadata("Idle", (d, _) => ((CaoCat)d).ApplyMood()));

    public static readonly DependencyProperty ShowCaptionProperty =
        DependencyProperty.Register(nameof(ShowCaption), typeof(bool), typeof(CaoCat),
            new PropertyMetadata(true, (d, _) => ((CaoCat)d).ApplyCaptionVisibility()));

    // Los frames son inmutables y compartidos (Panel y Benchmark usan el mismo mood).
    private static readonly Dictionary<string, IReadOnlyList<BitmapImage?>> Cache = new();

    private DispatcherTimer? _timer;
    private IReadOnlyList<BitmapImage?> _frames = Array.Empty<BitmapImage?>();
    private int _index;
    private int _loaded;
    private string? _currentKey;
    private INotifyPropertyChanged? _moodSource;
    private Storyboard? _breathBoard;

    public CaoCat()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
        try
        {
            // Diagnóstico: si un frame no decodifica, queda en la salida de depuración
            // en lugar de fallar en silencio (la mascota nunca rompe la página).
            if (FrameImage is not null)
                FrameImage.ImageFailed += (_, e) => Debug.WriteLine($"[CaoCat] ImageFailed: {e.ErrorMessage}");
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
        AttachGlobalMood();
        ApplyCaptionVisibility();
        ApplyMood();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachGlobalMood();
        StopTimer();
        StopBreath();
    }

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
            AutomationProperties.SetName(this, $"Mascota CA-O, estado {mood}");
            var key = $"{CurrentThemeKey()}/{mood}";
            if (key == _currentKey && _loaded > 1)
            {
                // Mismo ciclo ya en marcha (p. ej. RenderHub re-aplica Warn en cada
                // render): no reiniciar el índice ni el timer, o la animación nunca
                // avanzaría del frame 0. Solo se reasegura el estado del timer.
                if (Accessibility.ReducedMotion.ShouldAnimate) { StartTimer(); RestartBreath(); }
                else { StopTimer(); StopBreath(); }
                return;
            }
            _currentKey = key;
            _index = 0;
            LoadFrames();
            if (Accessibility.ReducedMotion.ShouldAnimate && _loaded > 1)
            {
                StartTimer();
                RestartBreath();
            }
            else
            {
                StopTimer();
                StopBreath();
                ShowFrame(0);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] ApplyMood: {ex.Message}"); }
    }

    private string CurrentThemeKey() =>
        MascotFlipbook.NormalizeTheme(ActualTheme == ElementTheme.Dark ? "dark" : "light");

    private void LoadFrames()
    {
        var mood = MascotFlipbook.Normalize(Mood);
        var theme = CurrentThemeKey();
        var key = $"{theme}/{mood}";
        if (!Cache.TryGetValue(key, out var frames))
        {
            var list = new List<BitmapImage?>();
            for (var i = 0; i < MascotFlipbook.FrameCount(mood); i++)
                list.Add(TryLoadFrame(theme, mood, i));
            frames = list;
            Cache[key] = frames;
        }
        _frames = frames;
        _loaded = 0;
        foreach (var frame in _frames)
            if (frame is not null) _loaded++;
        if (_loaded == 0)
            Debug.WriteLine($"[CaoCat] sin frames para {key}: la mascota quedará estática.");
        ShowFrame(_index);
    }

    /// <summary>
    /// Carga un frame: primero vía <c>ms-appx</c> (contrato WinUI para Content) y,
    /// si falla, vía ruta absoluta de archivo. Nunca lanza: devuelve <c>null</c>.
    /// </summary>
    private static BitmapImage? TryLoadFrame(string theme, string mood, int index)
    {
        try
        {
            return new BitmapImage(new Uri(MascotFlipbook.AppxUri(theme, mood, index)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CaoCat] ms-appx {theme}/{mood}/f{index:00}: {ex.Message}");
        }
        try
        {
            var relative = MascotFlipbook.RelativePath(theme, mood, index)
                .Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(AppContext.BaseDirectory, relative);
            if (File.Exists(full))
                return new BitmapImage(new Uri(full));
            Debug.WriteLine($"[CaoCat] falta frame: {full}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CaoCat] archivo {theme}/{mood}/f{index:00}: {ex.Message}");
        }
        return null;
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
            if (_frames.Count == 0) { StopTimer(); return; }
            ShowFrame((_index + 1) % _frames.Count);
        }
        catch (Exception ex) { Debug.WriteLine($"[CaoCat] OnTick: {ex.Message}"); StopTimer(); }
    }

    private void ShowFrame(int index)
    {
        if (_frames.Count == 0) return;
        _index = ((index % _frames.Count) + _frames.Count) % _frames.Count;
        if (FrameImage is not null)
            FrameImage.Source = _frames[_index];
    }

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
            if (!Accessibility.ReducedMotion.ShouldAnimate) return;
            if (BreathTransform is null || FrameImage is null) return;
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
        _ => (3, 0.02, 1800),
    };
}
