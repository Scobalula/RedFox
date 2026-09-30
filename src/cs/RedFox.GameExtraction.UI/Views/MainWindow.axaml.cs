using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        SidebarIconImage.Source = LoadIcon(viewModel.SidebarIconPath);

        SubscribeToViewModel(viewModel);
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
        viewModel.SourceManagerRequested += OnSourceManagerRequested;
        viewModel.FileDialogRequested += OnFileDialogRequested;
        viewModel.FolderDialogRequested += OnFolderDialogRequested;
        viewModel.ProcessSelectionRequested += OnProcessSelectionRequested;
        viewModel.PreviewRequested += OnPreviewRequested;
    }

    private void UnsubscribeFromViewModel(MainWindowViewModel viewModel)
    {
        viewModel.SettingsRequested -= OnSettingsRequested;
        viewModel.AboutRequested -= OnAboutRequested;
        viewModel.SourceManagerRequested -= OnSourceManagerRequested;
        viewModel.FileDialogRequested -= OnFileDialogRequested;
        viewModel.FolderDialogRequested -= OnFolderDialogRequested;
        viewModel.ProcessSelectionRequested -= OnProcessSelectionRequested;
        viewModel.PreviewRequested -= OnPreviewRequested;
    }

    private void OnAssetSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAssets(listBox.SelectedItems?.OfType<AssetRowViewModel>() ?? Enumerable.Empty<AssetRowViewModel>());
        }
    }

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel && FindRowItem<AssetRowViewModel>(args.Source) is { } row)
        {
            viewModel.OpenPreview(row);
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

    private void OnSettingsRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        SettingsWindow settingsWindow = new();
        settingsWindow.Initialize(viewModel.Config.Settings, viewModel.Config.SettingDefinitions, viewModel.Config.AppName, openPlugins: () => OpenPluginsWindow(settingsWindow, viewModel));
        settingsWindow.ShowDialog(this);
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

    private void OnPreviewRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (_previewWindow is null)
        {
            _previewWindow = new PreviewWindow { DataContext = viewModel.Preview };
            _previewWindow.Closed += OnPreviewWindowClosed;
            _previewWindow.Show(this);
            viewModel.SetPreviewWindowOpen(true);
        }

        _previewWindow.Activate();
    }

    private void OnPreviewWindowClosed(object? sender, EventArgs e)
    {
        _previewWindow = null;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetPreviewWindowOpen(false);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _previewWindow?.Close();
        if (DataContext is MainWindowViewModel viewModel)
        {
            UnsubscribeFromViewModel(viewModel);
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
