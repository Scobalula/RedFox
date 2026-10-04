using Avalonia;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Entry point for applications that host the GameExtraction Avalonia shell.
/// </summary>
public static class GameExtractionApp
{
    /// <summary>
    /// Builds and runs the Avalonia application with the provided configuration.
    /// This method blocks until the application window is closed.
    /// </summary>
    /// <param name="config">The game extraction configuration.</param>
    public static void Run(GameExtractionConfig config) => Run(config, []);

    /// <summary>
    /// Builds and runs the Avalonia application with the provided configuration and command line arguments.
    /// The <c>--verbose</c> (<c>-v</c>) flag enables debug-level console and file logging, while
    /// <c>--quiet</c> (<c>-q</c>) limits logging to warnings and errors. Recognized flags are consumed and the
    /// remaining arguments are forwarded to Avalonia.
    /// </summary>
    /// <param name="config">The game extraction configuration.</param>
    /// <param name="args">The command line arguments.</param>
    public static void Run(GameExtractionConfig config, string[] args) => Run(config, args, null);

    /// <summary>
    /// Builds and runs the Avalonia application with the provided configuration, command line arguments, and logging options.
    /// This method blocks until the application window is closed.
    /// </summary>
    /// <param name="config">The game extraction configuration.</param>
    /// <param name="args">The command line arguments.</param>
    /// <param name="configureLogging">A callback that customizes the shared file logging behavior.</param>
    public static void Run(GameExtractionConfig config, string[] args, Action<GameExtractionLogOptions>? configureLogging)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        string settingsPath = GameExtractionSettings.GetDefaultSettingsPath(config.AppName);
        config.Settings.LoadFrom(settingsPath);
        config.PreviewSettings.LoadFrom(config.Settings);

        GameExtractionLogOptions logOptions = new();
        string[] remainingArgs = GameExtractionLogging.ApplyArguments(args, logOptions);
        configureLogging?.Invoke(logOptions);
        GameExtractionLogging.Configure(config.AppName, logOptions);
        GameExtractionLogging.ApplySettings(config.Settings);

        App.CurrentConfig = config;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(remainingArgs);
    }

    /// <summary>
    /// Builds the Avalonia app builder. Can be used for advanced scenarios
    /// where the consumer needs to customize the app builder.
    /// </summary>
    /// <returns>The configured Avalonia app builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
