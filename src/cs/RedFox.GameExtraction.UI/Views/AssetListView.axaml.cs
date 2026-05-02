using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Asset list view boundary for DataGrid selection synchronization.
/// </summary>
public partial class AssetListView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetListView"/> class.
    /// </summary>
    public AssetListView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is not AssetListViewModel viewModel || sender is not DataGrid dataGrid)
        {
            return;
        }

        viewModel.SelectedAssets.Clear();
        foreach (object? item in dataGrid.SelectedItems)
        {
            if (item is AssetRowViewModel row)
            {
                viewModel.SelectedAssets.Add(row);
            }
        }

        NotifyMainViewModel();
    }

    private async void OnAssetDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is not Visual source)
        {
            return;
        }

        AssetRowViewModel? row = source.FindAncestorOfType<DataGridRow>()?.DataContext as AssetRowViewModel;
        if (row is null)
        {
            row = (source as Control)?.DataContext as AssetRowViewModel;
        }

        if (row is null)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.DataContext is MainWindowViewModel mainViewModel)
        {
            await mainViewModel.ExportSingleAssetAsync(row).ConfigureAwait(true);
        }
    }

    private void NotifyMainViewModel()
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.DataContext is MainWindowViewModel mainViewModel)
        {
            mainViewModel.NotifySelectionChanged();
        }
    }
}
