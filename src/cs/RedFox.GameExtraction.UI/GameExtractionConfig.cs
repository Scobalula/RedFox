using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Configures a GameExtraction Avalonia application.
/// </summary>
public sealed class GameExtractionConfig
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOptions =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the factory used to create the asset manager that powers the UI.
    /// </summary>
    public required Func<AssetManager> AssetManagerFactory { get; init; }

    /// <summary>
    /// Gets the application window title.
    /// </summary>
    public string WindowTitle { get; init; } = "RedFox Game Extraction";

    /// <summary>
    /// Gets the short application title shown in the sidebar.
    /// </summary>
    public string SidebarTitle { get; init; } = "Game Extraction";

    /// <summary>
    /// Gets the description shown in the sidebar.
    /// </summary>
    public string Description { get; init; } = "Mount sources and export discovered assets.";

    /// <summary>
    /// Gets the application name used for persisted settings.
    /// </summary>
    public string AppName { get; init; } = "RedFox";

    /// <summary>
    /// Gets the optional path to a window icon image.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Gets the optional path to an icon displayed next to the sidebar title.
    /// </summary>
    public string? SidebarIconPath { get; init; }

    /// <summary>
    /// Gets the optional font family name used by the application.
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>
    /// Gets the version string displayed in the About window.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the accent color hex value used by the theme.
    /// </summary>
    public string AccentColor { get; init; } = "#E53935";

    /// <summary>
    /// Gets the file picker filter string, using the WinForms-style format "Label|*.ext|All Files|*.*".
    /// </summary>
    public string FileFilter { get; init; } = "All Files|*.*";

    /// <summary>
    /// Gets a value indicating whether file-backed sources can be loaded.
    /// </summary>
    public bool SupportsFileSources { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether directory-backed sources can be loaded.
    /// </summary>
    public bool SupportsDirectorySources { get; init; }

    /// <summary>
    /// Gets a value indicating whether process-backed sources can be loaded.
    /// </summary>
    public bool SupportsProcessSources { get; init; }

    /// <summary>
    /// Gets the target process identifier used by the Load Process command.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// Gets the target process name used by the Load Process command.
    /// </summary>
    public string? ProcessName { get; init; }

    /// <summary>
    /// Gets source options passed to each mount request.
    /// </summary>
    public IReadOnlyDictionary<string, object?> SourceOptions { get; init; } = EmptyOptions;

    /// <summary>
    /// Gets optional metadata names that consumers expect to search or display.
    /// </summary>
    public IReadOnlyList<string> MetadataColumns { get; init; } = [];

    /// <summary>
    /// Gets the mutable export settings used by the application.
    /// </summary>
    public GameExtractionSettings Settings { get; init; } = new();

    /// <summary>
    /// Gets the optional About window configuration with description and links.
    /// </summary>
    public AboutConfig? About { get; init; }
}

/// <summary>
/// Configures the About window content and links.
/// </summary>
public sealed class AboutConfig
{
    /// <summary>
    /// Gets the description text shown below the title.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the links displayed as buttons in the About window.
    /// </summary>
    public IReadOnlyList<AboutLink>? Links { get; init; }
}

/// <summary>
/// Describes a link button displayed in the About window.
/// </summary>
public sealed class AboutLink
{
    /// <summary>
    /// Gets the button label text.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the URL opened when the button is clicked.
    /// </summary>
    public required string Url { get; init; }
}
