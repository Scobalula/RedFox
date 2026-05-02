using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides editable export settings for the settings window.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly string _appName;
    private readonly GameExtractionSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindowViewModel"/> class.
    /// </summary>
    /// <param name="settings">The settings object to edit.</param>
    /// <param name="appName">The application name used for persistence.</param>
    public SettingsWindowViewModel(GameExtractionSettings settings, string appName)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        _appName = appName;

        OutputDirectory = settings.OutputDirectory;
        Overwrite = settings.Overwrite;
        ExportReferences = settings.ExportReferences;
        PreserveDirectoryStructure = settings.PreserveDirectoryStructure;
    }

    /// <summary>
    /// Gets or sets the export output directory.
    /// </summary>
    [ObservableProperty]
    public partial string OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether existing files are overwritten during export.
    /// </summary>
    [ObservableProperty]
    public partial bool Overwrite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether referenced assets are exported recursively.
    /// </summary>
    [ObservableProperty]
    public partial bool ExportReferences { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether asset directory structure is preserved.
    /// </summary>
    [ObservableProperty]
    public partial bool PreserveDirectoryStructure { get; set; }

    /// <summary>
    /// Raised when the view model should be saved and closed.
    /// </summary>
    public event Action? SaveRequested;

    /// <summary>
    /// Raised when the settings window should close without saving.
    /// </summary>
    public event Action? CancelRequested;

    /// <summary>
    /// Raised when the view should browse for an output directory.
    /// </summary>
    public event Func<string?, Task<string?>>? BrowseOutputDirectoryRequested;

    /// <summary>
    /// Saves the current settings.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        _settings.OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory)
            ? GameExtractionSettings.GetDefaultOutputDirectory()
            : OutputDirectory.Trim();
        _settings.Overwrite = Overwrite;
        _settings.ExportReferences = ExportReferences;
        _settings.PreserveDirectoryStructure = PreserveDirectoryStructure;

        string path = GameExtractionSettings.GetDefaultSettingsPath(_appName);
        _settings.Save(path);
        SaveRequested?.Invoke();
    }

    /// <summary>
    /// Cancels editing.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        CancelRequested?.Invoke();
    }

    /// <summary>
    /// Opens a directory picker for the output directory.
    /// </summary>
    /// <returns>A task that completes when browsing finishes.</returns>
    [RelayCommand]
    public async Task BrowseOutputDirectoryAsync()
    {
        if (BrowseOutputDirectoryRequested is null)
        {
            return;
        }

        string? selectedPath = await BrowseOutputDirectoryRequested.Invoke(OutputDirectory).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            OutputDirectory = selectedPath;
        }
    }
}
