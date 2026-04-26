using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Hosting;
using Silk.NET.OpenGL;
using System.Numerics;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Hosts the RedFox OpenGL scene renderer inside an Avalonia OpenGL control.
/// </summary>
public sealed class AvaloniaOpenGlRendererControl : OpenGlControlBase, ICustomHitTest
{
    /// <summary>
    /// Defines the <see cref="Scene"/> property.
    /// </summary>
    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, Scene?>(nameof(Scene));

    /// <summary>
    /// Defines the <see cref="Camera"/> property.
    /// </summary>
    public static readonly StyledProperty<Camera?> CameraProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, Camera?>(nameof(Camera));

    /// <summary>
    /// Defines the <see cref="ViewportController"/> property.
    /// </summary>
    public static readonly StyledProperty<SceneViewportController?> ViewportControllerProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, SceneViewportController?>(nameof(ViewportController));

    /// <summary>
    /// Defines the <see cref="SelectedNode"/> property.
    /// </summary>
    public static readonly StyledProperty<SceneNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, SceneNode?>(nameof(SelectedNode));

    /// <summary>
    /// Defines the <see cref="ClearColor"/> property.
    /// </summary>
    public static readonly StyledProperty<Vector4> ClearColorProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, Vector4>(nameof(ClearColor), new Vector4(0.07f, 0.09f, 0.13f, 1.0f));

    /// <summary>
    /// Defines the <see cref="UseViewBasedLighting"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> UseViewBasedLightingProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, bool>(nameof(UseViewBasedLighting));

    /// <summary>
    /// Defines the <see cref="SkinningMode"/> property.
    /// </summary>
    public static readonly StyledProperty<SkinningMode> SkinningModeProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, SkinningMode>(nameof(SkinningMode), SkinningMode.Linear);

    /// <summary>
    /// Defines the <see cref="IsAnimationPaused"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsAnimationPausedProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlRendererControl, bool>(nameof(IsAnimationPaused));

    private readonly OrbitCamera _fallbackCamera = new("AvaloniaFallbackCamera");
    private AvaloniaCameraInputAdapter? _inputAdapter;
    private OpenGlGraphicsDevice? _graphicsDevice;
    private SceneRenderer? _renderer;
    private Scene? _subscribedScene;
    private DateTimeOffset _lastFrameTime = DateTimeOffset.UtcNow;

