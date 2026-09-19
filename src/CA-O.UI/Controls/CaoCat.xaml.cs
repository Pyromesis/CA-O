using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace CAO.UI.Controls;

/// <summary>
/// Mascota vectorial CaoCat: gato sentado con 5 moods animados (Idle/Working/Celebrate/Warn/Sleep).
/// Sin dependencias nuevas: solo XAML + Storyboard. Respeta <see cref="Accessibility.ReducedMotion" />.
/// </summary>
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
        try
        {
            if (CaptionText is null) return;
            var (caption, key) = CatMoodCatalog.Resolve(Mood);
            CaptionText.Text = caption;
            // Vía campos x:Name generados (verificada en compilación). Cada Storyboard
            // lleva además x:Key gemelo ("<AnimationKey>Board") por si hiciera falta
            // la vía fallback: (Storyboard)CatBody.Resources[key + "Board"].
            var boards = new Storyboard[] { IdleBoard, WorkingBoard, CelebrateBoard, WarnBoard, SleepBoard };
            foreach (var board in boards)
                board?.Stop();
            if (!Accessibility.ReducedMotion.ShouldAnimate) return;
            Storyboard run = key switch
            {
                "Working" => WorkingBoard,
                "Celebrate" => CelebrateBoard,
                "Warn" => WarnBoard,
                "Sleep" => SleepBoard,
                _ => IdleBoard,
            };
            run?.Begin();
        }
        catch { /* la mascota nunca rompe la página */ }
    }
}
