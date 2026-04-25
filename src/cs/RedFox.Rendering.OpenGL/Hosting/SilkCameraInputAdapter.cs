using Silk.NET.Input;
using System;
using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Hosting;

namespace RedFox.Rendering.OpenGL.Hosting;

/// <summary>
/// Adapts Silk.NET keyboard and mouse input into renderer-agnostic camera controller input.
/// </summary>
public sealed class SilkCameraInputAdapter : IInputSource, IDisposable
{
    private readonly IKeyboard? _keyboard;
    private readonly IMouse? _mouse;

    private bool _disposed;
    private bool _hasLastMousePosition;
    private Vector2 _lastMousePosition;
    private float _pendingScrollDelta;

    /// <summary>Gets or sets whether Alt must be held for mouse camera gestures.</summary>
    public bool RequireAltForMouseGestures { get; set; } = true;

    /// <summary>Gets or sets the look sensitivity multiplier applied to mouse deltas.</summary>
    public float LookSensitivity { get; set; } = 0.008f;

    /// <summary>Gets or sets the zoom sensitivity multiplier applied to wheel deltas.</summary>
    public float ZoomSensitivity { get; set; } = 1.0f;

    /// <summary>Gets or sets the pan sensitivity multiplier applied to mouse drag deltas.</summary>
    public float PanSensitivity { get; set; } = 0.008f;

    /// <summary>Gets or sets the dolly sensitivity multiplier applied to mouse drag deltas.</summary>
    public float DollySensitivity { get; set; } = 0.02f;

    /// <summary>
    /// Initializes a new Silk camera input adapter.
    /// </summary>
    /// <param name="inputContext">The active Silk input context.</param>
    public SilkCameraInputAdapter(IInputContext inputContext)
    {
        ArgumentNullException.ThrowIfNull(inputContext);
        _keyboard = inputContext.Keyboards.Count > 0 ? inputContext.Keyboards[0] : null;
        _mouse = inputContext.Mice.Count > 0 ? inputContext.Mice[0] : null;
        if (_mouse is not null)
        {
            _mouse.Scroll += OnMouseScroll;
        }
    }

    /// <summary>
    /// Samples the current input state and produces camera controller input.
    /// </summary>
    /// <returns>The sampled camera controller input for the current frame.</returns>
    public CameraControllerInput ReadInput()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Vector2 mouseDelta = ReadMouseDelta();
        Vector2 lookDelta = Vector2.Zero;
        Vector2 panDelta = Vector2.Zero;
        float dollyDelta = 0.0f;

        if (IsAltGestureActive())
        {
            if (_mouse is not null && _mouse.IsButtonPressed(MouseButton.Left))
            {
                lookDelta = mouseDelta * LookSensitivity;
            }

            if (_mouse is not null && _mouse.IsButtonPressed(MouseButton.Middle))
            {
                panDelta = new Vector2(-mouseDelta.X, mouseDelta.Y) * PanSensitivity;
            }

            if (_mouse is not null && _mouse.IsButtonPressed(MouseButton.Right))
            {
                dollyDelta = mouseDelta.Y * DollySensitivity;
            }
        }

        float zoomDelta = _pendingScrollDelta * ZoomSensitivity;
        _pendingScrollDelta = 0.0f;
        Vector3 moveIntent = ReadMoveIntent();
        return new CameraControllerInput(lookDelta, zoomDelta, panDelta, dollyDelta, moveIntent);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_mouse is not null)
        {
            _mouse.Scroll -= OnMouseScroll;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private Vector2 ReadMouseDelta()
    {
        if (_mouse is null)
        {
            return Vector2.Zero;
        }

        Vector2 position = _mouse.Position;
        if (!_hasLastMousePosition)
        {
            _lastMousePosition = position;
            _hasLastMousePosition = true;
            return Vector2.Zero;
        }

        Vector2 delta = position - _lastMousePosition;
        _lastMousePosition = position;
        if (delta.LengthSquared() <= 0.0f)
        {
            return Vector2.Zero;
        }

        return delta;
    }

    private bool IsAltGestureActive()
    {
        if (!RequireAltForMouseGestures)
        {
            return true;
        }

        if (_keyboard is null)
        {
            return false;
        }

        return _keyboard.IsKeyPressed(Key.AltLeft) || _keyboard.IsKeyPressed(Key.AltRight);
    }

    private Vector3 ReadMoveIntent()
    {
        if (_keyboard is null)
        {
            return Vector3.Zero;
        }

        Vector3 intent = Vector3.Zero;
        if (_keyboard.IsKeyPressed(Key.W))
        {
            intent.Z += 1.0f;
        }

        if (_keyboard.IsKeyPressed(Key.S))
        {
            intent.Z -= 1.0f;
        }

        if (_keyboard.IsKeyPressed(Key.D))
        {
            intent.X += 1.0f;
        }

        if (_keyboard.IsKeyPressed(Key.A))
        {
            intent.X -= 1.0f;
        }

        if (_keyboard.IsKeyPressed(Key.E))
        {
            intent.Y += 1.0f;
        }

        if (_keyboard.IsKeyPressed(Key.Q))
        {
            intent.Y -= 1.0f;
        }

        return intent;
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        _pendingScrollDelta += scrollWheel.Y;
    }
}
