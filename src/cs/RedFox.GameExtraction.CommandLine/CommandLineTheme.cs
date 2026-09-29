namespace RedFox.GameExtraction.CommandLine;

/// <summary>
/// Defines the colors used by the command line shell. Values are hex colors (such as <c>#E53935</c>)
/// or console color names (such as <c>grey</c>).
/// </summary>
public sealed class CommandLineTheme
{
    /// <summary>
    /// Gets the accent color used for the banner, prompt, commands, and progress bars.
    /// </summary>
    public string Accent { get; init; } = "#E53935";

    /// <summary>
    /// Gets the optional color the banner is blended towards, line by line, starting from <see cref="Accent"/>.
    /// Only applied to a custom <see cref="GameExtractionCommandLineConfig.Banner"/>.
    /// </summary>
    public string? BannerGradient { get; init; }

    /// <summary>
    /// Gets the color used for secondary text.
    /// </summary>
    public string Muted { get; init; } = "grey";

    /// <summary>
    /// Gets the color used for successful operations.
    /// </summary>
    public string Success { get; init; } = "#66BB6A";

    /// <summary>
    /// Gets the color used for warnings and cancellations.
    /// </summary>
    public string Warning { get; init; } = "#FFCA28";

    /// <summary>
    /// Gets the color used for errors.
    /// </summary>
    public string Error { get; init; } = "#EF5350";
}
