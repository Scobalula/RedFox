using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Initializes the GameExtraction Avalonia application and applies the configured accent and font.
/// </summary>
public class App : Application
{
    internal static GameExtractionConfig? CurrentConfig { get; set; }

    /// <summary>
    /// Loads the application resources and applies configuration-specific theme values.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (CurrentConfig is null)
        {
            return;
        }

        Color accent = Color.Parse(CurrentConfig.AccentColor);
        Color hover = Shift(accent, 24);
        Color pressed = Shift(accent, -24);

        Resources["AccentBrush"] = new SolidColorBrush(accent);
        Resources["AccentHoverBrush"] = new SolidColorBrush(hover);
        Resources["AccentPressedBrush"] = new SolidColorBrush(pressed);
        Resources["SystemAccentColor"] = accent;
        Resources["SystemAccentColorLight1"] = hover;
        Resources["SystemAccentColorLight2"] = Shift(accent, 48);
        Resources["SystemAccentColorLight3"] = Shift(accent, 72);
        Resources["SystemAccentColorDark1"] = pressed;
        Resources["SystemAccentColorDark2"] = Shift(accent, -48);
        Resources["SystemAccentColorDark3"] = Shift(accent, -72);

        if (!string.IsNullOrEmpty(CurrentConfig.FontFamily))
        {
            Resources["InterFont"] = new FontFamily(CurrentConfig.FontFamily);
        }
    }

    /// <summary>
    /// Creates the main window when the application is running with a desktop lifetime.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && CurrentConfig is not null)
        {
            Views.MainWindow mainWindow = new();
            mainWindow.Initialize(CurrentConfig);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Color Shift(Color color, int amount)
    {
        return Color.FromArgb(color.A, (byte)Math.Clamp(color.R + amount, 0, 255), (byte)Math.Clamp(color.G + amount, 0, 255), (byte)Math.Clamp(color.B + amount, 0, 255));
    }
}
