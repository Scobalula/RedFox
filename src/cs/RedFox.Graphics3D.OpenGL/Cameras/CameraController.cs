using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Cameras;

/// <summary>
/// Specifies the interaction mode for a <see cref="CameraController"/>.
/// </summary>
public enum CameraMode
{
    /// <summary>
    /// Arcball orbiting with left mouse, panning with middle mouse or shift+left.
    /// </summary>
    Arcball,
    /// <summary>
    /// Blender-style controls: orbit with middle mouse, pan with shift+middle, dolly with right mouse.
    /// </summary>
    Blender,
    /// <summary>
    /// First-person shooter style free-flight camera.
    /// </summary>
    Fps
}

/// <summary>
/// Encapsulates the current input state consumed by <see cref="CameraController.Update"/>.
/// </summary>
/// <param name="MouseDelta">Relative mouse movement since the last frame.</param>
/// <param name="WheelDelta">Accumulated mouse wheel delta.</param>
/// <param name="LeftMouseDown">Whether the left mouse button is held.</param>
/// <param name="MiddleMouseDown">Whether the middle mouse button is held.</param>
/// <param name="RightMouseDown">Whether the right mouse button is held.</param>
/// <param name="ShiftModifier">Whether the Shift key is held.</param>
/// <param name="MoveForward">Whether the forward movement input is active.</param>
/// <param name="MoveBackward">Whether the backward movement input is active.</param>
/// <param name="MoveLeft">Whether the left movement input is active.</param>
/// <param name="MoveRight">Whether the right movement input is active.</param>
/// <param name="MoveUp">Whether the upward movement input is active.</param>
/// <param name="MoveDown">Whether the downward movement input is active.</param>
/// <param name="FastMoveModifier">Whether the fast-move modifier input is active.</param>
public readonly record struct CameraInputState(
    Vector2 MouseDelta,
    float WheelDelta,
    bool LeftMouseDown,
    bool MiddleMouseDown,
    bool RightMouseDown,
    bool ShiftModifier,
    bool MoveForward,
    bool MoveBackward,
    bool MoveLeft,
    bool MoveRight,
    bool MoveUp,
    bool MoveDown,
    bool FastMoveModifier);

/// <summary>
/// Translates user input into camera position and orientation changes for an orbiting or free-flight camera.
/// </summary>
public sealed class CameraController
{
    private const float HalfPiMinusEpsilon = 1.5607964f;
    private const float MinimumDistance = 0.01f;

    private readonly Camera _camera;
    private float _yaw;
    private float _pitch;
    private float _distance;
    private Vector3 _focusPoint;

