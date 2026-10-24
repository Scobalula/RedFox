using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Dialog that lists the assets of a source container that can be mounted, so they can be loaded without exporting them first.
/// </summary>
public partial class SourceBrowserWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceBrowserWindow"/> class.
    /// </summary>
    public SourceBrowserWindow()
    {
        InitializeComponent();
        Opened += (_, _) => FilterBox.Focus();

        // Tunnelled, as the grid takes Enter to move to the next row.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Initializes the browser with the assets of a source container.
    /// </summary>
    /// <param name="sourceName">The name of the source being browsed.</param>
    /// <param name="assets">The assets that can be mounted from the source.</param>
    public void Initialize(string sourceName, IReadOnlyList<Asset> assets)
    {
        DataContext = new SourceBrowserViewModel(sourceName, assets);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && AssetGrid.SelectedItems.Count > 0)
        {
            CloseSelection();
            e.Handled = true;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is SourceBrowserViewModel viewModel)
        {
            viewModel.SelectedCount = AssetGrid.SelectedItems.Count;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs args)
    {
        Close(null);
    }

    private void OnLoadClick(object? sender, RoutedEventArgs args)
    {
        CloseSelection();
    }

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs args)
    {
        CloseSelection();
    }

    private void CloseSelection()
    {
        Asset[] selection = [.. AssetGrid.SelectedItems.OfType<Asset>()];

        if (selection.Length > 0)
        {
            Close(selection);
        }
    }
}
