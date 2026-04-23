using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a standard perspective camera with a simple look-at workflow.
/// </summary>
public sealed class PerspectiveCamera : Camera
{
    /// <summary>
    /// Initializes a new instance of <see cref="PerspectiveCamera"/>.
    /// </summary>
    public PerspectiveCamera()
        : base("PerspectiveCamera")
    {
        Projection = CameraProjection.Perspective;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PerspectiveCamera"/> with a specified name.
    /// </summary>
    /// <param name="name">The camera name.</param>
    public PerspectiveCamera(string name)
        : base(name)
    {
        Projection = CameraProjection.Perspective;
    }

    /// <summary>
    /// Sets camera orientation from a forward direction and up vector while preserving position.
    /// </summary>
    /// <param name="forward">The desired forward direction.</param>
    /// <param name="up">The desired up direction.</param>
    public void SetOrientation(Vector3 forward, Vector3 up)
    {
        Vector3 safeForward = forward.LengthSquared() > 1e-8f
            ? Vector3.Normalize(forward)
            : -Vector3.UnitZ;

        Vector3 safeUp = up.LengthSquared() > 1e-8f
            ? Vector3.Normalize(up)
            : Vector3.UnitY;

        Target = Position + safeForward;
        Up = safeUp;
    }
}
