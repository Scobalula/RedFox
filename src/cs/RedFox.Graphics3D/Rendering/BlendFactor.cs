namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies a blend factor used by a pipeline state's color blend stage.
/// </summary>
public enum BlendFactor
{
    /// <summary>
    /// Always uses zero.
    /// </summary>
    Zero = 0,

    /// <summary>
    /// Always uses one.
    /// </summary>
    One = 1,

    /// <summary>
    /// Uses the source color.
    /// </summary>
    SourceColor = 2,

    /// <summary>
    /// Uses one minus the source color.
    /// </summary>
    InverseSourceColor = 3,

    /// <summary>
    /// Uses the source alpha.
    /// </summary>
    SourceAlpha = 4,

    /// <summary>
    /// Uses one minus the source alpha.
    /// </summary>
    InverseSourceAlpha = 5,

    /// <summary>
    /// Uses the destination color.
    /// </summary>
    DestinationColor = 6,

    /// <summary>
    /// Uses one minus the destination color.
    /// </summary>
    InverseDestinationColor = 7,

    /// <summary>
    /// Uses the destination alpha.
    /// </summary>
    DestinationAlpha = 8,

    /// <summary>
    /// Uses one minus the destination alpha.
    /// </summary>
    InverseDestinationAlpha = 9,
}