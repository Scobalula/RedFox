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
    public static void Run(GameExtractionConfig config) => Run(config, null);

    /// <summary>
    /// Builds and runs the Avalonia application with the provided configuration and logging options.
    /// This method blocks until the application window is closed.
    /// </summary>
    /// <param name="config">The game extraction configuration.</param>
    /// <param name="configureLogging">A callback that customizes the shared file logging behavior.</param>
    public static void Run(GameExtractionConfig config, Action<GameExtractionLogOptions>? configureLogging)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (configureLogging is null)
            GameExtractionLogging.Configure(config.AppName);
        else
            GameExtractionLogging.Configure(config.AppName, configureLogging);

        string settingsPath = GameExtractionSettings.GetDefaultSettingsPath(config.AppName);
        config.Settings.LoadFrom(settingsPath);
        config.PreviewSettings.LoadFrom(config.Settings);

        App.CurrentConfig = config;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);
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
