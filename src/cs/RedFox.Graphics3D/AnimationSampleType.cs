namespace RedFox.Graphics3D;

/// <summary>
/// A class to handle sampling an <see cref="Animation"/> at arbitrary frames or in a linear fashion.
/// </summary>
public enum AnimationSampleType
{
    /// <summary>
    /// Samples using a normalized position within the animation range.
    /// </summary>
    Percentage,

    /// <summary>
    /// Samples using an absolute frame number.
    /// </summary>
    AbsoluteFrameTime,

    /// <summary>
    /// Samples using an absolute time value.
    /// </summary>
    AbsoluteTime,

    /// <summary>
    /// Advances sampling by the elapsed time since the previous update.
    /// </summary>
    DeltaTime,
}
