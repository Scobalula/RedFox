using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;

namespace RedFox.GameExtraction;

/// <summary>
/// Creates and exposes the shared logger factory used by GameExtraction frontends and the asset manager.
/// </summary>
public static class GameExtractionLogging
{
    private const string LogDirectoryName = "logs";
    private static ILoggerFactory _factory = NullLoggerFactory.Instance;
    private static LogFileProvider? _fileProvider;
    private static bool _ownsFactory;
    private static LogLevel _baseMinimumLevel = LogLevel.Information;
    private static LogLevel _minimumLevel = LogLevel.Information;

    /// <summary>
    /// Gets the name of the setting that controls debug-level logging.
    /// </summary>
    public const string VerboseSettingName = "VerboseLogging";

    /// <summary>
    /// Gets the standard setting that controls debug-level logging. Frontends should include this in their setting
    /// definitions and apply it with <see cref="IsVerboseEnabled"/> before configuring logging.
    /// </summary>
    public static GameExtractionSetting VerboseSetting { get; } = new()
    {
        Name = VerboseSettingName,
        Group = GameExtractionSettingGroup.General,
        Label = "Verbose logging",
        Description = "Write debug-level detail to the log file.",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = false,
    };

    /// <summary>
    /// Gets or sets the logger factory used by GameExtraction components. Assigning <see langword="null"/> restores the no-op factory.
    /// </summary>
    public static ILoggerFactory Factory
    {
        get => _factory;
        set
        {
            if (ReferenceEquals(_factory, value))
                return;

            DisposeOwnedFactory();
            _factory = value ?? NullLoggerFactory.Instance;
        }
    }

    /// <summary>
    /// Gets the minimum level currently applied to file logging.
    /// </summary>
    internal static LogLevel MinimumLevel => _minimumLevel;

    /// <summary>
    /// Creates the shared logger factory for an application and assigns it to <see cref="Factory"/>.
    /// </summary>
    /// <param name="appName">The application name used to locate the log directory.</param>
    /// <returns>The configured logger factory.</returns>
    public static ILoggerFactory Configure(string appName) => Configure(appName, _ => { });

    /// <summary>
    /// Creates the shared logger factory for an application and assigns it to <see cref="Factory"/>.
    /// </summary>
    /// <param name="appName">The application name used to locate the log directory.</param>
    /// <param name="configure">A callback that customizes logging behavior.</param>
    /// <returns>The configured logger factory.</returns>
    public static ILoggerFactory Configure(string appName, Action<GameExtractionLogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        GameExtractionLogOptions options = new();
        configure(options);
        return Configure(appName, options);
    }

    /// <summary>
    /// Creates the shared logger factory for an application and assigns it to <see cref="Factory"/>.
    /// </summary>
    /// <param name="appName">The application name used to locate the log directory.</param>
    /// <param name="options">The options that control logging behavior.</param>
    /// <returns>The configured logger factory.</returns>
    public static ILoggerFactory Configure(string appName, GameExtractionLogOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentNullException.ThrowIfNull(options);

        options.Directory = Path.GetFullPath(options.Directory ?? GetLogDirectory(appName));
        Directory.CreateDirectory(options.Directory);

        DisposeOwnedFactory();

        _baseMinimumLevel = options.MinimumLevel;
        _minimumLevel = options.MinimumLevel;
        _fileProvider = new LogFileProvider(options);

        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(_fileProvider);

            if (options.WriteToConsole)
            {
                builder.AddFilter<ConsoleLoggerProvider>(category: null, _baseMinimumLevel);
                builder.AddConsole();
            }

            if (options.WriteToDebug)
            {
                builder.AddFilter<DebugLoggerProvider>(category: null, _baseMinimumLevel);
                builder.AddDebug();
            }
        });

        _factory = factory;
        _ownsFactory = true;

        factory.CreateLogger("RedFox.GameExtraction").LogInformation("GameExtraction logging initialized for {ApplicationName}", appName);
        return factory;
    }

    /// <summary>
    /// Applies the <see cref="VerboseSetting"/> from the supplied settings to the active logger factory, changing the level
    /// without a restart. Verbose enables debug-level logging, and disabling it restores the level configured at startup.
    /// </summary>
    /// <param name="settings">The settings to apply.</param>
    public static void ApplySettings(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _minimumLevel = IsVerboseEnabled(settings) ? LogLevel.Debug : _baseMinimumLevel;
    }

    /// <summary>
    /// Applies recognized logging flags to the supplied options and returns the arguments that were not consumed.
    /// Recognized flags are <c>--verbose</c>/<c>-v</c> for debug-level console and file logging and <c>--quiet</c>/<c>-q</c> for warnings and errors only.
    /// </summary>
    /// <param name="arguments">The command line arguments to inspect.</param>
    /// <param name="options">The options that receive the parsed logging behavior.</param>
    /// <returns>The arguments that were not recognized as logging flags.</returns>
    public static string[] ApplyArguments(IReadOnlyList<string> arguments, GameExtractionLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(options);

        List<string> remaining = new(arguments.Count);

        foreach (string argument in arguments)
        {
            switch (argument)
            {
                case "--verbose" or "-v":
                    options.MinimumLevel = LogLevel.Debug;
                    options.WriteToConsole = true;
                    break;
                case "--quiet" or "-q":
                    options.MinimumLevel = LogLevel.Warning;
                    break;
                default:
                    remaining.Add(argument);
                    break;
            }
        }

        return [.. remaining];
    }

    /// <summary>
    /// Gets a value indicating whether the supplied settings enable the <see cref="VerboseSetting"/>.
    /// </summary>
    /// <param name="settings">The settings to inspect.</param>
    /// <returns><see langword="true"/> when verbose logging is enabled; otherwise, <see langword="false"/>.</returns>
    public static bool IsVerboseEnabled(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return bool.TryParse(settings.GetSettingValue(VerboseSetting), out bool enabled) && enabled;
    }

    /// <summary>
    /// Gets the directory that GameExtraction log files are written to for an application name.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <returns>The log directory path.</returns>
    public static string GetLogDirectory(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, appName, LogDirectoryName);
    }

    private static void DisposeOwnedFactory()
    {
        if (_ownsFactory)
        {
            _fileProvider?.Dispose();
            (_factory as IDisposable)?.Dispose();
        }

        _fileProvider = null;
        _ownsFactory = false;
    }
}
