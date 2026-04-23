using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents renderer-agnostic camera control input for a single frame.
/// </summary>
/// <remarks>
/// Initializes a new camera controller input value.
/// </remarks>
/// <param name="lookDelta">The look delta.</param>
/// <param name="zoomDelta">The zoom delta.</param>
/// <param name="panDelta">The pan delta.</param>
/// <param name="dollyDelta">The dolly delta.</param>
/// <param name="moveIntent">The move intent vector.</param>
public readonly struct CameraControllerInput(Vector2 lookDelta, float zoomDelta, Vector2 panDelta, float dollyDelta, Vector3 moveIntent)
{
    /// <summary>
    /// Gets an empty input value.
    /// </summary>
    public static CameraControllerInput Empty => default;

    /// <summary>
    /// Gets the look delta where X is yaw and Y is pitch.
    /// </summary>
    public Vector2 LookDelta { get; } = lookDelta;

    /// <summary>
    /// Gets the zoom delta, usually sourced from scroll wheel input.
    /// </summary>
    public float ZoomDelta { get; } = zoomDelta;

    /// <summary>
    /// Gets the pan delta where X is screen-right and Y is screen-up.
    /// </summary>
    public Vector2 PanDelta { get; } = panDelta;

    /// <summary>
    /// Gets the dolly delta, usually sourced from drag gestures.
    /// </summary>
    public float DollyDelta { get; } = dollyDelta;

    /// <summary>
    /// Gets movement intent in local camera-space where X is right, Y is up, and Z is forward.
    /// </summary>
    public Vector3 MoveIntent { get; } = moveIntent;
}
