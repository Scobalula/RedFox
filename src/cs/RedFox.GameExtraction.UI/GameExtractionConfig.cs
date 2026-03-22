using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Configuration object passed to <see cref="GameExtractionApp.Run"/> to bootstrap the application.
/// Consumers create this in their Main() to wire up game-specific implementations.
/// </summary>
public class GameExtractionConfig
{
    /// <summary>The game source implementation for loading assets.</summary>
    public required IGameSource GameSource { get; init; }

    /// <summary>The exporter implementation for saving assets.</summary>
    public required IAssetExporter Exporter { get; init; }

    /// <summary>Optional preview handler. If null, preview features are hidden.</summary>
    public IPreviewHandler? PreviewHandler { get; init; }

    /// <summary>Application settings instance. Must be a concrete subclass of SettingsBase.</summary>
    public required SettingsBase Settings { get; init; }

    /// <summary>Application window title.</summary>
    public string WindowTitle { get; init; } = "RedFox Game Extraction";

    /// <summary>Application name used for settings file path.</summary>
    public string AppName { get; init; } = "RedFox";

    /// <summary>Optional path to a window icon image.</summary>
    public string? IconPath { get; init; }

    /// <summary>Optional path to an icon displayed next to the title in the sidebar. Falls back to <see cref="IconPath"/> if not set.</summary>
    public string? SidebarIconPath { get; init; }

    /// <summary>Optional font family name (e.g., "Segoe UI", "JetBrains Mono"). If null, the default Inter font is used.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Version string displayed in About.</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>Optional accent color hex (e.g., "#FF5722"). Defaults to a red accent.</summary>
    public string AccentColor { get; init; } = "#E53935";

    /// <summary>Optional About window configuration with description and links.</summary>
    public AboutConfig? About { get; init; }
}

/// <summary>
/// Configuration for the About window content and links.
/// </summary>
public class AboutConfig
{
    /// <summary>Description text shown below the title.</summary>
    public string? Description { get; init; }

    /// <summary>Links displayed as buttons in the About window.</summary>
    public IReadOnlyList<AboutLink>? Links { get; init; }
}

/// <summary>
/// A link button displayed in the About window.
/// </summary>
public class AboutLink
{
    /// <summary>Button label text.</summary>
    public required string Label { get; init; }

    /// <summary>URL opened when the button is clicked.</summary>
    public required string Url { get; init; }
}
