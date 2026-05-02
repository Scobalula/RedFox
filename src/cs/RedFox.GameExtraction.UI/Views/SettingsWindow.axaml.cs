using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Settings dialog view boundary for export configuration.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the settings dialog.
    /// </summary>
    /// <param name="settings">The settings to edit.</param>
    /// <param name="appName">The application name used for persistence.</param>
    public void Initialize(GameExtractionSettings settings, string appName)
    {
        SettingsWindowViewModel viewModel = new(settings, appName);
        DataContext = viewModel;

        viewModel.BrowseOutputDirectoryRequested += OnBrowseOutputDirectoryRequested;
        viewModel.SaveRequested += () => Close();
        viewModel.CancelRequested += () => Close();
    }

    private async Task<string?> OnBrowseOutputDirectoryRequested(string? currentPath)
    {
        string startPath = string.IsNullOrWhiteSpace(currentPath)
            ? GameExtractionSettings.GetDefaultOutputDirectory()
            : currentPath;

        IStorageFolder? startFolder = Directory.Exists(startPath)
            ? await StorageProvider.TryGetFolderFromPathAsync(startPath).ConfigureAwait(true)
            : null;

        IReadOnlyList<IStorageFolder> result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select export folder",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
        }).ConfigureAwait(true);

        return result.FirstOrDefault()?.TryGetLocalPath();
    }
}
