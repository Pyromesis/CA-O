using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>History viewer backed by %ProgramData%\CA-O\history.jsonl (spec 74) with filters and search.</summary>
public sealed partial class HistoryPage : Page
{
    private readonly ViewModels.HistoryViewModel _vm;

    public HistoryPage()
    {
        InitializeComponent();
        SourceNote.Text = $"Fuente: {CaOPaths.HistoryFile}";
        _vm = AppHost.Resolve<ViewModels.HistoryViewModel>();
        DataContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.HistoryViewModel.FilteredRows) or nameof(ViewModels.HistoryViewModel.IsEmpty) or nameof(ViewModels.HistoryViewModel.WarningMessage) or nameof(ViewModels.HistoryViewModel.CorruptedCount))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _vm.RefreshCommand.Execute(null);
        RenderVm();
    }

    private void RenderVm()
    {
        EmptyHistoryCard.Visibility = _vm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = _vm.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        HistoryList.ItemsSource = _vm.FilteredRows;
        WarningBar.IsOpen = !string.IsNullOrWhiteSpace(_vm.WarningMessage);
        WarningBar.Message = _vm.WarningMessage;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) { _vm.RefreshCommand.Execute(null); RenderVm(); }
    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: During InitializeComponent the SelectedItem event fires before _vm is assigned (IsSelected=True),
        // and FilterBox may still be null during XAML load – must not throw (spec §19 nunca cerrar).
        if (_vm is null) return;
        if (FilterBox?.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _vm.Filter = tag;
    }
    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.Search = SearchBox?.Text?.Trim() ?? "";
    }
}
