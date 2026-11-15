using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RedFox.Avalonia.Themes;

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

        if (Resources["ThemeCardBrush"] is ISolidColorBrush cardBrush)
        {
            Resources["ScenePreviewClearColor"] = RedFoxThemeColors.ToVector4(cardBrush.Color);
        }

        if (CurrentConfig is null)
        {
            return;
        }

        Color accent = Color.Parse(CurrentConfig.AccentColor);
        Color hover = Shift(accent, 24);
        Color pressed = Shift(accent, -24);

        Resources["AccentBrush"]							= new SolidColorBrush(accent);
        Resources["AccentHoverBrush"]						= new SolidColorBrush(hover);
        Resources["AccentPressedBrush"]						= new SolidColorBrush(pressed);
        Resources["AssetSelectionBrush"]					= new SolidColorBrush(accent, 0.16);
        Resources["AssetSelectionBorderBrush"]				= new SolidColorBrush(accent, 0.6);
        Resources["TextControlBorderBrushFocused"]			= Resources["AssetSelectionBorderBrush"];
        Resources["ComboBoxBackgroundBorderBrushFocused"]	= Resources["AssetSelectionBorderBrush"];
        Resources["SliderTrackValueFill"]					= Resources["AccentBrush"];
        Resources["SliderTrackValueFillPointerOver"]		= Resources["AccentHoverBrush"];
        Resources["SliderTrackValueFillPressed"]			= Resources["AccentHoverBrush"];
        Resources["SliderThumbBackgroundPressed"]			= Resources["AccentHoverBrush"];
        Resources["SystemAccentColor"]						= accent;
        Resources["SystemAccentColorLight1"]				= hover;
        Resources["SystemAccentColorLight2"]				= Shift(accent, 48);
        Resources["SystemAccentColorLight3"]				= Shift(accent, 72);
        Resources["SystemAccentColorDark1"]					= pressed;
        Resources["SystemAccentColorDark2"]					= Shift(accent, -48);
        Resources["SystemAccentColorDark3"]					= Shift(accent, -72);

        if (Resources["ThemeHeaderBrush"] is ISolidColorBrush headerBrush)
        {
            Resources["ThemePanelHeaderBrush"] = new SolidColorBrush(RedFoxThemeColors.Tint(headerBrush.Color, accent, CurrentConfig.WindowTintStrength / 4));
        }

        if (Resources["ThemeCardBrush"] is ISolidColorBrush panelBrush)
        {
            Color toolbar = panelBrush.Color;

            Resources["ThemeToolbarBrush"] = new RadialGradientBrush
            {
                Center			= new RelativePoint(0.04, 0.5, RelativeUnit.Relative),
                GradientOrigin	= new RelativePoint(0.04, 0.5, RelativeUnit.Relative),
                RadiusX			= RelativeScalar.Parse("35%"),
                RadiusY			= RelativeScalar.Parse("400%"),
                GradientStops	= { new GradientStop(RedFoxThemeColors.Tint(toolbar, accent, CurrentConfig.WindowTintStrength * 3), 0), new GradientStop(RedFoxThemeColors.Tint(toolbar, accent, CurrentConfig.WindowTintStrength), 0.45), new GradientStop(toolbar, 1) }
            };
        }

        if (!string.IsNullOrEmpty(CurrentConfig.FontFamily))
        {
            Resources["AppFont"] = new FontFamily(CurrentConfig.FontFamily);
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
