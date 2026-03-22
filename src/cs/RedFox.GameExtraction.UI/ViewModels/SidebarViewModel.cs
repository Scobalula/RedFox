using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class SidebarViewModel(GameExtractionConfig config) : ObservableObject
{
    public string Title { get; } = config.GameSource.Title;
    public string Description { get; } = config.GameSource.Description;
    public GameCapabilities Capabilities { get; } = config.GameSource.Capabilities;

    /// <summary>Loaded icon bitmap for the sidebar.</summary>
    public Bitmap? Icon { get; } = LoadIcon(config.SidebarIconPath ?? config.IconPath);

    /// <summary>Whether an icon is available to display.</summary>
    public bool HasIcon => Icon is not null;

    [ObservableProperty]
    public partial int SourceCount { get; set; }

    public string SourceCountDisplay => SourceCount > 0
        ? $"Manage Sources ({SourceCount})"
        : "Manage Sources";

    partial void OnSourceCountChanged(int value) => OnPropertyChanged(nameof(SourceCountDisplay));

    // Commands wired by MainWindowViewModel
    public IAsyncRelayCommand? LoadSourceCommand { get; set; }
    public IAsyncRelayCommand? LoadGameCommand { get; set; }
    public IRelayCommand? ManageSourcesCommand { get; set; }
    public IRelayCommand? ClearAllCommand { get; set; }
    public IAsyncRelayCommand? ExportAllCommand { get; set; }
    public IAsyncRelayCommand? ExportSelectedCommand { get; set; }
    public IRelayCommand? OpenPreviewCommand { get; set; }
    public IRelayCommand? OpenSettingsCommand { get; set; }
    public IRelayCommand? OpenAboutCommand { get; set; }

    private static Bitmap? LoadIcon(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
            return null;

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
