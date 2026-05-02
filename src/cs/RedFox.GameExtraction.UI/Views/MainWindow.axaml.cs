using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Main window view boundary for dialogs and platform pickers.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the main window with an extraction configuration.
    /// </summary>
    /// <param name="config">The extraction UI configuration.</param>
    public void Initialize(GameExtractionConfig config)
    {
        MainWindowViewModel viewModel = new(config);
        DataContext = viewModel;

        viewModel.SettingsRequested += OnSettingsRequested;
        viewModel.AboutRequested += OnAboutRequested;
        viewModel.SourceManagerRequested += OnSourceManagerRequested;
        viewModel.FileDialogRequested += OnFileDialogRequested;
        viewModel.FolderDialogRequested += OnFolderDialogRequested;
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

        return result
            .Select(file => file.TryGetLocalPath())
            .Where(path => path is not null)
            .Cast<string>()
            .ToList();
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
        settingsWindow.Initialize(viewModel.Config.Settings, viewModel.Config.AppName);
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

    private static IReadOnlyList<FilePickerFileType> ParseFileFilter(string filter)
    {
        List<FilePickerFileType> types = [];
        string[] parts = filter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index + 1 < parts.Length; index += 2)
        {
            string name = parts[index];
            List<string> patterns = parts[index + 1]
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

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
