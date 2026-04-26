using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RedFox.Graphics3D.Rendering.Hosting;
using System.Numerics;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Adapts Avalonia pointer and keyboard input into renderer-agnostic camera controller input.
/// </summary>
public sealed class AvaloniaCameraInputAdapter : IInputSource, IDisposable
{
    private readonly Control _control;
    private readonly HashSet<Key> _pressedKeys = [];
    private bool _disposed;
    private bool _hasLastPointerPosition;
    private bool _isLeftPressed;
    private bool _isMiddlePressed;
    private bool _isRightPressed;
    private KeyModifiers _keyModifiers;
    private Vector2 _lastPointerPosition;
    private Vector2 _pointerDelta;
    private float _wheelDelta;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaCameraInputAdapter"/> class.
    /// </summary>
    /// <param name="control">The control that receives input events.</param>
    public AvaloniaCameraInputAdapter(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _control.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _control.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _control.LostFocus += OnLostFocus;
    }

    /// <summary>
    /// Gets or sets whether Alt must be held for pointer camera gestures.
    /// </summary>
    public bool RequireAltForPointerGestures { get; set; } = true;

    /// <summary>
    /// Gets or sets the look sensitivity multiplier applied to pointer deltas.
    /// </summary>
    public float LookSensitivity { get; set; } = 0.0052f;

    /// <summary>
    /// Gets or sets the zoom sensitivity multiplier applied to wheel deltas.
    /// </summary>
    public float ZoomSensitivity { get; set; } = 0.25f;

    /// <summary>
    /// Gets or sets the pan sensitivity multiplier applied to pointer deltas.
    /// </summary>
    public float PanSensitivity { get; set; } = 0.0022f;

    /// <summary>
    /// Gets or sets the dolly sensitivity multiplier applied to pointer deltas.
    /// </summary>
    public float DollySensitivity { get; set; } = 0.014f;

    /// <inheritdoc/>
    public CameraControllerInput ReadInput()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Vector2 delta = _pointerDelta;
        float wheel = _wheelDelta;
        _pointerDelta = Vector2.Zero;
        _wheelDelta = 0.0f;

        Vector2 lookDelta = Vector2.Zero;
        Vector2 panDelta = Vector2.Zero;
        float dollyDelta = 0.0f;
        if (IsPointerGestureActive())
        {
            if (_isLeftPressed)
            {
                lookDelta = delta * LookSensitivity;
            }

            if (_isMiddlePressed)
            {
                panDelta = new Vector2(-delta.X, delta.Y) * PanSensitivity;
            }

            if (_isRightPressed)
            {
                dollyDelta = delta.Y * DollySensitivity;
            }
        }

        return new CameraControllerInput(lookDelta, wheel * ZoomSensitivity, panDelta, dollyDelta, ReadMoveIntent());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _control.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        _control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        _control.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _control.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        _control.LostFocus -= OnLostFocus;
        _pressedKeys.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        Point position = e.GetPosition(_control);
        Vector2 current = new((float)position.X, (float)position.Y);
        if (!_hasLastPointerPosition)
        {
            _lastPointerPosition = current;
            _hasLastPointerPosition = true;
            return;
        }

        _pointerDelta += current - _lastPointerPosition;
        _lastPointerPosition = current;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _control.Focus(NavigationMethod.Pointer);
        _keyModifiers = e.KeyModifiers;
        PointerPoint point = e.GetCurrentPoint(_control);
        e.Pointer.Capture(_control);
        Point position = e.GetPosition(_control);
        _lastPointerPosition = new Vector2((float)position.X, (float)position.Y);
        _hasLastPointerPosition = true;
        _isLeftPressed |= point.Properties.IsLeftButtonPressed;
        _isMiddlePressed |= point.Properties.IsMiddleButtonPressed;
        _isRightPressed |= point.Properties.IsRightButtonPressed;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        PointerPoint point = e.GetCurrentPoint(_control);
        _isLeftPressed = point.Properties.IsLeftButtonPressed;
        _isMiddlePressed = point.Properties.IsMiddleButtonPressed;
        _isRightPressed = point.Properties.IsRightButtonPressed;
        if (!_isLeftPressed && !_isMiddlePressed && !_isRightPressed)
        {
            e.Pointer.Capture(null);
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        _wheelDelta += (float)e.Delta.Y;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        _pressedKeys.Add(e.Key);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        _pressedKeys.Remove(e.Key);
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _pressedKeys.Clear();
        _keyModifiers = KeyModifiers.None;
        _isLeftPressed = false;
        _isMiddlePressed = false;
        _isRightPressed = false;
        _hasLastPointerPosition = false;
        _pointerDelta = Vector2.Zero;
        _wheelDelta = 0.0f;
    }

    private bool IsPointerGestureActive()
    {
        if (!RequireAltForPointerGestures)
        {
            return true;
        }

        return _keyModifiers.HasFlag(KeyModifiers.Alt)
            || _pressedKeys.Contains(Key.LeftAlt)
            || _pressedKeys.Contains(Key.RightAlt);
    }

    private Vector3 ReadMoveIntent()
    {
        Vector3 intent = Vector3.Zero;
        if (_pressedKeys.Contains(Key.W))
        {
            intent.Z += 1.0f;
        }

        if (_pressedKeys.Contains(Key.S))
        {
            intent.Z -= 1.0f;
        }

        if (_pressedKeys.Contains(Key.D))
        {
            intent.X += 1.0f;
        }

        if (_pressedKeys.Contains(Key.A))
        {
            intent.X -= 1.0f;
        }

        if (_pressedKeys.Contains(Key.E))
        {
            intent.Y += 1.0f;
        }

        if (_pressedKeys.Contains(Key.Q))
        {
            intent.Y -= 1.0f;
        }

        return intent;
    }
}
