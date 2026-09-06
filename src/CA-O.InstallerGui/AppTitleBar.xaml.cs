using Microsoft.UI.Xaml.Controls;

namespace CAO.InstallerGui;

public sealed partial class AppTitleBar : UserControl
{
    public AppTitleBar()
    {
        InitializeComponent();
        try { TitleText.Text = $"CA-O {CAO.Shared.Constants.BuildConstants.ProductVersion} Setup"; } catch { }
    }
}