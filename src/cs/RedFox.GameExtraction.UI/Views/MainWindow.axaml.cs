using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly PreviewWindowHost _previewWindowHost = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _previewWindowHost.Closed += OnPreviewWindowClosed;
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

        SubscribeToViewModel(viewModel);
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
        if (DataContext is not MainWindowViewModel viewModel || sender is not DataGrid dataGrid)
        {
            return;
        }

        viewModel.SetSelectedAssets(dataGrid.SelectedItems.OfType<AssetRowViewModel>());
    }

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (DataContext is not MainWindowViewModel viewModel || args.Source is not Visual source)
        {
            return;
        }

        AssetRowViewModel? row = source.FindAncestorOfType<DataGridRow>()?.DataContext as AssetRowViewModel;
        if (row is null)
        {
            row = (source as Control)?.DataContext as AssetRowViewModel;
        }

        if (row is not null)
        {
            viewModel.OpenPreview(row);
        }
    }

    private async Task<IReadOnlyList<string>> OnFileDialogRequested()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return [];
        }

        IReadOnlyList<FilePickerFileType> filters = ParseFileFilter(viewModel.Config.FileFilter);
        IReadOnlyList<IStorageFile> result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select file(s) to load",
            AllowMultiple = true,
            FileTypeFilter = filters,
        }).ConfigureAwait(true);

        return [.. result
            .Select(file => file.TryGetLocalPath())
            .Where(path => path is not null)
            .Cast<string>()];
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
        settingsWindow.Initialize(viewModel.Config.Settings, viewModel.Config.SettingDefinitions, viewModel.Config.AppName);
        settingsWindow.ShowDialog(this);
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
        if (DataContext is MainWindowViewModel viewModel)
        {
            _previewWindowHost.Show(this, viewModel);
            viewModel.SetPreviewWindowOpen(true);
        }
    }

    private void OnPreviewWindowClosed()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetPreviewWindowOpen(false);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            UnsubscribeFromViewModel(viewModel);
            viewModel.Dispose();
        }

        _previewWindowHost.Dispose();
    }

    private static IReadOnlyList<FilePickerFileType> ParseFileFilter(string filter)
    {
        List<FilePickerFileType> types = [];
        string[] parts = filter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index + 1 < parts.Length; index += 2)
        {
            string name = parts[index];
            List<string> patterns = [.. parts[index + 1]
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

            if (patterns.Count > 0)
            {
                types.Add(new FilePickerFileType(name) { Patterns = patterns });
            }
        }

        if (types.Count == 0)
        {
            types.Add(new FilePickerFileType("All Files") { Patterns = ["*.*"] });
        }

        return types;
    }
}
