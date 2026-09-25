namespace RedFox.Imaging.Processing;

/// <summary>
/// Identifies a decoded pixel channel, or a constant, used by channel operations.
/// </summary>
public enum ColorChannel
{
    /// <summary>
    /// The red channel.
    /// </summary>
    Red,

    /// <summary>
    /// The green channel.
    /// </summary>
    Green,

    /// <summary>
    /// The blue channel.
    /// </summary>
    Blue,

    /// <summary>
    /// The alpha channel.
    /// </summary>
    Alpha,

    /// <summary>
    /// The constant value 0.
    /// </summary>
    Zero,

    /// <summary>
    /// The constant value 1.
    /// </summary>
    One,
}
