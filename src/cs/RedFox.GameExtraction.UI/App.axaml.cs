using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace RedFox.GameExtraction.UI;

public class App : Application
{
    internal static GameExtractionConfig? CurrentConfig { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Inject accent color from config into dynamic resources
        if (CurrentConfig is not null)
        {
            var accent = Color.Parse(CurrentConfig.AccentColor);
            var accentBrush = new SolidColorBrush(accent);

            // Lighter variant for hover
            var hoverColor = Color.FromArgb(
                accent.A,
                (byte)Math.Min(accent.R + 30, 255),
                (byte)Math.Min(accent.G + 30, 255),
                (byte)Math.Min(accent.B + 30, 255));

            // Darker variant for pressed
            var pressedColor = Color.FromArgb(
                accent.A,
                (byte)Math.Max(accent.R - 30, 0),
                (byte)Math.Max(accent.G - 30, 0),
                (byte)Math.Max(accent.B - 30, 0));

            // Subtle glow (low opacity accent)
            var glowColor = Color.FromArgb(40, accent.R, accent.G, accent.B);

            Resources["AccentBrush"] = accentBrush;
            Resources["AccentColor"] = accent;
            Resources["AccentHoverBrush"] = new SolidColorBrush(hoverColor);
            Resources["AccentPressedBrush"] = new SolidColorBrush(pressedColor);
            Resources["AccentGlowBrush"] = new SolidColorBrush(glowColor);

            // Gradient brushes for accent button
            Resources["AccentGradient"] = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(accent, 0),
                    new GradientStop(pressedColor, 1)
                }
            };
            Resources["AccentHoverGradient"] = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(hoverColor, 0),
                    new GradientStop(accent, 1)
                }
            };

            // Diagonal glow for sidebar — accent fading from top-left corner
            Resources["AccentRadialGlow"] = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.6, 0.7, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(40, accent.R, accent.G, accent.B), 0),
                    new GradientStop(Colors.Transparent, 1)
                }
            };

            // Override default font if configured
            if (!string.IsNullOrEmpty(CurrentConfig.FontFamily))
            {
                Resources["InterFont"] = new FontFamily(CurrentConfig.FontFamily);
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && CurrentConfig is not null)
        {
            var mainWindow = new Views.MainWindow();
            mainWindow.Initialize(CurrentConfig);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
