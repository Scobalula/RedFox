using System;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// First-person camera driven by yaw/pitch look input and WASD-style local movement.
/// </summary>
public sealed class FpsCamera : Camera
{
    private float _yawRadians;
    private float _pitchRadians;

    /// <summary>
    /// Gets or sets the camera yaw in radians.
    /// </summary>
    public float YawRadians
    {
        get => _yawRadians;
        set
        {
            _yawRadians = value;
            ApplyOrientation();
        }
    }

    /// <summary>
    /// Gets or sets the camera pitch in radians.
    /// </summary>
    public float PitchRadians
    {
        get => _pitchRadians;
        set
        {
            _pitchRadians = value;
            ApplyOrientation();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether pitch should be clamped.
    /// </summary>
    public bool UsePitchLimits { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum pitch when <see cref="UsePitchLimits"/> is enabled.
    /// </summary>
    public float MinPitchRadians { get; set; } = -1.45f;

    /// <summary>
    /// Gets or sets the maximum pitch when <see cref="UsePitchLimits"/> is enabled.
    /// </summary>
    public float MaxPitchRadians { get; set; } = 1.45f;

    /// <summary>
    /// Initializes a new <see cref="FpsCamera"/>.
    /// </summary>
    public FpsCamera() : base("FpsCamera")
    {
        Projection = CameraProjection.Perspective;
        MoveSpeed = 4.0f;
        ApplyOrientation();
    }

    /// <summary>
    /// Initializes a new <see cref="FpsCamera"/> with the specified name.
    /// </summary>
    /// <param name="name">The camera name.</param>
    public FpsCamera(string name) : base(name)
    {
        Projection = CameraProjection.Perspective;
        MoveSpeed = 4.0f;
        ApplyOrientation();
    }

    /// <inheritdoc/>
    public override void UpdateInput(float deltaTime, in CameraControllerInput input)
    {
        if (deltaTime < 0.0f)
        {
            return;
        }

        float yawScale = InvertX ? -1.0f : 1.0f;
        float pitchScale = InvertY ? -1.0f : 1.0f;

        _yawRadians += input.LookDelta.X * LookSensitivity * yawScale;
        _pitchRadians += input.LookDelta.Y * LookSensitivity * pitchScale;

        if (UsePitchLimits)
        {
            _pitchRadians = Math.Clamp(_pitchRadians, MinPitchRadians, MaxPitchRadians);
        }

        ApplyOrientation();

        Vector3 forward = GetForward();
        Vector3 right = GetRight();
        Vector3 up = Up;

        Vector3 move = (input.MoveIntent.X * right)
            + (input.MoveIntent.Y * up)
            + (input.MoveIntent.Z * forward);

        // Wheel zoom and drag dolly both translate the camera forward at MoveSpeed scale.
        float dolly = (input.ZoomDelta + input.DollyDelta) * ZoomSensitivity;
        if (dolly != 0.0f)
        {
            move += forward * dolly;
        }

        if (move.LengthSquared() > 1e-8f)
        {
            float speed = MoveSpeed;
            Position += Vector3.Normalize(move) * speed * deltaTime;
            Target = Position + forward;
        }
    }

    private void ApplyOrientation()
    {
        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(_yawRadians, _pitchRadians, 0.0f);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);

        Target = Position + forward;
        Up = up;
    }
}
