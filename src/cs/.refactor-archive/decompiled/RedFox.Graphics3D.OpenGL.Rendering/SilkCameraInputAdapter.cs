using System;
using System.Numerics;
using Silk.NET.Input;

namespace RedFox.Graphics3D.OpenGL.Rendering;

/// <summary>
/// Adapts Silk.NET keyboard and mouse input into renderer-agnostic camera controller input.
/// </summary>
public sealed class SilkCameraInputAdapter : IDisposable
{
	private readonly IKeyboard? _keyboard;

	private readonly IMouse? _mouse;

	private bool _disposed;

	private bool _hasLastMousePosition;

	private Vector2 _lastMousePosition;

	private float _pendingScrollDelta;

	/// <summary>
	/// Gets or sets whether Alt must be held for mouse camera gestures.
	/// </summary>
	public bool RequireAltForMouseGestures { get; set; } = true;

	/// <summary>
	/// Gets or sets the look sensitivity multiplier applied to mouse deltas.
	/// </summary>
	public float LookSensitivity { get; set; } = 0.008f;

	/// <summary>
	/// Gets or sets the zoom sensitivity multiplier applied to wheel deltas.
	/// </summary>
	public float ZoomSensitivity { get; set; } = 1f;

	/// <summary>
	/// Gets or sets the pan sensitivity multiplier applied to mouse drag deltas.
	/// </summary>
	public float PanSensitivity { get; set; } = 0.008f;

	/// <summary>
	/// Gets or sets the dolly sensitivity multiplier applied to mouse drag deltas.
	/// </summary>
	public float DollySensitivity { get; set; } = 0.02f;

	/// <summary>
	/// Initializes a new Silk camera input adapter.
	/// </summary>
	/// <param name="inputContext">The active Silk input context.</param>
	public SilkCameraInputAdapter(IInputContext inputContext)
	{
		ArgumentNullException.ThrowIfNull(inputContext, "inputContext");
		_keyboard = ((inputContext.Keyboards.Count > 0) ? inputContext.Keyboards[0] : null);
		_mouse = ((inputContext.Mice.Count > 0) ? inputContext.Mice[0] : null);
		if (_mouse != null)
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
		Vector2 vector = ReadMouseDelta();
		Vector2 lookDelta = Vector2.Zero;
		Vector2 panDelta = Vector2.Zero;
		float dollyDelta = 0f;
		if (IsAltGestureActive())
		{
			if (_mouse != null && _mouse.IsButtonPressed(MouseButton.Left))
			{
				lookDelta = vector * LookSensitivity;
			}
			if (_mouse != null && _mouse.IsButtonPressed(MouseButton.Middle))
			{
				panDelta = new Vector2(0f - vector.X, vector.Y) * PanSensitivity;
			}
			if (_mouse != null && _mouse.IsButtonPressed(MouseButton.Right))
			{
				dollyDelta = vector.Y * DollySensitivity;
			}
		}
		float zoomDelta = _pendingScrollDelta * ZoomSensitivity;
		_pendingScrollDelta = 0f;
		Vector3 moveIntent = ReadMoveIntent();
		return new CameraControllerInput(lookDelta, zoomDelta, panDelta, dollyDelta, moveIntent);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			if (_mouse != null)
			{
				_mouse.Scroll -= OnMouseScroll;
			}
			_disposed = true;
		}
	}

	private Vector2 ReadMouseDelta()
	{
		if (_mouse == null)
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
		Vector2 result = position - _lastMousePosition;
		_lastMousePosition = position;
		if (result.LengthSquared() <= 0f)
		{
			return Vector2.Zero;
		}
		return result;
	}

	private bool IsAltGestureActive()
	{
		if (!RequireAltForMouseGestures)
		{
			return true;
		}
		if (_keyboard == null)
		{
			return false;
		}
		return _keyboard.IsKeyPressed(Key.AltLeft) || _keyboard.IsKeyPressed(Key.AltRight);
	}

	private Vector3 ReadMoveIntent()
	{
		if (_keyboard == null)
		{
			return Vector3.Zero;
		}
		Vector3 zero = Vector3.Zero;
		if (_keyboard.IsKeyPressed(Key.W))
		{
			zero.Z += 1f;
		}
		if (_keyboard.IsKeyPressed(Key.S))
		{
			zero.Z -= 1f;
		}
		if (_keyboard.IsKeyPressed(Key.D))
		{
			zero.X += 1f;
		}
		if (_keyboard.IsKeyPressed(Key.A))
		{
			zero.X -= 1f;
		}
		if (_keyboard.IsKeyPressed(Key.E))
		{
			zero.Y += 1f;
		}
		if (_keyboard.IsKeyPressed(Key.Q))
		{
			zero.Y -= 1f;
		}
		return zero;
	}

	private void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
	{
		_pendingScrollDelta += scrollWheel.Y;
	}
}
