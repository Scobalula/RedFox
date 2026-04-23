using System;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace RedFox.Graphics3D.OpenGL.Rendering;

/// <summary>
/// Hosts a window and drives a render loop for an OpenGL scene renderer.
/// </summary>
public sealed class OpenGlRendererHost : IDisposable
{
	private static readonly Action<double, Scene, Camera> s_noopUpdate = delegate
	{
	};

	private static readonly Action<double, Scene, Camera, IInputContext> s_noopUpdateWithInput = delegate
	{
	};

	private readonly Camera _camera;

	private readonly OpenGlSceneRenderer _renderer;

	private readonly Scene _scene;

	private readonly IWindow _window;

	private bool _disposed;

	private IInputContext? _inputContext;

	private Action<double, Scene, Camera>? _updateCallback;

	private Action<double, Scene, Camera, IInputContext>? _updateWithInputCallback;

	/// <summary>
	/// Gets the active input context.
	/// </summary>
	public IInputContext InputContext => _inputContext ?? throw new InvalidOperationException("Input context is not available before the window load event.");

	/// <summary>
	/// Initializes a new windowed OpenGL renderer host.
	/// </summary>
	/// <param name="title">The window title.</param>
	/// <param name="width">The initial window width.</param>
	/// <param name="height">The initial window height.</param>
	/// <param name="scene">The scene to render.</param>
	/// <param name="camera">The active scene camera.</param>
	/// <param name="settings">The renderer settings.</param>
	public OpenGlRendererHost(string title, int width, int height, Scene scene, Camera camera, OpenGlRenderSettings settings)
	{
		_scene = scene ?? throw new ArgumentNullException("scene");
		_camera = camera ?? throw new ArgumentNullException("camera");
		WindowOptions options = WindowOptions.Default;
		options.Title = title;
		options.Size = new Vector2D<int>(width, height);
		options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(4, 3));
		_window = Window.Create(options);
		_renderer = new OpenGlSceneRenderer(_window, settings);
		_window.Load += OnLoad;
		_window.Render += OnRender;
		_window.FramebufferResize += OnFramebufferResize;
	}

	/// <summary>
	/// Runs the host render loop until the window closes.
	/// </summary>
	public void Run()
	{
		Run(s_noopUpdate);
	}

	/// <summary>
	/// Runs the host render loop until the window closes and invokes a per-frame update callback.
	/// </summary>
	/// <param name="updateCallback">The callback invoked before each render with delta time, scene, and camera.</param>
	public void Run(Action<double, Scene, Camera> updateCallback)
	{
		ThrowIfDisposed();
		_updateCallback = updateCallback ?? throw new ArgumentNullException("updateCallback");
		_updateWithInputCallback = null;
		try
		{
			_window.Run();
		}
		finally
		{
			_updateCallback = null;
		}
	}

	/// <summary>
	/// Runs the host render loop until the window closes and invokes a per-frame update callback with input context.
	/// </summary>
	/// <param name="updateCallback">The callback invoked before each render with delta time, scene, camera, and input context.</param>
	public void Run(Action<double, Scene, Camera, IInputContext> updateCallback)
	{
		ThrowIfDisposed();
		_updateWithInputCallback = updateCallback ?? throw new ArgumentNullException("updateCallback");
		_updateCallback = null;
		try
		{
			_window.Run();
		}
		finally
		{
			_updateWithInputCallback = null;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_window.Load -= OnLoad;
			_window.Render -= OnRender;
			_window.FramebufferResize -= OnFramebufferResize;
			_inputContext?.Dispose();
			_inputContext = null;
			_renderer.Dispose();
			_window.Dispose();
			_disposed = true;
		}
	}

	private void OnLoad()
	{
		_inputContext = _window.CreateInput();
		_renderer.Initialize();
		Vector2D<int> framebufferSize = _window.FramebufferSize;
		_renderer.Resize(framebufferSize.X, framebufferSize.Y);
		if (framebufferSize.Y > 0)
		{
			_camera.AspectRatio = (float)framebufferSize.X / (float)framebufferSize.Y;
		}
	}

	private void OnRender(double deltaTime)
	{
		_updateCallback?.Invoke(deltaTime, _scene, _camera);
		if (_updateWithInputCallback != null)
		{
			IInputContext arg = _inputContext ?? throw new InvalidOperationException("Input context was not initialized before render.");
			_updateWithInputCallback(deltaTime, _scene, _camera, arg);
		}
		_scene.Update((float)deltaTime);
		CameraView view = _camera.GetView();
		_renderer.Render(_scene, in view);
	}

	private void OnFramebufferResize(Vector2D<int> framebufferSize)
	{
		_renderer.Resize(framebufferSize.X, framebufferSize.Y);
		if (framebufferSize.Y > 0)
		{
			_camera.AspectRatio = (float)framebufferSize.X / (float)framebufferSize.Y;
		}
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}
}
