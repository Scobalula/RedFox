using Avalonia.Controls;
using Avalonia.Media;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Defines every colour of the code editor's chrome. Assign one to <see cref="CodeEditorControl.Colors"/> to override
/// the host application's theme.
/// </summary>
public sealed record CodeEditorColors
{
    /// <summary>
    /// Gets the VS Code Dark+ palette.
    /// </summary>
    public static CodeEditorColors VsCodeDark { get; } = new();

    /// <summary>
    /// Gets the editing surface colour.
    /// </summary>
    public Color Background { get; init; } = Color.Parse("#1E1E1E");

    /// <summary>
    /// Gets the colour of text that has no token colour.
    /// </summary>
    public Color Foreground { get; init; } = Color.Parse("#D4D4D4");

    /// <summary>
    /// Gets the line number colour.
    /// </summary>
    public Color LineNumber { get; init; } = Color.Parse("#858585");

    /// <summary>
    /// Gets the current line outline colour.
    /// </summary>
    public Color CurrentLine { get; init; } = Color.Parse("#282828");

    /// <summary>
    /// Gets the selection colour.
    /// </summary>
    public Color Selection { get; init; } = Color.Parse("#264F78");

    /// <summary>
    /// Gets the caret colour.
    /// </summary>
    public Color Caret { get; init; } = Color.Parse("#AEAFAD");

    /// <summary>
    /// Gets the indent guide colour.
    /// </summary>
    public Color IndentGuide { get; init; } = Color.Parse("#404040");

    /// <summary>
    /// Gets the empty space beside the tab.
    /// </summary>
    public Color TabBarBackground { get; init; } = Color.Parse("#252526");

    /// <summary>
    /// Gets the tab colour.
    /// </summary>
    public Color TabBackground { get; init; } = Color.Parse("#1E1E1E");

    /// <summary>
    /// Gets the tab text colour.
    /// </summary>
    public Color TabForeground { get; init; } = Color.Parse("#FFFFFF");

    /// <summary>
    /// Gets the breadcrumb text colour.
    /// </summary>
    public Color BreadcrumbForeground { get; init; } = Color.Parse("#A9A9A9");

    /// <summary>
    /// Gets the file icon colour.
    /// </summary>
    public Color Accent { get; init; } = Color.Parse("#519ABA");

    /// <summary>
    /// Gets the colours brackets cycle through as they nest.
    /// </summary>
    public IReadOnlyList<Color> Brackets { get; init; } = [Color.Parse("#FFD700"), Color.Parse("#DA70D6"), Color.Parse("#179FFF")];

    /// <summary>
    /// Creates a palette from the resources of a host application, keeping the VS Code value of any it does not define.
    /// </summary>
    /// <param name="control">The control whose resources are read.</param>
    /// <returns>The palette that matches the host.</returns>
    public static CodeEditorColors FromResources(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        CodeEditorColors fallback = VsCodeDark;
        Color background = Find(control, "ThemeCardBrush", fallback.Background);
        Color foreground = Find(control, "ThemeForegroundBrush", fallback.Foreground);
        Color muted = Find(control, "ThemeMutedForegroundBrush", fallback.LineNumber);
        Color accent = Find(control, "AccentBrush", fallback.Accent);

        return fallback with
        {
            Background = background,
            Foreground = foreground,
            LineNumber = muted,
            CurrentLine = Find(control, "ThemeSecondaryBrush", fallback.CurrentLine),
            Selection = Color.FromArgb(0x66, accent.R, accent.G, accent.B),
            Caret = foreground,
            IndentGuide = Find(control, "ThemeBorderBrush", fallback.IndentGuide),
            TabBarBackground = Find(control, "ThemeBackgroundBrush", fallback.TabBarBackground),
            TabBackground = background,
            TabForeground = foreground,
            BreadcrumbForeground = muted,
            Accent = accent,
        };
    }

    private static Color Find(Control control, string key, Color fallback)
    {
        return control.TryFindResource(key, control.ActualThemeVariant, out object? value) && value is ISolidColorBrush brush ? brush.Color : fallback;
    }
}
