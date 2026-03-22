using Avalonia;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Entry point for consuming the RedFox.UI.GameExtraction library.
/// Consumer projects call <see cref="Run"/> from their Main() method.
///
/// Example usage:
/// <code>
/// public static void Main(string[] args)
/// {
///     GameExtractionApp.Run(new GameExtractionConfig
///     {
///         GameSource = new MyGameSource(),
///         Exporter = new MyExporter(),
///         PreviewHandler = new MyPreviewHandler(),
///         Settings = new MySettings(),
///         WindowTitle = "My Game Ripper",
///         AppName = "MyGameRipper",
///         Version = "1.0.0"
///     });
/// }
/// </code>
/// </summary>
public static class GameExtractionApp
{
    /// <summary>
    /// Builds and runs the Avalonia application with the provided configuration.
    /// This method blocks until the application window is closed.
    /// </summary>
    /// <param name="config">The game extraction configuration.</param>
    public static void Run(GameExtractionConfig config)
    {
        var settingsPath = SettingsBase.GetDefaultSettingsPath(config.AppName);
        config.Settings.LoadFrom(settingsPath);

        App.CurrentConfig = config;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);
    }

    /// <summary>
    /// Builds the Avalonia app builder. Can be used for advanced scenarios
    /// where the consumer needs to customize the app builder.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
