using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace RedFox.Avalonia.Themes;

/// <summary>
/// Applies the shared RedFox Avalonia theme to an application.
/// </summary>
public static class RedFoxTheme
{
    private static readonly Uri BaseUri = new("avares://RedFox.Avalonia.Themes");
    private static readonly Uri ThemeUri = new("avares://RedFox.Avalonia.Themes/RedFoxTheme.axaml");

    /// <summary>
    /// Adds the RedFox theme resources and styles to the supplied application.
    /// </summary>
    /// <param name="application">The application to theme.</param>
    public static void Apply(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.Resources["ControlCornerRadius"] = new CornerRadius(0.0);
        application.Resources["OverlayCornerRadius"] = new CornerRadius(0.0);

        application.Styles.Add(new FluentTheme());
        application.Styles.Add(new StyleInclude(BaseUri)
        {
            Source = ThemeUri,
        });
    }
}