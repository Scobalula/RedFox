namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Identifies the arithmetic operation used to combine source and destination blend results.
/// </summary>
public enum BlendOp
{
    /// <summary>
    /// Adds the source and destination values.
    /// </summary>
    Add = 0,

    /// <summary>
    /// Subtracts the destination value from the source value.
    /// </summary>
    Subtract = 1,

    /// <summary>
    /// Subtracts the source value from the destination value.
    /// </summary>
    ReverseSubtract = 2,

    /// <summary>
    /// Selects the minimum of the source and destination values.
    /// </summary>
    Minimum = 3,

    /// <summary>
    /// Selects the maximum of the source and destination values.
    /// </summary>
    Maximum = 4,
}