using System;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// A camera that orbits around <see cref="OrbitTarget"/> using yaw, pitch, and distance.
/// Uses a multiplicative ("exponential") dolly zoom that feels consistent at any scale,
/// and clamps cleanly to <see cref="MinDistance"/>/<see cref="MaxDistance"/> without
/// drifting the orbit pivot.
/// </summary>
public class OrbitCamera : Camera
{
    private float _yawRadians;
    private float _pitchRadians;
    private float _distance = 5.0f;

    // Pending input deltas that are smoothed in over multiple frames.
    private Vector2 _pendingLook;
    private Vector2 _pendingPan;
    private float _pendingZoom;

    /// <summary>
    /// Gets or sets the world-space point the camera orbits around.
    /// </summary>
    public Vector3 OrbitTarget { get; set; } = Vector3.Zero;

    /// <summary>
    /// Gets or sets the orbit yaw in radians.
    /// </summary>
    public float YawRadians
    {
        get => _yawRadians;
        set => _yawRadians = value;
    }

    /// <summary>
    /// Gets or sets the orbit pitch in radians.
    /// </summary>
    public float PitchRadians
    {
        get => _pitchRadians;
        set => _pitchRadians = value;
    }

    /// <summary>
    /// Gets or sets the camera distance from <see cref="OrbitTarget"/>.
    /// Always clamped to <c>[<see cref="MinDistance"/>, <see cref="MaxDistance"/>]</c>.
    /// </summary>
    public float Distance
    {
        get => _distance;
        set => _distance = Math.Clamp(value, MinDistance, MaxDistance);
    }

    /// <summary>
    /// Gets or sets the minimum allowed orbit distance.
    /// </summary>
    public float MinDistance { get; set; } = 0.05f;

    /// <summary>
    /// Gets or sets the maximum allowed orbit distance.
    /// </summary>
    public float MaxDistance { get; set; } = 10000.0f;

    /// <summary>
    /// Gets or sets a value indicating whether pitch should be clamped to
    /// <see cref="MinPitchRadians"/> / <see cref="MaxPitchRadians"/>.
    /// </summary>
    public bool UsePitchLimits { get; set; }

    /// <summary>
    /// Gets or sets the minimum pitch when <see cref="UsePitchLimits"/> is enabled.
    /// </summary>
    public float MinPitchRadians { get; set; } = -1.5f;

    /// <summary>
    /// Gets or sets the maximum pitch when <see cref="UsePitchLimits"/> is enabled.
    /// </summary>
    public float MaxPitchRadians { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets a value indicating whether camera input is smoothed over time.
    /// When enabled, raw input deltas are accumulated and applied gradually
    /// using exponential decay.
    /// </summary>
    public bool EnableInputSmoothing { get; set; } = true;

    /// <summary>
    /// Gets or sets the smoothing sharpness. Higher values feel snappier,
    /// lower values feel heavier. Used as the rate constant in
    /// <c>1 - exp(-sharpness * deltaTime)</c>.
    /// </summary>
    public float InputSmoothingSharpness { get; set; } = 18.0f;

    /// <summary>
    /// Initializes a new <see cref="OrbitCamera"/>.
    /// </summary>
    public OrbitCamera() : base("OrbitCamera")
    {
        ApplyOrbit();
    }

    /// <summary>
    /// Initializes a new <see cref="OrbitCamera"/> with the specified name.
    /// </summary>
    /// <param name="name">The camera name.</param>
    public OrbitCamera(string name) : base(name)
    {
        ApplyOrbit();
    }

    /// <summary>
    /// Recomputes <see cref="Camera.Position"/>, <see cref="Camera.Target"/> and
    /// <see cref="Camera.Up"/> from the current orbit state. Call this after
    /// changing <see cref="OrbitTarget"/>, <see cref="YawRadians"/>,
    /// <see cref="PitchRadians"/>, or <see cref="Distance"/> directly.
    /// </summary>
    public void ApplyOrbit()
    {
        if (UsePitchLimits)
        {
            _pitchRadians = Math.Clamp(_pitchRadians, MinPitchRadians, MaxPitchRadians);
        }

        _distance = Math.Clamp(_distance, MinDistance, MaxDistance);

        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(_yawRadians, _pitchRadians, 0.0f);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);

        Position = OrbitTarget - (forward * _distance);
        Target = OrbitTarget;
        Up = up;
    }

    /// <summary>
    /// Adds yaw and pitch deltas (in radians) to the orbit and reapplies it.
    /// </summary>
    /// <param name="yawDeltaRadians">The yaw delta in radians.</param>
    /// <param name="pitchDeltaRadians">The pitch delta in radians.</param>
    public void AddOrbit(float yawDeltaRadians, float pitchDeltaRadians)
    {
        _yawRadians += yawDeltaRadians;
        _pitchRadians += pitchDeltaRadians;
        ApplyOrbit();
    }

    /// <summary>
    /// Applies a multiplicative zoom step. Positive <paramref name="zoomDelta"/>
    /// zooms in (decreases distance), negative zooms out. Distance is clamped to
    /// <see cref="MinDistance"/>/<see cref="MaxDistance"/>; the orbit pivot is
    /// never moved, so zoom always stays consistent.
    /// </summary>
    /// <param name="zoomDelta">The zoom amount (e.g. scroll wheel ticks).</param>
    public void AddZoom(float zoomDelta)
    {
        if (zoomDelta == 0.0f)
        {
            return;
        }

        // Multiplicative dolly: distance *= exp(-delta * sensitivity).
        // Exponent clamp keeps a single oversized event from teleporting the camera.
        float exponent = Math.Clamp(-zoomDelta * ZoomSensitivity, -4.0f, 4.0f);
        float proposed = _distance * MathF.Exp(exponent);
        _distance = Math.Clamp(proposed, MinDistance, MaxDistance);
        ApplyOrbit();
    }

