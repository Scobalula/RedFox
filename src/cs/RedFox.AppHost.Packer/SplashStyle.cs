namespace RedFox.AppHost.Packer;

/// <summary>
/// Describes the look of the launcher splash screen. Colours are 32-bit ARGB values.
/// </summary>
internal readonly record struct SplashStyle
{
    /// <summary>
    /// Gets the font family used for all splash text.
    /// </summary>
    public required string FontFamily { get; init; }

    /// <summary>
    /// Gets the window background colour.
    /// </summary>
    public required uint Background { get; init; }

    /// <summary>
    /// Gets the application name colour.
    /// </summary>
    public required uint Foreground { get; init; }

    /// <summary>
    /// Gets the version and status text colour.
    /// </summary>
    public required uint SecondaryText { get; init; }

    /// <summary>
    /// Gets the progress bar colour.
    /// </summary>
    public required uint Accent { get; init; }

    /// <summary>
    /// Gets the window border colour, fully transparent for none.
    /// </summary>
    public required uint Border { get; init; }

    /// <summary>
    /// Gets the window corner radius in pixels at 96 DPI, zero for square corners.
    /// </summary>
    public required uint CornerRadius { get; init; }
}
