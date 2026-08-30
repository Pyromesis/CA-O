using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.Helpers;

public static class LocalizationHelper
{
    // Build reverse map from Spanish literal -> key once
    private static readonly Dictionary<string, string> SpanishValueToKey = BuildMap();

    private static Dictionary<string, string> BuildMap()
    {
        // Access via reflection to get Spanish dictionary
        var field = typeof(Localizer).GetField("Spanish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field?.GetValue(null) is Dictionary<string, string> dict)
        {
            var rev = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in dict) rev[kv.Value] = kv.Key;
            return rev;
        }
        return new();
    }

    public static void LocalizeTree(DependencyObject root)
    {
        if (root == null) return;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            TryLocalizeElement(child);
            LocalizeTree(child);
        }
        // Also check root itself if it's a TextBlock/Button etc (in case root is page content not counted)
        TryLocalizeElement(root);
    }

    private static void TryLocalizeElement(DependencyObject obj)
    {
        switch (obj)
        {
            case TextBlock tb when !string.IsNullOrEmpty(tb.Text) && SpanishValueToKey.TryGetValue(tb.Text, out var key):
                // Skip technical identifiers (e.g., pure CPU/GPU single word? those are also keys but we want them translated — keep)
                tb.Text = Localizer.Get(key);
                break;
            case Button btn when btn.Content is string s && SpanishValueToKey.TryGetValue(s, out var k):
                btn.Content = Localizer.Get(k);
                break;
            case HyperlinkButton hb when hb.Content is string hs && SpanishValueToKey.TryGetValue(hs, out var hk):
                hb.Content = Localizer.Get(hk);
                break;
            case MenuFlyoutItem mfi when mfi.Text != null && SpanishValueToKey.TryGetValue(mfi.Text, out var mk):
                mfi.Text = Localizer.Get(mk);
                break;
            case ComboBoxItem cbi when cbi.Content is string cs && SpanishValueToKey.TryGetValue(cs, out var ck):
                cbi.Content = Localizer.Get(ck);
                break;
            case NavigationViewItem nvi when nvi.Content is string ns && SpanishValueToKey.TryGetValue(ns, out var nk):
                nvi.Content = Localizer.Get(nk);
                break;
        }
        // ToolTip
        var tip = ToolTipService.GetToolTip(obj) as string;
        if (!string.IsNullOrEmpty(tip) && SpanishValueToKey.TryGetValue(tip, out var tk))
            ToolTipService.SetToolTip(obj, Localizer.Get(tk));
    }
}
