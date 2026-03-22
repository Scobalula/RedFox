using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

public partial class AssetListView : UserControl
{
    public AssetListView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not AssetListViewModel vm) return;
        if (sender is not ListBox listBox) return;

        vm.SelectedAssets.Clear();
        foreach (var item in listBox.SelectedItems)
        {
            if (item is AssetEntryModel model)
                vm.SelectedAssets.Add(model);
        }

        // Notify the parent MainWindowViewModel to refresh command states
        if (Parent?.Parent is { } grandParent)
        {
            // Walk up to find the MainWindow and notify
            var window = TopLevel.GetTopLevel(this);
            if (window?.DataContext is MainWindowViewModel mainVm)
                mainVm.NotifySelectionChanged();
        }
    }

    private async void OnAssetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source) return;

        // Walk up the visual tree to find the ListBoxItem that was double-clicked
        var listBoxItem = source.FindAncestorOfType<ListBoxItem>();
        if (listBoxItem?.DataContext is not AssetEntryModel model) return;

        var window = TopLevel.GetTopLevel(this);
        if (window?.DataContext is MainWindowViewModel mainVm)
            await mainVm.ExportSingleEntryAsync(model.Entry);
    }
}
