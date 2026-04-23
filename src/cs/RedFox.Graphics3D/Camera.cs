using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a camera in the scene graph. Produces a renderer-facing
/// <see cref="CameraView"/> that contains the view and projection matrices
/// for the current frame.
/// </summary>
/// <remarks>
/// Uses a right-handed coordinate system consistent with
/// <see cref="Matrix4x4.CreateLookAt"/> and <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/>.
/// </remarks>
public class Camera : SceneNode
{
    /// <summary>
    /// Gets or sets the world-space camera position.
    /// </summary>
    public Vector3 Position { get; set; } = new(0.0f, 1.0f, 5.0f);

    /// <summary>
    /// Gets or sets the world-space point the camera is aimed at.
    /// </summary>
    public Vector3 Target { get; set; } = Vector3.Zero;

    /// <summary>
    /// Gets or sets the world-space up direction.
    /// </summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>
    /// Gets or sets the projection mode.
    /// </summary>
    public CameraProjection Projection { get; set; } = CameraProjection.Perspective;

    /// <summary>
    /// Gets or sets the vertical field of view in degrees (perspective only).
    /// </summary>
    public float FieldOfView { get; set; } = 60.0f;

    /// <summary>
    /// Gets or sets the orthographic vertical view height (orthographic only).
    /// </summary>
    public float OrthographicSize { get; set; } = 10.0f;

    /// <summary>
    /// Gets or sets the near clip plane distance.
    /// </summary>
    public float NearPlane { get; set; } = 0.01f;

    /// <summary>
    /// Gets or sets the far clip plane distance.
    /// </summary>
    public float FarPlane { get; set; } = 1000.0f;

    /// <summary>
    /// Gets or sets the viewport aspect ratio (width divided by height).
    /// </summary>
    public float AspectRatio { get; set; } = 16.0f / 9.0f;

    /// <summary>
    /// Gets or sets look sensitivity applied to mouse-look style input.
    /// </summary>
    public float LookSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets zoom sensitivity applied to scroll/dolly style input.
    /// </summary>
    public float ZoomSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets pan sensitivity applied to drag-pan style input.
    /// </summary>
    public float PanSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets WASD movement speed in world units per second.
    /// </summary>
    public float MoveSpeed { get; set; } = 2.5f;

    /// <summary>
    /// Gets or sets the multiplier applied to <see cref="MoveSpeed"/> when boost is active.
    /// </summary>
    public float BoostMultiplier { get; set; } = 3.0f;

    /// <summary>
    /// Gets or sets a value indicating whether yaw input should be inverted.
    /// </summary>
    public bool InvertX { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pitch input should be inverted.
    /// </summary>
    public bool InvertY { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="Camera"/>.
    /// </summary>
    public Camera() : base("Camera") { }

    /// <summary>
    /// Initializes a new instance of <see cref="Camera"/> with the specified name.
    /// </summary>
    /// <param name="name">The camera name.</param>
    public Camera(string name) : base(name) { }

    /// <summary>
    /// Computes the world-to-view matrix for the current frame.
    /// </summary>
    public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(Position, Target, Up);

    /// <summary>
    /// Computes the view-to-clip projection matrix for the current frame.
    /// </summary>
    public Matrix4x4 GetProjectionMatrix() => Projection switch
    {
        CameraProjection.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(
            float.DegreesToRadians(FieldOfView),
            AspectRatio,
            NearPlane,
            FarPlane),
        CameraProjection.Orthographic => Matrix4x4.CreateOrthographic(
            OrthographicSize * AspectRatio,
            OrthographicSize,
            NearPlane,
            FarPlane),
        _ => Matrix4x4.Identity,
    };

    /// <summary>
    /// Builds a renderer-facing <see cref="CameraView"/> snapshot for the current frame.
    /// </summary>
    public CameraView GetView() => new(GetViewMatrix(), GetProjectionMatrix(), Position);

    /// <summary>
    /// Returns the normalized forward direction the camera is facing.
    /// </summary>
    public Vector3 GetForward()
    {
        Vector3 forward = Target - Position;
        return forward.LengthSquared() > 1e-8f ? Vector3.Normalize(forward) : -Vector3.UnitZ;
    }

    /// <summary>
    /// Returns the normalized right direction relative to the camera's orientation.
    /// </summary>
    public Vector3 GetRight()
    {
        Vector3 right = Vector3.Cross(GetForward(), Up);
        return right.LengthSquared() > 1e-8f ? Vector3.Normalize(right) : Vector3.UnitX;
    }

    /// <summary>
    /// Updates camera state from generic input for the current frame.
    /// </summary>
    /// <param name="deltaTime">Elapsed frame time in seconds.</param>
    /// <param name="input">Generic camera input payload.</param>
    public virtual void UpdateInput(float deltaTime, in CameraControllerInput input)
    {
    }
}
