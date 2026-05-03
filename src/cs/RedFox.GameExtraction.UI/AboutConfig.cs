namespace RedFox.GameExtraction.UI;

/// <summary>
/// Configures the About window content and links.
/// </summary>
public sealed class AboutConfig
{
    /// <summary>
    /// Gets the description text shown below the title.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the links displayed as buttons in the About window.
    /// </summary>
    public IReadOnlyList<AboutLink>? Links { get; init; }
}