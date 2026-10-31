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
    /// Gets the colour of the caret line's number.
    /// </summary>
    public Color ActiveLineNumber { get; init; } = Color.Parse("#C6C6C6");

    /// <summary>
    /// Gets the current line outline colour.
    /// </summary>
    public Color CurrentLine { get; init; } = Color.Parse("#282828");

    /// <summary>
    /// Gets the selection colour.
    /// </summary>
    public Color Selection { get; init; } = Color.Parse("#264F78");

    /// <summary>
    /// Gets the colour of the whitespace marks inside the selection.
    /// </summary>
    public Color SelectionWhitespace { get; init; } = Color.Parse("#40E3E4E2");

    /// <summary>
    /// Gets the colour of the selection's other occurrences.
    /// </summary>
    public Color WordHighlight { get; init; } = Color.Parse("#26ADD6FF");

    /// <summary>
    /// Gets the caret colour.
    /// </summary>
    public Color Caret { get; init; } = Color.Parse("#AEAFAD");

    /// <summary>
    /// Gets the colour of the indent guide around the caret.
    /// </summary>
    public Color ActiveIndentGuide { get; init; } = Color.Parse("#585858");

    /// <summary>
    /// Gets the colour of the line between the line numbers and the code.
    /// </summary>
    public Color GutterLine { get; init; } = Color.Parse("#6E6E6E");

    /// <summary>
    /// Gets the fill of the bracket at the caret and its partner.
    /// </summary>
    public Color BracketMatch { get; init; } = Color.Parse("#1A006400");

    /// <summary>
    /// Gets the outline of the bracket at the caret and its partner.
    /// </summary>
    public Color BracketMatchBorder { get; init; } = Color.Parse("#888888");

    /// <summary>
    /// Gets the indent guide colour.
    /// </summary>
    public Color IndentGuide { get; init; } = Color.Parse("#404040");

    /// <summary>
    /// Gets the colours brackets cycle through as they nest.
    /// </summary>
    public IReadOnlyList<Color> Brackets { get; init; } = [Color.Parse("#FFD700"), Color.Parse("#DA70D6"), Color.Parse("#179FFF")];

    /// <summary>
    /// Creates a palette from the surface of a host application. Text, selection and highlights keep their VS Code
    /// values, so they stay readable whatever the application's accent is.
    /// </summary>
    /// <param name="control">The control whose resources are read.</param>
    /// <returns>The palette that matches the host.</returns>
    public static CodeEditorColors FromResources(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        CodeEditorColors fallback = VsCodeDark;
        return fallback with
        {
            Background = Find(control, "ThemeCardBrush", fallback.Background),
        };
    }

    private static Color Find(Control control, string key, Color fallback)
    {
        return control.TryFindResource(key, control.ActualThemeVariant, out object? value) && value is ISolidColorBrush brush ? brush.Color : fallback;
    }
}
