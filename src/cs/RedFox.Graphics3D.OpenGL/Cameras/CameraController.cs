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
    public float ZoomSensitivity { get; set; } = 0.15f;
    public float MoveSpeed { get; set; } = 4.5f;
    public float FastMoveMultiplier { get; set; } = 3.0f;
    public Vector3 FocusPoint => _focusPoint;
    public float Distance => _distance;

    public void Fit(Vector3 center, float radius)
    {
        _focusPoint = center;
        _distance = MathF.Max(radius * 2.5f, 1.0f);
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
        _distance = MathF.Max(Vector3.Distance(_camera.Position, _camera.Target), 0.001f);
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
        Vector3 forward = GetForward();
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
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

        _distance = MathF.Max(0.1f, _distance - (direction * MoveSpeed * deltaTime));
    }

    private void Orbit(Vector2 mouseDelta)
    {
        _yaw += mouseDelta.X * OrbitSensitivity;
        _pitch = Math.Clamp(_pitch - mouseDelta.Y * OrbitSensitivity, -HalfPiMinusEpsilon, HalfPiMinusEpsilon);
    }

    private void Pan(Vector2 mouseDelta)
    {
        Vector3 forward = GetForward();
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        float panScale = MathF.Max(_distance, 1.0f) * PanSensitivity;

        _focusPoint += (-right * mouseDelta.X + up * mouseDelta.Y) * panScale;
    }

    private void Zoom(float wheelDelta)
    {
        float zoomAmount = MathF.Max(_distance, 1.0f) * wheelDelta * ZoomSensitivity;
        _distance = Math.Clamp(_distance - zoomAmount, 0.1f, 5000f);
    }

    private void ApplyOrbitCamera()
    {
        Vector3 forward = GetForward();
        _camera.Target = _focusPoint;
        _camera.Position = _focusPoint - (forward * _distance);
        _camera.Up = Vector3.UnitY;
    }

    private Vector3 GetForward()
    {
        float cosPitch = MathF.Cos(_pitch);
        return Vector3.Normalize(new Vector3(
            MathF.Sin(_yaw) * cosPitch,
            MathF.Sin(_pitch),
            -MathF.Cos(_yaw) * cosPitch));
    }
}
