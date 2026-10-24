using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Main window view boundary for dialogs and platform pickers.
/// </summary>
public partial class MainWindow : Window
{
    private PreviewWindow? _previewWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Closed += OnWindowClosed;
        Opened += OnWindowOpened;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AssetListBox.ContainerPrepared += OnAssetContainerPrepared;
        AssetListBox.ContainerClearing += OnAssetContainerClearing;
    }

    /// <summary>
    /// Initializes the main window with an extraction configuration.
    /// </summary>
    /// <param name="config">The extraction UI configuration.</param>
    public void Initialize(GameExtractionConfig config)
    {
        if (DataContext is MainWindowViewModel previousViewModel)
        {
            UnsubscribeFromViewModel(previousViewModel);
        }

        MainWindowViewModel viewModel = new(config);
        DataContext = viewModel;
        AppIconImage.Source = LoadIcon(viewModel.SidebarIconPath);
        viewModel.Thumbnails.Renderer = ThumbnailRenderer;

        SubscribeToViewModel(viewModel);
        ApplyContentLayout(viewModel);
    }

    private static Bitmap? LoadIcon(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    private void SubscribeToViewModel(MainWindowViewModel viewModel)
    {
        viewModel.SettingsRequested += OnSettingsRequested;
        viewModel.AboutRequested += OnAboutRequested;
        viewModel.DonateRequested += OnDonateRequested;
        viewModel.SourceManagerRequested += OnSourceManagerRequested;
        viewModel.FileDialogRequested += OnFileDialogRequested;
        viewModel.FolderDialogRequested += OnFolderDialogRequested;
        viewModel.ProcessSelectionRequested += OnProcessSelectionRequested;
        viewModel.SourceBrowserRequested += OnSourceBrowserRequested;
        viewModel.PreviewRequested += OnPreviewRequested;
        viewModel.PreviewWindowCloseRequested += OnPreviewWindowCloseRequested;
        viewModel.AssetSelectionRestoreRequested += OnAssetSelectionRestoreRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnsubscribeFromViewModel(MainWindowViewModel viewModel)
    {
        viewModel.SettingsRequested -= OnSettingsRequested;
        viewModel.AboutRequested -= OnAboutRequested;
        viewModel.DonateRequested -= OnDonateRequested;
        viewModel.SourceManagerRequested -= OnSourceManagerRequested;
        viewModel.FileDialogRequested -= OnFileDialogRequested;
        viewModel.FolderDialogRequested -= OnFolderDialogRequested;
        viewModel.ProcessSelectionRequested -= OnProcessSelectionRequested;
        viewModel.SourceBrowserRequested -= OnSourceBrowserRequested;
        viewModel.PreviewRequested -= OnPreviewRequested;
        viewModel.PreviewWindowCloseRequested -= OnPreviewWindowCloseRequested;
        viewModel.AssetSelectionRestoreRequested -= OnAssetSelectionRestoreRequested;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is MainWindowViewModel viewModel && args.PropertyName is nameof(MainWindowViewModel.IsPreviewPanelVisible) or nameof(MainWindowViewModel.IsBrowserVisible))
        {
            CapturePreviewPanelRatio(viewModel);
            ApplyContentLayout(viewModel);
        }
    }

    private void CapturePreviewPanelRatio(MainWindowViewModel viewModel)
    {
        double browserWidth = ContentGrid.ColumnDefinitions[0].ActualWidth;
        double previewWidth = ContentGrid.ColumnDefinitions[2].ActualWidth;

        // Column widths lag a layout pass behind visibility changes, so only a visible splitter with both panels laid out reflects the user's split.
        if (PreviewSplitter.IsVisible && browserWidth > 0 && previewWidth > 0)
        {
            viewModel.PreviewSettings.PreviewPanelRatio = Math.Clamp(previewWidth / (browserWidth + previewWidth), 0.1, 0.9);
        }
    }

    private void ApplyContentLayout(MainWindowViewModel viewModel)
    {
        ColumnDefinition browserColumn = ContentGrid.ColumnDefinitions[0];
        ColumnDefinition previewColumn = ContentGrid.ColumnDefinitions[2];
        bool isSplit = viewModel.IsBrowserVisible && viewModel.IsPreviewPanelVisible;
        double ratio = viewModel.PreviewSettings.PreviewPanelRatio;

        browserColumn.Width = viewModel.IsBrowserVisible ? new GridLength(isSplit ? 1 - ratio : 1, GridUnitType.Star) : new GridLength(0);
        previewColumn.Width = viewModel.IsPreviewPanelVisible ? new GridLength(isSplit ? ratio : 1, GridUnitType.Star) : new GridLength(0);
        browserColumn.MinWidth = isSplit ? 280 : 0;
        previewColumn.MinWidth = isSplit ? 320 : 0;
        PreviewSplitter.IsVisible = isSplit;
    }

    private void OnAssetSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAssets(listBox.SelectedItems?.OfType<AssetRowViewModel>() ?? Enumerable.Empty<AssetRowViewModel>());
        }
    }

    private void OnAssetContainerPrepared(object? sender, ContainerPreparedEventArgs args)
    {
        if (DataContext is MainWindowViewModel { PreviewSettings.ShowAssetGrid: true } viewModel && args.Container.DataContext is AssetRowViewModel row)
        {
            viewModel.Thumbnails.Request(row);
        }
    }

    private void OnAssetContainerClearing(object? sender, ContainerClearingEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel && args.Container.DataContext is AssetRowViewModel row)
        {
            viewModel.Thumbnails.Cancel(row);
        }
    }

    private void OnAssetSelectionRestoreRequested(IReadOnlyList<int> indexes)
    {
        AssetListBox.Selection.BeginBatchUpdate();

        try
        {
            AssetListBox.Selection.Clear();

            foreach (int index in indexes)
            {
                AssetListBox.Selection.Select(index);
            }
        }
        finally
        {
            AssetListBox.Selection.EndBatchUpdate();
        }

        if (indexes.Count > 0)
        {
            AssetListBox.ScrollIntoView(indexes[^1]);
        }
    }

    private async void OnAssetDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel && FindRowItem<AssetRowViewModel>(args.Source) is { } row)
        {
            await viewModel.ExportAssetAsync(row);
        }
    }

    private static T? FindRowItem<T>(object? source) where T : class
    {
        return source is Visual visual
            ? visual.FindAncestorOfType<ListBoxItem>()?.DataContext as T
                ?? (visual as Control)?.DataContext as T
            : null;
    }

    private async Task<IReadOnlyList<string>> OnFileDialogRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return [];
        }

        IReadOnlyList<IStorageFile> result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select file(s) to load",
            AllowMultiple = true,
            FileTypeFilter = ParseFileFilter(viewModel.Config.FileFilter),
        }).ConfigureAwait(true);

        return [.. result.Select(file => file.TryGetLocalPath()).OfType<string>()];
    }

    private async Task<string?> OnFolderDialogRequested()
    {
        IReadOnlyList<IStorageFolder> result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder to load",
            AllowMultiple = false,
        }).ConfigureAwait(true);

        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    private async void OnSettingsRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        SettingsWindow settingsWindow = new();
        settingsWindow.Initialize(viewModel.Config.Settings, viewModel.Config.SettingDefinitions, viewModel.Config.AppName, openPlugins: () => OpenPluginsWindow(settingsWindow, viewModel));
        await settingsWindow.ShowDialog(this);
        viewModel.Config.PreviewSettings.LoadFrom(viewModel.Config.Settings);
        GameExtractionLogging.ApplySettings(viewModel.Config.Settings);
    }

    private static void OpenPluginsWindow(Window owner, MainWindowViewModel viewModel)
    {
        PluginsWindow window = new();
        window.Initialize(viewModel.Plugins);
        window.ShowDialog(owner);
    }

    private void OnAboutRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        AboutWindow aboutWindow = new();
        aboutWindow.Initialize(viewModel.Config);
        aboutWindow.ShowDialog(this);
    }

    private void OnDonateRequested(bool isFirstLaunch)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        DonateWindow donateWindow = new();
        donateWindow.Initialize(viewModel.Config, isFirstLaunch);
        donateWindow.ShowDialog(this);
    }

    private void OnWindowOpened(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PromptDonationOnFirstLaunch();
        }
    }

    private void OnSourceManagerRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        SourceManagerWindow managerWindow = new();
        managerWindow.Initialize(viewModel);
        managerWindow.ShowDialog(this);
    }

    private async Task<ProcessSelectionResult?> OnProcessSelectionRequested(IReadOnlyList<ProcessCandidateViewModel> processes)
    {
        ProcessSelectionWindow processWindow = new();
        processWindow.Initialize(processes);
        return await processWindow.ShowDialog<ProcessSelectionResult?>(this).ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<Asset>?> OnSourceBrowserRequested(string sourceName, IReadOnlyList<Asset> assets)
    {
        SourceBrowserWindow browserWindow = new();
        browserWindow.Initialize(sourceName, assets);
        return await browserWindow.ShowDialog<IReadOnlyList<Asset>?>(this).ConfigureAwait(true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        string[] paths = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>()];

        if (paths.Length > 0 && DataContext is MainWindowViewModel { IsLoading: false } viewModel)
        {
            await viewModel.LoadPathsAsync(paths);
        }
    }

    private void OnPreviewRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (_previewWindow is null)
        {
            // Release the docked preview before the window binds to it; a view can only have one host.
            viewModel.SetPreviewWindowOpen(true);
            _previewWindow = new PreviewWindow { DataContext = viewModel.Preview };
            _previewWindow.Closed += OnPreviewWindowClosed;
            _previewWindow.Show(this);
        }

        _previewWindow.Activate();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.P && e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && DataContext is MainWindowViewModel mainViewModel)
        {
            mainViewModel.PreviewSelectedCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Source is TextBox || DataContext is not MainWindowViewModel { IsPreviewActive: true, Preview.Content: IPreviewKeyHandler handler })
        {
            return;
        }

        if (handler.HandleKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    private void OnPreviewWindowCloseRequested()
    {
        _previewWindow?.Close();
    }

    private void OnPreviewWindowClosed(object? sender, EventArgs e)
    {
        if (sender is PreviewWindow window)
        {
            window.DataContext = null;
        }

        _previewWindow = null;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetPreviewWindowOpen(false);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_previewWindow is not null)
        {
            // Closing during shutdown must not hand the preview back to the main window.
            _previewWindow.Closed -= OnPreviewWindowClosed;
            _previewWindow.Close();
            _previewWindow = null;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            UnsubscribeFromViewModel(viewModel);
            CapturePreviewPanelRatio(viewModel);
            viewModel.SaveLayout();
            viewModel.Dispose();
        }
    }

    private static IReadOnlyList<FilePickerFileType> ParseFileFilter(string filter)
    {
        List<FilePickerFileType> types = [];
        string[] parts = filter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index + 1 < parts.Length; index += 2)
        {
            List<string> patterns = [.. parts[index + 1].Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            if (patterns.Count > 0)
            {
                types.Add(new FilePickerFileType(parts[index]) { Patterns = patterns });
            }
        }

        if (types.Count == 0)
        {
            types.Add(new FilePickerFileType("All Files") { Patterns = ["*.*"] });
        }

        return types;
    }
}