    /// <summary>
    /// Initializes a new <see cref="CameraController"/> that drives the specified <paramref name="camera"/>.
    /// </summary>
    /// <param name="camera">The camera to control.</param>
    public CameraController(Camera camera)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        SynchronizeFromCamera();
    }

    /// <summary>
    /// Gets the <see cref="Camera"/> instance controlled by this controller.
    /// </summary>
    public Camera Camera => _camera;

    /// <summary>
    /// Gets or sets the camera interaction mode.
    /// </summary>
    public CameraMode Mode { get; set; } = CameraMode.Arcball;

    /// <summary>
    /// Gets or sets the sensitivity applied to orbit rotations.
    /// </summary>
    public float OrbitSensitivity { get; set; } = 0.01f;

    /// <summary>
    /// Gets or sets the sensitivity applied to panning.
    /// </summary>
    public float PanSensitivity { get; set; } = 0.0025f;

    /// <summary>
    /// Gets or sets the sensitivity applied to zoom via the mouse wheel.
    /// </summary>
    public float ZoomSensitivity { get; set; } = 0.12f;

    /// <summary>
    /// Gets or sets the base movement speed used in FPS mode.
    /// </summary>
    public float MoveSpeed { get; set; } = 4.5f;

    /// <summary>
    /// Gets or sets the multiplier applied to <see cref="MoveSpeed"/> when the fast-move modifier is active.
    /// </summary>
    public float FastMoveMultiplier { get; set; } = 3.0f;

    /// <summary>
    /// Gets the point in world space around which the camera orbits.
    /// </summary>
    public Vector3 FocusPoint => _focusPoint;

    /// <summary>
    /// Gets the current distance between the camera and the focus point.
    /// </summary>
    public float Distance => _distance;

    /// <summary>
    /// Positions the camera so that a sphere of the given <paramref name="radius"/> centered at
    /// <paramref name="center"/> is fully visible.
    /// </summary>
    /// <param name="center">The world-space center of the bounding sphere.</param>
    /// <param name="radius">The radius of the bounding sphere.</param>
    public void Fit(Vector3 center, float radius)
    {
        _focusPoint = center;

        if (_camera.Projection == CameraProjection.Orthographic)
        {
            _camera.OrthographicSize = MathF.Max(radius * 2.2f, 0.1f);
            _distance = MathF.Max(radius * 2.5f, 1.0f);
            ApplyOrbitCamera();
            return;
        }

        float verticalHalfFov = float.DegreesToRadians(MathF.Max(_camera.FieldOfView, 1.0f)) * 0.5f;
        float horizontalHalfFov = MathF.Atan(MathF.Tan(verticalHalfFov) * MathF.Max(_camera.AspectRatio, 1e-3f));
        float limitingHalfFov = MathF.Max(MathF.Min(verticalHalfFov, horizontalHalfFov), 0.1f);
        _distance = MathF.Max((radius / MathF.Sin(limitingHalfFov)) * 1.05f, radius + 0.1f);
        ApplyOrbitCamera();
    }

    /// <summary>
    /// Recalculates internal yaw, pitch, focus point, and distance from the camera's current position and target.
    /// </summary>
    public void SynchronizeFromCamera()
    {
        Vector3 forward = Vector3.Normalize(_camera.Target - _camera.Position);
        if (!float.IsFinite(forward.X) || !float.IsFinite(forward.Y) || !float.IsFinite(forward.Z))
            forward = -Vector3.UnitZ;

        _pitch = Math.Clamp(MathF.Asin(forward.Y), -HalfPiMinusEpsilon, HalfPiMinusEpsilon);
        _yaw = MathF.Atan2(forward.X, -forward.Z);
        _focusPoint = _camera.Target;
        _distance = MathF.Max(Vector3.Distance(_camera.Position, _camera.Target), MinimumDistance);
    }

    /// <summary>
    /// Applies the given <paramref name="input"/> state to update the camera for the current frame.
    /// </summary>
    /// <param name="input">The current input state.</param>
    /// <param name="deltaTime">The time elapsed since the last frame, in seconds.</param>
    public void Update(in CameraInputState input, float deltaTime)
    {
        switch (Mode)
        {
            case CameraMode.Arcball:
                UpdateArcball(input);
                break;

            case CameraMode.Blender:
                UpdateBlender(input, deltaTime);
                break;

            case CameraMode.Fps:
                UpdateFps(input, deltaTime);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void UpdateArcball(in CameraInputState input)
    {
        if (input.LeftMouseDown && !input.ShiftModifier)
            Orbit(input.MouseDelta);

        if (input.MiddleMouseDown || (input.LeftMouseDown && input.ShiftModifier))
            Pan(input.MouseDelta);

        if (MathF.Abs(input.WheelDelta) > float.Epsilon)
            Zoom(input.WheelDelta);

        ApplyOrbitCamera();
    }

    private void UpdateBlender(in CameraInputState input, float deltaTime)
    {
        if (input.MiddleMouseDown && !input.ShiftModifier)
            Orbit(input.MouseDelta);

        if (input.MiddleMouseDown && input.ShiftModifier)
            Pan(input.MouseDelta);

        if (input.RightMouseDown)
            DollyFromKeyboard(input, deltaTime);

        if (MathF.Abs(input.WheelDelta) > float.Epsilon)
            Zoom(input.WheelDelta);

        ApplyOrbitCamera();
    }

    private void UpdateFps(in CameraInputState input, float deltaTime)
    {
        if (input.RightMouseDown)
        {
            _yaw += input.MouseDelta.X * OrbitSensitivity;
            _pitch = Math.Clamp(_pitch - input.MouseDelta.Y * OrbitSensitivity, -HalfPiMinusEpsilon, HalfPiMinusEpsilon);
        }

        float moveSpeed = MoveSpeed * deltaTime * (input.FastMoveModifier ? FastMoveMultiplier : 1.0f);
        GetOrbitBasis(out Vector3 forward, out Vector3 right, out _);
        Vector3 movement = Vector3.Zero;

        if (input.MoveForward) movement += forward;
        if (input.MoveBackward) movement -= forward;
        if (input.MoveRight) movement += right;
        if (input.MoveLeft) movement -= right;
        if (input.MoveUp) movement += Vector3.UnitY;
        if (input.MoveDown) movement -= Vector3.UnitY;

        if (movement != Vector3.Zero)
            movement = Vector3.Normalize(movement) * moveSpeed;

        _camera.Position += movement;
        _camera.Target = _camera.Position + forward;
        _focusPoint = _camera.Target;
    }

    private void DollyFromKeyboard(in CameraInputState input, float deltaTime)
    {
        float direction = 0f;
        if (input.MoveForward) direction += 1f;
        if (input.MoveBackward) direction -= 1f;

        if (MathF.Abs(direction) < float.Epsilon)
            return;

        ApplyDolly(direction * MoveSpeed * deltaTime);
    }

    private void Orbit(Vector2 mouseDelta)
    {
        _yaw += mouseDelta.X * OrbitSensitivity;
        _pitch = Math.Clamp(_pitch - mouseDelta.Y * OrbitSensitivity, -HalfPiMinusEpsilon, HalfPiMinusEpsilon);
    }

    private void Pan(Vector2 mouseDelta)
    {
        GetOrbitBasis(out _, out Vector3 right, out Vector3 up);
        float distanceScale = _camera.Projection == CameraProjection.Orthographic
            ? MathF.Max(_camera.OrthographicSize, 0.25f)
            : MathF.Max(_distance, 0.25f);
        float fovScale = _camera.Projection == CameraProjection.Orthographic
            ? 1.0f
            : MathF.Tan(float.DegreesToRadians(MathF.Max(_camera.FieldOfView, 1.0f)) * 0.5f) * 2.0f;
        float panScale = distanceScale * fovScale * PanSensitivity;

        _focusPoint += (-right * mouseDelta.X + up * mouseDelta.Y) * panScale;
    }

    private void Zoom(float wheelDelta)
    {
        if (MathF.Abs(wheelDelta) < float.Epsilon)
            return;

        float zoomFactor = MathF.Exp(wheelDelta * ZoomSensitivity);
        float dollyAmount = MathF.Max(_distance, 0.1f) * (1.0f - (1.0f / zoomFactor));
        ApplyDolly(dollyAmount);
    }

    private void ApplyOrbitCamera()
    {
        GetOrbitBasis(out Vector3 forward, out _, out Vector3 up);
        _camera.Target = _focusPoint;
        _camera.Position = _focusPoint - (forward * _distance);
        _camera.Up = up;
    }

    private Vector3 GetForward()
    {
        float cosPitch = MathF.Cos(_pitch);
        return Vector3.Normalize(new Vector3(
            MathF.Sin(_yaw) * cosPitch,
            MathF.Sin(_pitch),
            -MathF.Cos(_yaw) * cosPitch));
    }

    private Vector3 GetRight()
    {
        // Keep the horizontal orbit axis driven by yaw alone so it stays stable
        // as we approach a top-down view instead of collapsing at the pole.
        Vector3 right = new(
            MathF.Cos(_yaw),
            0.0f,
            MathF.Sin(_yaw));

        return NormalizeOrFallback(right, Vector3.UnitX);
    }

    private void GetOrbitBasis(out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        forward = GetForward();
        right = GetRight();
        up = Vector3.Cross(right, forward);
        up = NormalizeOrFallback(up, Vector3.UnitY);
    }

    private void ApplyDolly(float amount)
    {
        if (MathF.Abs(amount) < float.Epsilon)
            return;

        float desiredDistance = _distance - amount;
        if (desiredDistance >= MinimumDistance)
        {
            _distance = desiredDistance;
            return;
        }

        float forwardTravel = MinimumDistance - desiredDistance;
        _distance = MinimumDistance;

        if (amount > 0.0f)
            _focusPoint += GetForward() * forwardTravel;
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        return value.LengthSquared() > 1e-8f ? Vector3.Normalize(value) : fallback;
    }
}
