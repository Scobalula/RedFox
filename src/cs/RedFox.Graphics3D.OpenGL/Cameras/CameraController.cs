using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Cameras;

public enum CameraMode
{
    Arcball,
    Blender,
    Fps
}

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

public sealed class CameraController
{
    private const float HalfPiMinusEpsilon = 1.5607964f;
    private const float MinimumDistance = 0.01f;

    private readonly Camera _camera;
    private float _yaw;
    private float _pitch;
    private float _distance;
    private Vector3 _focusPoint;

    public CameraController(Camera camera)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        SynchronizeFromCamera();
    }

    public Camera Camera => _camera;
    public CameraMode Mode { get; set; } = CameraMode.Arcball;
    public float OrbitSensitivity { get; set; } = 0.01f;
    public float PanSensitivity { get; set; } = 0.0025f;
    public float ZoomSensitivity { get; set; } = 0.12f;
    public float MoveSpeed { get; set; } = 4.5f;
    public float FastMoveMultiplier { get; set; } = 3.0f;
    public Vector3 FocusPoint => _focusPoint;
    public float Distance => _distance;

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