    /// <summary>
    /// Pans the orbit target in screen space. <paramref name="screenDelta"/>.X moves
    /// right, .Y moves up. Pan amount automatically scales with the current orbit
    /// distance so it feels consistent at any zoom level.
    /// </summary>
    /// <param name="screenDelta">The screen-space pan delta.</param>
    public void AddPan(Vector2 screenDelta)
    {
        if (screenDelta == Vector2.Zero)
        {
            return;
        }

        float scale = _distance * PanSensitivity;
        Vector3 right = GetRight();
        Vector3 up = Up;
        OrbitTarget += ((right * screenDelta.X) + (up * screenDelta.Y)) * scale;
        ApplyOrbit();
    }

    /// <summary>
    /// Frames the camera on a sphere of <paramref name="radius"/> centered at
    /// <paramref name="target"/> using the current field of view and aspect ratio.
    /// </summary>
    /// <param name="target">The world-space focus point.</param>
    /// <param name="radius">The radius of the sphere to fit in view.</param>
    public void Frame(Vector3 target, float radius)
    {
        radius = MathF.Max(radius, 1e-3f);
        OrbitTarget = target;

        float verticalFov = float.DegreesToRadians(MathF.Max(FieldOfView, 1.0f));
        float halfVertical = verticalFov * 0.5f;
        float halfHorizontal = MathF.Atan(MathF.Tan(halfVertical) * MathF.Max(AspectRatio, 1e-3f));
        float limitingHalfFov = MathF.Min(halfVertical, halfHorizontal);
        float fitDistance = radius / MathF.Max(MathF.Sin(limitingHalfFov), 1e-4f);

        _distance = Math.Clamp(fitDistance, MinDistance, MaxDistance);
        _pendingLook = Vector2.Zero;
        _pendingPan = Vector2.Zero;
        _pendingZoom = 0.0f;
        ApplyOrbit();
    }

    /// <inheritdoc/>
    public override void UpdateInput(float deltaTime, in CameraControllerInput input)
    {
        if (deltaTime < 0.0f)
        {
            return;
        }

        // Wheel zoom and drag-dolly both contribute to the same multiplicative zoom.
        float rawZoom = input.ZoomDelta + input.DollyDelta;

        if (!EnableInputSmoothing || deltaTime <= 0.0f)
        {
            ApplyLook(input.LookDelta);
            ApplyZoom(rawZoom);
            ApplyPan(input.PanDelta);
        }
        else
        {
            // Accumulate raw deltas, then bleed off a portion this frame.
            // alpha = 1 - exp(-sharpness * dt) so the curve is frame-rate independent.
            _pendingLook += input.LookDelta;
            _pendingPan += input.PanDelta;
            _pendingZoom += rawZoom;

            float alpha = 1.0f - MathF.Exp(-MathF.Max(InputSmoothingSharpness, 0.0f) * deltaTime);
            if (alpha < 0.0f)
            {
                alpha = 0.0f;
            }
            else if (alpha > 1.0f)
            {
                alpha = 1.0f;
            }

            Vector2 lookStep = _pendingLook * alpha;
            Vector2 panStep = _pendingPan * alpha;
            float zoomStep = _pendingZoom * alpha;

            _pendingLook -= lookStep;
            _pendingPan -= panStep;
            _pendingZoom -= zoomStep;

            // Snap residuals to zero once they're imperceptible to avoid endless tiny updates.
            if (_pendingLook.LengthSquared() < 1e-10f)
            {
                _pendingLook = Vector2.Zero;
            }
            if (_pendingPan.LengthSquared() < 1e-10f)
            {
                _pendingPan = Vector2.Zero;
            }
            if (MathF.Abs(_pendingZoom) < 1e-5f)
            {
                _pendingZoom = 0.0f;
            }

            ApplyLook(lookStep);
            ApplyZoom(zoomStep);
            ApplyPan(panStep);
        }

        // WASD-style movement is direct (not smoothed) since it's already a velocity.
        if (input.MoveIntent != Vector3.Zero)
        {
            Vector3 forward = GetForward();
            Vector3 right = GetRight();
            Vector3 up = Up;

            Vector3 move = (input.MoveIntent.X * right)
                + (input.MoveIntent.Y * up)
                + (input.MoveIntent.Z * forward);

            if (move.LengthSquared() > 1e-8f)
            {
                float speed = MoveSpeed;
                OrbitTarget += Vector3.Normalize(move) * speed * deltaTime;
                ApplyOrbit();
            }
        }
    }

    private void ApplyLook(Vector2 lookDelta)
    {
        if (lookDelta == Vector2.Zero)
        {
            return;
        }

        float yawScale = InvertX ? -1.0f : 1.0f;
        float pitchScale = InvertY ? -1.0f : 1.0f;
        AddOrbit(lookDelta.X * LookSensitivity * yawScale, lookDelta.Y * LookSensitivity * pitchScale);
    }

    private void ApplyZoom(float zoomDelta)
    {
        if (zoomDelta != 0.0f)
        {
            AddZoom(zoomDelta);
        }
    }

    private void ApplyPan(Vector2 panDelta)
    {
        if (panDelta != Vector2.Zero)
        {
            AddPan(panDelta);
        }
    }
}