using System.Numerics;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Cameras;
using RedFox.Graphics3D.OpenGL.Passes;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace RedFox.Graphics3D.Preview;

public sealed class PreviewWindow : IDisposable
{
    private readonly PreviewCliOptions _options;
    private readonly Scene _scene;
    private readonly ImageTranslatorManager _imageTranslatorManager;
    private readonly IWindow _window;
    private readonly HashSet<Key> _keysDown = [];

    private IInputContext? _inputContext;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private GL? _gl;
    private GLRenderer? _renderer;
    private Camera? _camera;
    private CameraController? _cameraController;
    private AnimationPlaybackController? _animationController;

    private Vector2 _lastMousePosition;
    private float _pendingWheelDelta;
    private bool _hasLastMousePosition;
    private int _renderedFrames;
    private bool _disposed;

    public PreviewWindow(PreviewCliOptions options, Scene scene, ImageTranslatorManager imageTranslatorManager)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _imageTranslatorManager = imageTranslatorManager ?? throw new ArgumentNullException(nameof(imageTranslatorManager));

        Window.PrioritizeGlfw();

        WindowOptions windowOptions = WindowOptions.Default;
        windowOptions.Size = new Vector2D<int>(_options.Width, _options.Height);
        windowOptions.Title = BuildTitle(_options.InputFiles);
        windowOptions.IsVisible = !_options.Hidden;
        windowOptions.VSync = true;
        windowOptions.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.ForwardCompatible,
            new APIVersion(4, 1));

        _window = Window.Create(windowOptions);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    public int Run()
    {
        _window.Run();
        return 0;
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window);
        _renderer = new GLRenderer(_gl)
        {
            ImageTranslatorManager = _imageTranslatorManager,
            ShowBones = _options.ShowBones,
            ShowWireframe = _options.Wireframe,
            EnableBackFaceCulling = false,
        };
        _renderer.Initialize();
        _renderer.SceneTransform = SceneBootstrapper.GetUpAxisTransform(_options.UpAxis);
        if (_renderer.GetPass<GridPass>() is GridPass gridPass)
            gridPass.Enabled = _options.ShowGrid;

        _inputContext = _window.CreateInput();
        _keyboard = _inputContext.Keyboards.FirstOrDefault();
        _mouse = _inputContext.Mice.FirstOrDefault();
        if (_mouse is not null)
            _mouse.Scroll += OnMouseScroll;

        _camera = ResolveCamera();
        _camera.AspectRatio = GetAspectRatio();

        _cameraController = new CameraController(_camera)
        {
            Mode = _options.CameraMode,
        };

        if (_options.AutoFitOnLoad)
        {
            FitCameraToScene();
        }
        else
        {
            if (SceneBootstrapper.HasExplicitTransform(_camera))
            {
                SceneBootstrapper.SynchronizeCameraFromNodeTransform(_camera);
                ApplySceneTransformToCamera(_camera, _renderer.SceneTransform);
            }

            _cameraController.SynchronizeFromCamera();
            ConfigureCameraClipPlanes(SceneBounds.TryGetBounds(_scene, out SceneBoundsInfo bounds) ? bounds.Radius : 10.0f);
        }

        _renderer.ActiveCamera = _camera;
        _animationController = new AnimationPlaybackController(_scene, _options.AnimationName)
        {
            Speed = _options.AnimationSpeed,
        };

        ApplyCursorMode();
        PrintSummary();
    }

    private void OnUpdate(double deltaTime)
    {
        if (_renderer is null || _camera is null || _cameraController is null)
            return;

        _scene.Update((float)deltaTime);
        _animationController?.Update((float)deltaTime);

        HandleHotkeys();
        ApplyCursorMode();

        _camera.AspectRatio = GetAspectRatio();
        _cameraController.Update(BuildCameraInputState(), (float)deltaTime);
        _renderer.ActiveCamera = _camera;
    }

    private void OnRender(double deltaTime)
    {
        _renderer?.Render(_scene, (float)deltaTime);

        _renderedFrames++;
        if (_options.MaxFrames > 0 && _renderedFrames >= _options.MaxFrames)
            _window.Close();
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        if (_gl is null || _camera is null)
            return;

        _gl.Viewport(size);
        _camera.AspectRatio = GetAspectRatio();
    }

    private void OnClosing()
    {
    }

    private Camera ResolveCamera()
    {
        Camera? existingCamera = _scene.RootNode.TryGetFirstOfType<Camera>();
        if (existingCamera is not null)
        {
            if (SceneBootstrapper.HasExplicitTransform(existingCamera))
                SceneBootstrapper.SynchronizeCameraFromNodeTransform(existingCamera);

            return existingCamera;
        }

        return _scene.RootNode.AddNode(new Camera("PreviewCamera"));
    }

    private CameraInputState BuildCameraInputState()
    {
        Vector2 mousePosition = Vector2.Zero;
        if (_mouse is not null)
            mousePosition = new Vector2(_mouse.Position.X, _mouse.Position.Y);

        Vector2 mouseDelta = _hasLastMousePosition ? mousePosition - _lastMousePosition : Vector2.Zero;
        _lastMousePosition = mousePosition;
        _hasLastMousePosition = true;

        float wheelDelta = _pendingWheelDelta;
        _pendingWheelDelta = 0.0f;

        return new CameraInputState(
            mouseDelta,
            wheelDelta,
            IsMouseDown(MouseButton.Left),
            IsMouseDown(MouseButton.Middle),
            IsMouseDown(MouseButton.Right),
            IsKeyDown(Key.ShiftLeft) || IsKeyDown(Key.ShiftRight),
            IsKeyDown(Key.W),
            IsKeyDown(Key.S),
            IsKeyDown(Key.A),
            IsKeyDown(Key.D),
            IsKeyDown(Key.E),
            IsKeyDown(Key.Q),
            IsKeyDown(Key.ControlLeft) || IsKeyDown(Key.ControlRight));
    }

    private void HandleHotkeys()
    {
        if (ConsumeKeyPress(Key.Number1))
            SetCameraMode(CameraMode.Arcball);

        if (ConsumeKeyPress(Key.Number2))
            SetCameraMode(CameraMode.Blender);

        if (ConsumeKeyPress(Key.Number3))
            SetCameraMode(CameraMode.Fps);

        if (ConsumeKeyPress(Key.B) && _renderer is not null)
            _renderer.ShowBones = !_renderer.ShowBones;

        if (ConsumeKeyPress(Key.G) && _renderer?.GetPass<GridPass>() is GridPass gridPass)
            gridPass.Enabled = !gridPass.Enabled;

        if (ConsumeKeyPress(Key.W) && _renderer is not null)
            _renderer.ShowWireframe = !_renderer.ShowWireframe;

        if (ConsumeKeyPress(Key.F))
            FitCameraToScene();

        if (ConsumeKeyPress(Key.Escape))
            _window.Close();
    }

    private void SetCameraMode(CameraMode mode)
    {
        if (_cameraController is null)
            return;

        _cameraController.Mode = mode;
    }

    private void ApplyCursorMode()
    {
        if (_mouse?.Cursor is null || _cameraController is null)
            return;

        _mouse.Cursor.CursorMode = _cameraController.Mode == CameraMode.Fps
            ? CursorMode.Disabled
            : CursorMode.Normal;
    }

    private bool ConsumeKeyPress(Key key)
    {
        bool isDown = IsKeyDown(key);
        bool wasDown = _keysDown.Contains(key);

        if (isDown)
            _keysDown.Add(key);
        else
            _keysDown.Remove(key);

        return isDown && !wasDown;
    }

    private bool IsKeyDown(Key key) => _keyboard?.IsKeyPressed(key) ?? false;

    private bool IsMouseDown(MouseButton button) => _mouse?.IsButtonPressed(button) ?? false;

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _pendingWheelDelta += wheel.Y;
    }

    private float GetAspectRatio()
    {
        Vector2D<int> framebufferSize = _window.FramebufferSize;
        return framebufferSize.Y > 0
            ? framebufferSize.X / (float)framebufferSize.Y
            : 16.0f / 9.0f;
    }

    private void PrintSummary()
    {
        int meshCount = _scene.RootNode.EnumerateDescendants<Mesh>().Count();
        int skeletonCount = _scene.RootNode.EnumerateDescendants<Skeleton>().Count();
        int animationCount = _scene.RootNode.EnumerateDescendants<Animation>().Count();

        Console.WriteLine($"Loaded {_options.InputFiles.Count} file(s).");
        Console.WriteLine($"Scene contains {meshCount} mesh(es), {skeletonCount} skeleton root(s), and {animationCount} animation node(s).");
        Console.WriteLine($"Camera mode: {_options.CameraMode}. Up-axis: {_options.UpAxis}. Hidden: {_options.Hidden}. Auto-close frames: {_options.MaxFrames}. Auto-fit: {_options.AutoFitOnLoad}. Normalize: {_options.NormalizeScene}.");
    }

    private void FitCameraToScene()
    {
        if (_cameraController is null)
            return;

        if (SceneBounds.TryGetBounds(_scene, out SceneBoundsInfo bounds))
        {
            Vector3 transformedCenter = _renderer is not null
                ? _renderer.TransformPoint(bounds.Center)
                : bounds.Center;

            _cameraController.Fit(transformedCenter, bounds.Radius);
            ConfigureCameraClipPlanes(bounds.Radius);
            return;
        }

        float gridRadius = _renderer?.GetPass<GridPass>()?.HalfExtent ?? 10.0f;
        _cameraController.Fit(Vector3.Zero, gridRadius);
        ConfigureCameraClipPlanes(gridRadius);
    }

    private void ConfigureCameraClipPlanes(float radius)
    {
        if (_camera is null)
            return;

        float safeRadius = MathF.Max(radius, 1.0f);
        _camera.NearPlane = MathF.Max(0.01f, safeRadius / 1000.0f);
        _camera.FarPlane = MathF.Max(1000.0f, safeRadius * 20.0f);
    }

    private static string BuildTitle(IReadOnlyList<string> inputFiles)
    {
        if (inputFiles.Count == 0)
            return "RedFox Preview";

        if (inputFiles.Count == 1)
            return $"RedFox Preview - {Path.GetFileName(inputFiles[0])}";

        return $"RedFox Preview - {inputFiles.Count} files";
    }

    private static void ApplySceneTransformToCamera(Camera camera, Matrix4x4 sceneTransform)
    {
        camera.Position = Vector3.Transform(camera.Position, sceneTransform);
        camera.Target = Vector3.Transform(camera.Target, sceneTransform);

        Vector3 transformedUp = Vector3.TransformNormal(camera.Up, sceneTransform);
        if (transformedUp.LengthSquared() > 1e-12f)
            camera.Up = Vector3.Normalize(transformedUp);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _renderer?.Dispose();
        _renderer = null;

        if (_mouse is not null)
            _mouse.Scroll -= OnMouseScroll;

        _inputContext?.Dispose();
        _inputContext = null;

        _window.Dispose();
    }
}
