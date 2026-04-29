using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace RedFox.Graphics3D.Silk;

/// <summary>
/// Provides a shared Silk window, input, and render-loop host for renderer presenters.
/// </summary>
public sealed class SilkRendererHost : IRendererHost
{
    private readonly ISilkGraphicsPresenterFactory _presenterFactory;
    private readonly Func<IGraphicsDevice, SceneRenderer> _rendererFactory;
    private readonly IWindow _window;

    private ISilkGraphicsPresenter? _presenter;
    private bool _disposed;
    private Action<double, IInputContext, SceneRenderer>? _frameCallback;
    private IInputContext? _inputContext;
    private SceneRenderer? _renderer;
    private bool _rendererDisposedOnClosing;

    /// <summary>
    /// Initializes a new instance of the <see cref="SilkRendererHost"/> class.
    /// </summary>
    /// <param name="windowOptions">The Silk window options.</param>
    /// <param name="presenterFactory">The presenter factory.</param>
    /// <param name="rendererFactory">Creates the scene renderer for the presenter graphics device.</param>
    public SilkRendererHost(
        WindowOptions windowOptions,
        ISilkGraphicsPresenterFactory presenterFactory,
        Func<IGraphicsDevice, SceneRenderer> rendererFactory)
    {
        _presenterFactory = presenterFactory ?? throw new ArgumentNullException(nameof(presenterFactory));
        _rendererFactory = rendererFactory ?? throw new ArgumentNullException(nameof(rendererFactory));

        WindowOptions options = windowOptions;
        _presenterFactory.ConfigureWindowOptions(ref options);

        _window = global::Silk.NET.Windowing.Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    /// <summary>
    /// Gets the active scene renderer.
    /// </summary>
    public SceneRenderer Renderer => _renderer ?? throw new InvalidOperationException("Renderer is not available before the window load event.");

    /// <summary>
    /// Gets the active Silk input context.
    /// </summary>
    public IInputContext InputContext => _inputContext ?? throw new InvalidOperationException("Input context is not available before the window load event.");

    /// <summary>
    /// Gets the underlying Silk window.
    /// </summary>
    public IWindow Window => _window;

    /// <summary>
    /// Runs the host render loop without a per-frame callback.
    /// </summary>
    public void Run()
    {
        Run(static (_, _, _) => { });
    }

    /// <summary>
    /// Runs the host render loop and invokes a callback each frame.
    /// </summary>
    /// <param name="frameCallback">The per-frame callback.</param>
    public void Run(Action<double, IInputContext, SceneRenderer> frameCallback)
    {
        ThrowIfDisposed();
        _frameCallback = frameCallback ?? throw new ArgumentNullException(nameof(frameCallback));
        try
        {
            _window.Run();
        }
        finally
        {
            _frameCallback = null;
        }
    }

    /// <summary>
    /// Releases host and presentation resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Load -= OnLoad;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= OnClosing;
        _inputContext?.Dispose();
        _inputContext = null;
        if (!_rendererDisposedOnClosing && _renderer is not null)
        {
            _renderer.Dispose();
            _rendererDisposedOnClosing = true;
        }

        _presenter?.Dispose();
        _presenter = null;
        _window.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnLoad()
    {
        _inputContext = _window.CreateInput();
        _presenter = _presenterFactory.CreatePresenter(_window);
        _renderer = _rendererFactory(_presenter.GraphicsDevice)
            ?? throw new InvalidOperationException("Renderer factory returned null.");
        _renderer.Initialize();

        Vector2D<int> framebufferSize = _window.FramebufferSize;
        Resize(framebufferSize.X, framebufferSize.Y);
    }

    private void OnRender(double deltaTime)
    {
        if (_frameCallback is null || _inputContext is null || _renderer is null || _presenter is null)
        {
            return;
        }

        _frameCallback(deltaTime, _inputContext, _renderer);
        if (_rendererDisposedOnClosing)
        {
            return;
        }

        _presenter.Present();
    }

    private void OnFramebufferResize(Vector2D<int> framebufferSize)
    {
        Resize(framebufferSize.X, framebufferSize.Y);
    }

    private void OnClosing()
    {
        if (_disposed || _rendererDisposedOnClosing || _renderer is null)
        {
            return;
        }

        _renderer.Dispose();
        _rendererDisposedOnClosing = true;
    }

    private void Resize(int width, int height)
    {
        int safeWidth = Math.Max(1, width);
        int safeHeight = Math.Max(1, height);
        _presenter?.Resize(safeWidth, safeHeight);
        _renderer?.Resize(safeWidth, safeHeight);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
