using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides sidebar display state and commands.
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SidebarViewModel"/> class.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    public SidebarViewModel(GameExtractionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Title = config.SidebarTitle;
        Description = config.Description;
        CanLoadFiles = config.SupportsFileSources;
        CanLoadDirectories = config.SupportsDirectorySources;
        CanLoadProcess = config.SupportsProcessSources;
        Icon = LoadIcon(config.SidebarIconPath ?? config.IconPath);
    }

    /// <summary>
    /// Gets the sidebar title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the sidebar description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the loaded sidebar icon.
    /// </summary>
    public Bitmap? Icon { get; }

    /// <summary>
    /// Gets a value indicating whether an icon is available to display.
    /// </summary>
    public bool HasIcon => Icon is not null;

    /// <summary>
    /// Gets a value indicating whether file sources can be loaded.
    /// </summary>
    public bool CanLoadFiles { get; }

    /// <summary>
    /// Gets a value indicating whether directory sources can be loaded.
    /// </summary>
    public bool CanLoadDirectories { get; }

    /// <summary>
    /// Gets a value indicating whether process sources can be loaded.
    /// </summary>
    public bool CanLoadProcess { get; }

    /// <summary>
    /// Gets a value indicating whether any source loader is available.
    /// </summary>
    public bool CanLoadAnySource => CanLoadFiles || CanLoadDirectories || CanLoadProcess;

    /// <summary>
    /// Gets or sets the loaded source count.
    /// </summary>
    [ObservableProperty]
    public partial int SourceCount { get; set; }

    /// <summary>
    /// Gets the manage sources button text.
    /// </summary>
    public string SourceCountDisplay => SourceCount > 0
        ? $"Manage Sources ({SourceCount})"
        : "Manage Sources";

    /// <summary>
    /// Gets or sets the file load command.
    /// </summary>
    public IAsyncRelayCommand? LoadSourceCommand { get; set; }

    /// <summary>
    /// Gets or sets the directory load command.
    /// </summary>
    public IAsyncRelayCommand? LoadDirectoryCommand { get; set; }

    /// <summary>
    /// Gets or sets the process load command.
    /// </summary>
    public IAsyncRelayCommand? LoadProcessCommand { get; set; }

    /// <summary>
    /// Gets or sets the manage sources command.
    /// </summary>
    public IRelayCommand? ManageSourcesCommand { get; set; }

    /// <summary>
    /// Gets or sets the clear all command.
    /// </summary>
    public IAsyncRelayCommand? ClearAllCommand { get; set; }

    /// <summary>
    /// Gets or sets the export all command.
    /// </summary>
    public IAsyncRelayCommand? ExportAllCommand { get; set; }

    /// <summary>
    /// Gets or sets the export selected command.
    /// </summary>
    public IAsyncRelayCommand? ExportSelectedCommand { get; set; }

    /// <summary>
    /// Gets or sets the open settings command.
    /// </summary>
    public IRelayCommand? OpenSettingsCommand { get; set; }

    /// <summary>
    /// Gets or sets the open about command.
    /// </summary>
    public IRelayCommand? OpenAboutCommand { get; set; }

    partial void OnSourceCountChanged(int value)
    {
        OnPropertyChanged(nameof(SourceCountDisplay));
    }

    private static Bitmap? LoadIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(fullPath);
        }
        catch
        {
            return null;
        }
    }
}
