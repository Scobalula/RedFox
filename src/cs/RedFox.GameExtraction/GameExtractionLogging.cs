using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RedFox.GameExtraction;

/// <summary>
/// Creates and exposes the shared logger factory used by GameExtraction frontends and the asset manager.
/// </summary>
public static class GameExtractionLogging
{
    private const string LogDirectoryName = "logs";
    private static ILoggerFactory _factory = NullLoggerFactory.Instance;

    /// <summary>
    /// Gets or sets the logger factory used by GameExtraction components. Assigning <see langword="null"/> restores the no-op factory.
    /// </summary>
    public static ILoggerFactory Factory
    {
        get => _factory;
        set => _factory = value ?? NullLoggerFactory.Instance;
    }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentNullException.ThrowIfNull(configure);

        GameExtractionLogOptions options = new();
        configure(options);
        options.Directory = Path.GetFullPath(options.Directory ?? GetLogDirectory(appName));
        Directory.CreateDirectory(options.Directory);

        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(options.MinimumLevel);
            builder.AddProvider(new LogFileProvider(options));

            if (options.WriteToConsole)
                builder.AddConsole();

            if (options.WriteToDebug)
                builder.AddDebug();
        });

        factory.CreateLogger("RedFox.GameExtraction").LogInformation("GameExtraction logging initialized for {ApplicationName}", appName);
        return Factory = factory;
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
}
