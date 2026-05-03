namespace RedFox.GameExtraction.UI;

/// <summary>
/// Describes a link button displayed in the About window.
/// </summary>
public sealed class AboutLink
{
    /// <summary>
    /// Gets the button label text.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the URL opened when the button is clicked.
    /// </summary>
    public required string Url { get; init; }
}