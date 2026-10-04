namespace RedFox.GameExtraction.CommandLine;

/// <summary>
/// Configures a GameExtraction interactive command line application.
/// </summary>
public sealed class GameExtractionCommandLineConfig
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOptions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the factory used to create the asset manager that powers the shell.
    /// </summary>
    public required Func<AssetManager> AssetManagerFactory { get; init; }

    /// <summary>
    /// Gets the mutable settings used by the application. Persisted values are loaded over these defaults on startup.
    /// </summary>
    public required GameExtractionSettings Settings { get; init; }

    /// <summary>
    /// Gets the settings that can be viewed and changed with the <c>/set</c> command.
    /// </summary>
    public required IReadOnlyList<GameExtractionSetting> SettingDefinitions { get; init; }

    /// <summary>
    /// Gets the application title shown in the banner and status views.
    /// </summary>
    public string Title { get; init; } = "Game Extraction";

    /// <summary>
    /// Gets the description shown below the banner.
    /// </summary>
    public string Description { get; init; } = "Mount sources and export discovered assets.";

    /// <summary>
    /// Gets the application name used to locate persisted settings and command history.
    /// Frontends sharing the same name share the same settings file.
    /// </summary>
    public string AppName { get; init; } = "RedFox";

    /// <summary>
    /// Gets the version string shown below the banner.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the optional author shown below the banner.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the optional multi-line ASCII art banner. When <see langword="null"/>, the <see cref="Title"/>
    /// is rendered as FIGlet text instead.
    /// </summary>
    public string? Banner { get; init; }

    /// <summary>
    /// Gets the optional donation details. When set, the donation message is shown on every interactive launch and
    /// the user is asked to donate on the first launch.
    /// </summary>
    public DonationConfig? Donation { get; init; }

    /// <summary>
    /// Gets the color scheme used by the shell.
    /// </summary>
    public CommandLineTheme Theme { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether file-backed sources can be mounted.
    /// </summary>
    public bool SupportsFileSources { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether directory-backed sources can be mounted.
    /// </summary>
    public bool SupportsDirectorySources { get; init; }

    /// <summary>
    /// Gets a value indicating whether process-backed sources can be mounted.
    /// </summary>
    public bool SupportsProcessSources { get; init; }

    /// <summary>
    /// Gets source options passed to each mount request.
    /// </summary>
    public IReadOnlyDictionary<string, object?> SourceOptions { get; init; } = EmptyOptions;

    /// <summary>
    /// Gets metadata names displayed as additional columns when listing assets.
    /// </summary>
    public IReadOnlyList<string> MetadataColumns { get; init; } = [];

    /// <summary>
    /// Gets additional commands exposed by the shell. A command sharing a name or alias with a
    /// built-in command replaces it.
    /// </summary>
    public IReadOnlyList<ICommandLineCommand> Commands { get; init; } = [];

    /// <summary>
    /// Gets an optional callback invoked once before any sources are mounted, used to pre-load
    /// data such as name tables or to locate a game installation.
    /// </summary>
    public Func<CommandLineSession, CancellationToken, Task>? Startup { get; init; }

    /// <summary>
    /// Gets an optional callback invoked after each source is mounted.
    /// </summary>
    public Func<CommandLineSession, IAssetSource, CancellationToken, Task>? SourceMounted { get; init; }
}
