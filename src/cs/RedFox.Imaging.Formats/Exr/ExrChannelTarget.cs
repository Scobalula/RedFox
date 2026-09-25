namespace RedFox.Imaging.Formats.Exr;

/// <summary>
/// Identifies which RGBA component receives a decoded EXR channel.
/// </summary>
internal enum ExrChannelTarget
{
    /// <summary>
    /// The channel is not mapped to an output component.
    /// </summary>
    Ignore,

    /// <summary>
    /// The channel feeds the red component.
    /// </summary>
    Red,

    /// <summary>
    /// The channel feeds the green component.
    /// </summary>
    Green,

    /// <summary>
    /// The channel feeds the blue component.
    /// </summary>
    Blue,

    /// <summary>
    /// The channel feeds the alpha component.
    /// </summary>
    Alpha,

    /// <summary>
    /// The channel is luminance and feeds red, green, and blue.
    /// </summary>
    Luminance,
}
