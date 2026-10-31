namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Identifies a button around a view cube.
/// </summary>
public enum ViewCubeButton
{
    /// <summary>
    /// No button.
    /// </summary>
    None,

    /// <summary>
    /// Asks for the default view.
    /// </summary>
    Reset,

    /// <summary>
    /// Asks to fit the model without changing the angle.
    /// </summary>
    Fit,

    /// <summary>
    /// Asks for the next up axis.
    /// </summary>
    UpAxis,

    /// <summary>
    /// Turns the top of the cube to the front.
    /// </summary>
    TurnTop,

    /// <summary>
    /// Turns the bottom of the cube to the front.
    /// </summary>
    TurnBottom,

    /// <summary>
    /// Turns the left of the cube to the front.
    /// </summary>
    TurnLeft,

    /// <summary>
    /// Turns the right of the cube to the front.
    /// </summary>
    TurnRight,
}
