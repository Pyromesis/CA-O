using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Shared;

namespace CAO.UI.Controls;

/// <summary>RiskBadge + ImpactBadge reutilizables (§55-56, §162) — semantic tokens, sin hardcoded colors.</summary>
public sealed class RiskBadge : UserControl
{
    private readonly Border _badge;
    private readonly TextBlock _text;

    public RiskBadge()
    {
        _text = new TextBlock { FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _badge = new Border
        {
            Style = (Style)Application.Current.Resources["CaoBadgeStyle"],
            Child = _text
        };
        Content = _badge;
    }

    public void Bind(RiskLevel risk)
    {
        _text.Text = risk.ToString();
        _badge.Background = risk switch
        {
            RiskLevel.Safe => (Brush)Application.Current.Resources["CaoSuccessBrush"],
            RiskLevel.Low => (Brush)Application.Current.Resources["CaoInfoBrush"],
            RiskLevel.Moderate => (Brush)Application.Current.Resources["CaoWarningBrush"],
            RiskLevel.High => (Brush)Application.Current.Resources["CaoDangerBrush"],
            RiskLevel.Critical => (Brush)Application.Current.Resources["CaoCriticalBrush"],
            _ => (Brush)Application.Current.Resources["CaoNeutralBrush"],
        };
    }
}

public sealed class ImpactBadge : UserControl
{
    private readonly Border _badge;
    private readonly TextBlock _text;

    public ImpactBadge()
    {
        _text = new TextBlock { FontSize = 11 };
        _badge = new Border
        {
            Style = (Style)Application.Current.Resources["CaoBadgeStyle"],
            Child = _text
        };
        Content = _badge;
    }

    public void Bind(PerformanceImpact impact, EvidenceLevel evidence)
    {
        _text.Text = $"{impact} · {evidence}";
        _badge.Background = evidence is EvidenceLevel.Official or EvidenceLevel.Benchmark
            ? (Brush)Application.Current.Resources["CaoSuccessBrush"]
            : evidence is EvidenceLevel.Unknown ? (Brush)Application.Current.Resources["CaoNeutralBrush"]
            : (Brush)Application.Current.Resources["CaoInfoBrush"];
    }
}

public sealed class ScoreRing : UserControl
{
    private readonly TextBlock _value;
    private readonly Border _ring;

    public ScoreRing()
    {
        _value = new TextBlock { FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        _ring = new Border
        {
            CornerRadius = new CornerRadius(40),
            Width = 80, Height = 80,
            BorderThickness = new Thickness(4),
            Child = _value
        };
        Content = _ring;
    }

    public void SetScore(int? score)
    {
        if (score is null) { _value.Text = "—"; _ring.BorderBrush = (Brush)Application.Current.Resources["CaoNeutralBrush"]; return; }
        _value.Text = score.Value.ToString();
        _ring.BorderBrush = score.Value switch
        {
            >= 85 => (Brush)Application.Current.Resources["CaoSuccessBrush"],
            >= 65 => (Brush)Application.Current.Resources["CaoInfoBrush"],
            >= 40 => (Brush)Application.Current.Resources["CaoWarningBrush"],
            _ => (Brush)Application.Current.Resources["CaoDangerBrush"],
        };
    }
}