    static AvaloniaOpenGlRendererControl()
    {
        SceneProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, e) => control.OnScenePropertyChanged(e));
        CameraProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, _) => control.RequestFrame());
        ViewportControllerProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, _) => control.RequestFrame());
        ClearColorProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, _) => control.ApplyRendererProperties());
        UseViewBasedLightingProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, _) => control.ApplyRendererProperties());
        SkinningModeProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, _) => control.ApplyRendererProperties());
        IsAnimationPausedProperty.Changed.AddClassHandler<AvaloniaOpenGlRendererControl>((control, _) => control.RequestFrame());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaOpenGlRendererControl"/> class.
    /// </summary>
    public AvaloniaOpenGlRendererControl()
    {
        Focusable = true;
        _fallbackCamera.Distance = 4.0f;
        _fallbackCamera.ApplyOrbit();
    }

    /// <summary>
    /// Occurs after a scene frame is rendered.
    /// </summary>
    public event EventHandler<AvaloniaRenderFrameEventArgs>? RenderFrame;

    /// <summary>
    /// Gets or sets the scene bound to the renderer control.
    /// </summary>
    public Scene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>
    /// Gets or sets the camera bound to the renderer control.
    /// </summary>
    public Camera? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    /// <summary>
    /// Gets or sets the viewport controller used to update camera, scene, and render state.
    /// </summary>
    public SceneViewportController? ViewportController
    {
        get => GetValue(ViewportControllerProperty);
        set => SetValue(ViewportControllerProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected scene node.
    /// </summary>
    public SceneNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    /// <summary>
    /// Gets or sets the clear color applied at frame start.
    /// </summary>
    public Vector4 ClearColor
    {
        get => GetValue(ClearColorProperty);
        set => SetValue(ClearColorProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether view-based lighting is enabled.
    /// </summary>
    public bool UseViewBasedLighting
    {
        get => GetValue(UseViewBasedLightingProperty);
        set => SetValue(UseViewBasedLightingProperty, value);
    }

    /// <summary>
    /// Gets or sets the renderer skinning mode.
    /// </summary>
    public SkinningMode SkinningMode
    {
        get => GetValue(SkinningModeProperty);
        set => SetValue(SkinningModeProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether scene animation is paused.
    /// </summary>
    public bool IsAnimationPaused
    {
        get => GetValue(IsAnimationPausedProperty);
        set => SetValue(IsAnimationPausedProperty, value);
    }

    /// <summary>
    /// Gets or sets the renderer factory used when the OpenGL context is initialized.
    /// </summary>
    public Func<IGraphicsDevice, SceneRenderer>? RendererFactory { get; set; }

    /// <summary>
    /// Gets the active scene renderer, when initialized.
    /// </summary>
    public SceneRenderer? Renderer => _renderer;

    /// <summary>
    /// Requests a render after external scene graph mutation.
    /// </summary>
    public void InvalidateScene() => RequestFrame();

    /// <inheritdoc/>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        GL silkGl = GL.GetApi(gl.GetProcAddress);
        _graphicsDevice = new OpenGlGraphicsDevice(silkGl);
        _renderer = RendererFactory?.Invoke(_graphicsDevice) ?? CreateDefaultRenderer(_graphicsDevice);
        _renderer.Initialize();
        _inputAdapter = new AvaloniaCameraInputAdapter(this);
        SubscribeScene(Scene);
        ApplyRendererProperties();
        RequestFrame();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        SceneViewportController? viewportController = ViewportController;
        Scene? scene = viewportController?.Scene ?? Scene;
        SceneRenderer? renderer = _renderer;
        OpenGlGraphicsDevice? graphicsDevice = _graphicsDevice;
        if (renderer is null || graphicsDevice is null)
        {
            return;
        }

        graphicsDevice.DefaultFramebufferHandle = unchecked((uint)fb);
        ApplyRendererProperties();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        float deltaTime = Math.Clamp((float)(now - _lastFrameTime).TotalSeconds, 0.0f, 0.25f);
        _lastFrameTime = now;

        int width = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        int height = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
        renderer.Resize(width, height);

        if (scene is not null)
        {
            Camera camera = viewportController?.Camera ?? Camera ?? _fallbackCamera;
            camera.AspectRatio = (float)width / height;
            _inputAdapter ??= new AvaloniaCameraInputAdapter(this);
            scene.IsAnimationPaused = IsAnimationPaused;
            if (viewportController is not null)
            {
                viewportController.ResizeViewport(width, height);
                viewportController.UpdateAndRender(renderer, _inputAdapter, deltaTime);
            }
            else
            {
                camera.UpdateInput(deltaTime, _inputAdapter.ReadInput());
                scene.Update(deltaTime);
                renderer.Render(scene, camera.GetView(), deltaTime);
            }

            RenderFrame?.Invoke(this, new AvaloniaRenderFrameEventArgs(renderer, scene, camera, TimeSpan.FromSeconds(deltaTime)));
        }
        else
        {
            gl.BindFramebuffer(0x8D40, fb);
            gl.Viewport(0, 0, width, height);
            gl.ClearColor(ClearColor.X, ClearColor.Y, ClearColor.Z, ClearColor.W);
            gl.Clear(0x00004000 | 0x00000100);
        }

        RequestNextFrameRendering();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        UnsubscribeScene();
        if (_renderer is not null && Scene is not null)
        {
            _renderer.ReleaseResources(Scene);
        }

        _inputAdapter?.Dispose();
        _inputAdapter = null;
        _renderer?.Dispose();
        _renderer = null;
        _graphicsDevice = null;
        base.OnOpenGlDeinit(gl);
    }

    /// <inheritdoc/>
    protected override void OnOpenGlLost()
    {
        UnsubscribeScene();
        _inputAdapter?.Dispose();
        _inputAdapter = null;
        _renderer = null;
        _graphicsDevice = null;
        base.OnOpenGlLost();
    }

    private static SceneRenderer CreateDefaultRenderer(IGraphicsDevice graphicsDevice)
    {
        return new SceneRenderer(
            graphicsDevice,
            new Vector4(0.07f, 0.09f, 0.13f, 1.0f),
            new Vector3(0.13f, 0.13f, 0.16f),
            new Vector3(-0.4f, -1.0f, -0.2f),
            Vector3.One,
            0.8f,
            false,
            SkinningMode.Linear);
    }

    private void OnScenePropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_renderer is not null && e.OldValue is Scene oldScene)
        {
            _renderer.ReleaseResources(oldScene);
        }

        UnsubscribeScene();
        SubscribeScene(Scene);
        RequestFrame();
    }

    private void SubscribeScene(Scene? scene)
    {
        if (scene is null || ReferenceEquals(scene, _subscribedScene))
        {
            return;
        }

        _subscribedScene = scene;
        _subscribedScene.Changed += OnSceneChanged;
    }

    private void UnsubscribeScene()
    {
        if (_subscribedScene is null)
        {
            return;
        }

        _subscribedScene.Changed -= OnSceneChanged;
        _subscribedScene = null;
    }

    private void OnSceneChanged(object? sender, SceneChangedEventArgs e)
    {
        if (e.Kind == SceneChangeKind.NodeRemoved && e.Node is not null)
        {
            _renderer?.ReleaseResources(e.Node);
        }

        RequestFrame();
    }

    private void ApplyRendererProperties()
    {
        if (_renderer is null)
        {
            return;
        }

        _renderer.ClearColor = ClearColor;
        _renderer.UseViewBasedLighting = UseViewBasedLighting;
        _renderer.SkinningMode = SkinningMode;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_renderer is not null)
        {
            RequestNextFrameRendering();
        }
    }

    bool ICustomHitTest.HitTest(Point point)
    {
        return new Rect(Bounds.Size).Contains(point);
    }
}
