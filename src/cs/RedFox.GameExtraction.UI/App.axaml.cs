using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using RedFox.Avalonia.Themes;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Initializes the GameExtraction Avalonia application and applies the configured accent and font.
/// </summary>
public class App : Application
{
    private static readonly Color LightAccentForeground	= Color.Parse("#FFFBEB");
    private static readonly Color DarkAccentForeground	= Color.Parse("#171717");

    /// <summary>
    /// Gets the high contrast theme variant, which falls back to the dark theme for resources it does not override.
    /// </summary>
    public static ThemeVariant HighContrastVariant { get; } = new("HighContrast", ThemeVariant.Dark);

    internal static GameExtractionConfig? CurrentConfig { get; set; }

    /// <summary>
    /// Loads the application resources and applies configuration-specific theme values.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        foreach (ResourceDictionary resources in Resources.ThemeDictionaries.Values.Cast<ResourceDictionary>())
        {
            resources["ScenePreviewClearColor"] = RedFoxThemeColors.ToVector4(((ISolidColorBrush)resources["ThemeCardBrush"]!).Color);
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
        Resources["AccentForegroundBrush"]					= new SolidColorBrush(ContrastRatio(accent, LightAccentForeground) >= 4.5 ? LightAccentForeground : DarkAccentForeground);
        Resources["AssetSelectionBrush"]					= new SolidColorBrush(accent, 0.16);
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

        foreach (ThemeVariant variant in Resources.ThemeDictionaries.Keys)
        {
            ApplyAccent((ResourceDictionary)Resources.ThemeDictionaries[variant], accent, CurrentConfig.WindowTintStrength);
        }

        ApplyTheme(CurrentConfig);

        if (PlatformSettings is not null)
        {
            PlatformSettings.ColorValuesChanged += (_, _) => ApplyTheme(CurrentConfig);
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

    /// <summary>
    /// Applies the interface theme selected by the theme setting.
    /// </summary>
    /// <param name="config">The game extraction configuration.</param>
    internal static void ApplyTheme(GameExtractionConfig config)
    {
        if (Current is null)
        {
            return;
        }

        if (!Enum.TryParse(config.Settings.GetSettingValue(GameExtractionApp.ThemeSetting), true, out InterfaceTheme theme))
        {
            theme = InterfaceTheme.Dark;
        }

        if (theme == InterfaceTheme.System && Current.PlatformSettings?.GetColorValues().ContrastPreference == ColorContrastPreference.High)
        {
            theme = InterfaceTheme.HighContrast;
        }

        Current.RequestedThemeVariant = theme switch
        {
            InterfaceTheme.Light		=> ThemeVariant.Light,
            InterfaceTheme.HighContrast	=> HighContrastVariant,
            InterfaceTheme.System		=> ThemeVariant.Default,
            _							=> ThemeVariant.Dark,
        };
    }

    private static void ApplyAccent(ResourceDictionary resources, Color accent, double tintStrength)
    {
        Color card		= ((ISolidColorBrush)resources["ThemeCardBrush"]!).Color;
        Color header	= ((ISolidColorBrush)resources["ThemeHeaderBrush"]!).Color;
        Color readable	= ReadableAccent(accent, card);

        resources["AssetSelectionBorderBrush"]				= new SolidColorBrush(readable);
        resources["TextControlBorderBrushFocused"]			= resources["AssetSelectionBorderBrush"];
        resources["ComboBoxBackgroundBorderBrushFocused"]	= resources["AssetSelectionBorderBrush"];
        resources["ThemePanelHeaderBrush"]					= new SolidColorBrush(RedFoxThemeColors.Tint(header, accent, tintStrength / 4));
        resources["ThemeToolbarBrush"]						= new RadialGradientBrush
        {
            Center			= new RelativePoint(0.04, 0.5, RelativeUnit.Relative),
            GradientOrigin	= new RelativePoint(0.04, 0.5, RelativeUnit.Relative),
            RadiusX			= RelativeScalar.Parse("35%"),
            RadiusY			= RelativeScalar.Parse("400%"),
            GradientStops	= { new GradientStop(RedFoxThemeColors.Tint(card, accent, tintStrength * 3), 0), new GradientStop(RedFoxThemeColors.Tint(card, accent, tintStrength), 0.45), new GradientStop(card, 1) }
        };
    }

    private static Color ReadableAccent(Color accent, Color background)
    {
        Color target = Luminance(background) < 0.5 ? Colors.White : Colors.Black;
        Color result = accent;

        for (int step = 1; step <= 10 && ContrastRatio(result, background) < 3; step++)
        {
            double amount = step / 10.0;

            result = Color.FromRgb((byte)(accent.R + (target.R - accent.R) * amount), (byte)(accent.G + (target.G - accent.G) * amount), (byte)(accent.B + (target.B - accent.B) * amount));
        }

        return result;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        double lighter	= Math.Max(Luminance(first), Luminance(second));
        double darker	= Math.Min(Luminance(first), Luminance(second));

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;

            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static Color Shift(Color color, int amount)
    {
        return Color.FromArgb(color.A, (byte)Math.Clamp(color.R + amount, 0, 255), (byte)Math.Clamp(color.G + amount, 0, 255), (byte)Math.Clamp(color.B + amount, 0, 255));
    }

}
