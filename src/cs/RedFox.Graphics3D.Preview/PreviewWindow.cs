using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Cameras;
using RedFox.Graphics3D.OpenGL.Passes;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

using System.IO;
using System.Numerics;

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
            EnvironmentMapExposure = _options.EnvironmentMapExposure,
            EnvironmentMapReflectionIntensity = _options.EnvironmentMapReflectionIntensity,
            EnvironmentMapBlurEnabled = _options.EnvironmentMapBlur,
            EnvironmentMapBlurRadius = _options.EnvironmentMapBlurRadius,
            EnableIBL = _options.EnableIBL,
        };
        _renderer.Initialize();
        _renderer.SceneTransform = SceneBootstrapper.GetUpAxisTransform(_options.UpAxis);
        
        if (_renderer.GetPass<GridPass>() is GridPass gridPass)
            gridPass.Enabled = _options.ShowGrid;

        if (!string.IsNullOrEmpty(_options.EnvironmentMapPath) && File.Exists(_options.EnvironmentMapPath))
        {
            _renderer.EnvironmentMap = new GLEquirectangularEnvironmentMap(_gl);
            _renderer.EnvironmentMap.Load(_options.EnvironmentMapPath, _imageTranslatorManager);

            // Add IBL precompute pass when environment map is loaded
            if (_options.EnableIBL)
            {
                var iblPass = new IblPrecomputePass();
                _renderer.AddPass(iblPass);
            }
        }

        _inputContext = _window.CreateInput();
        _keyboard = _inputContext.Keyboards.FirstOrDefault();
        _mouse = _inputContext.Mice.FirstOrDefault();
        
        if (_mouse is not null)
        {
            _mouse.Scroll += OnMouseScroll;
            _mouse.Cursor.CursorMode = CursorMode.Disabled;
        }

        _camera = CreateOrReuseCamera();
        _cameraController = new CameraController(_camera)
        {
            Mode = _options.CameraMode,
        };
        _cameraController.SynchronizeFromCamera();
        ApplySceneTransformToCamera(_camera, _renderer.SceneTransform);

        if (_options.AutoFitOnLoad && SceneBounds.TryGetBounds(_scene, out SceneBoundsInfo bounds))
            ConfigureCameraClipPlanes(bounds.Radius);

        _renderer.ActiveCamera = _camera;
        
        _animationController = new AnimationPlaybackController(_scene, _options.AnimationName)
        {
            Speed = _options.AnimationSpeed,
        };

        ApplyCursorMode();
    }

    private void OnUpdate(double deltaTime)
    {
        IKeyboard? keyboard = _keyboard;
        IMouse? mouse = _mouse;
        
        // Handle key presses
        if (keyboard is not null)
        {
            // Check specific keys we care about
            CheckKeyState(keyboard, Key.Escape);
            CheckKeyState(keyboard, Key.B);
            CheckKeyState(keyboard, Key.V);
            CheckKeyState(keyboard, Key.G);
            CheckKeyState(keyboard, Key.W);
            CheckKeyState(keyboard, Key.F);
            CheckKeyState(keyboard, Key.Number1);
            CheckKeyState(keyboard, Key.Number2);
            CheckKeyState(keyboard, Key.Number3);
        }

        CameraInputState inputState = BuildInputState(keyboard, mouse);
        _cameraController?.Update(inputState, (float)deltaTime);
        _animationController?.Update((float)deltaTime);
        
        _pendingWheelDelta = 0.0f;
    }

    private void CheckKeyState(IKeyboard keyboard, Key key)
    {
        bool isPressed = keyboard.IsKeyPressed(key);
        bool wasPressed = _keysDown.Contains(key);
        
        if (isPressed && !wasPressed)
        {
            // Key was just pressed - handle it
            HandleKeyPress(key);
        }
        
        if (isPressed)
            _keysDown.Add(key);
        else
            _keysDown.Remove(key);
    }

    private CameraInputState BuildInputState(IKeyboard? keyboard, IMouse? mouse)
    {
        bool leftMouseDown = mouse?.IsButtonPressed(MouseButton.Left) ?? false;
        bool middleMouseDown = mouse?.IsButtonPressed(MouseButton.Middle) ?? false;
        bool rightMouseDown = mouse?.IsButtonPressed(MouseButton.Right) ?? false;
        
        Vector2 mouseDelta = Vector2.Zero;
        if (mouse is not null && _hasLastMousePosition)
        {
            mouseDelta = mouse.Position - _lastMousePosition;
        }
        
        if (mouse is not null)
        {
            _lastMousePosition = mouse.Position;
            _hasLastMousePosition = true;
        }

        bool shiftHeld = keyboard?.IsKeyPressed(Key.ShiftLeft) == true || keyboard?.IsKeyPressed(Key.ShiftRight) == true;
        bool moveForward = keyboard?.IsKeyPressed(Key.W) == true || keyboard?.IsKeyPressed(Key.Up) == true;
        bool moveBackward = keyboard?.IsKeyPressed(Key.S) == true || keyboard?.IsKeyPressed(Key.Down) == true;
        bool moveLeft = keyboard?.IsKeyPressed(Key.A) == true || keyboard?.IsKeyPressed(Key.Left) == true;
        bool moveRight = keyboard?.IsKeyPressed(Key.D) == true || keyboard?.IsKeyPressed(Key.Right) == true;
        bool moveUp = keyboard?.IsKeyPressed(Key.Space) == true;
        bool moveDown = keyboard?.IsKeyPressed(Key.ControlLeft) == true;
        bool fastMoveModifier = shiftHeld;

        return new CameraInputState(
            mouseDelta,
            _pendingWheelDelta,
            leftMouseDown,
            middleMouseDown,
            rightMouseDown,
            shiftHeld,
            moveForward,
            moveBackward,
            moveLeft,
            moveRight,
            moveUp,
            moveDown,
            fastMoveModifier);
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
        _gl?.Viewport(size);
        
        if (_camera is not null)
        {
            float aspectRatio = size.Y > 0 ? size.X / (float)size.Y : 16.0f / 9.0f;
            _camera.AspectRatio = aspectRatio;
        }
    }

    private void OnClosing()
    {
        Dispose();
    }

    private void HandleKeyPress(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                _window.Close();
                break;

            case Key.B:
                if (_renderer is not null)
                    _renderer.ShowBones = !_renderer.ShowBones;
                break;

            case Key.V:
                // Toggle environment map blur (V for enVironment blur)
                if (_renderer is not null)
                    _renderer.EnvironmentMapBlurEnabled = !_renderer.EnvironmentMapBlurEnabled;
                break;

            case Key.G:
                if (_renderer?.GetPass<GridPass>() is GridPass gridPass)
                    gridPass.Enabled = !gridPass.Enabled;
                break;

            case Key.W:
                if (_renderer is not null)
                    _renderer.ShowWireframe = !_renderer.ShowWireframe;
                break;

            case Key.F:
                if (SceneBounds.TryGetBounds(_scene, out SceneBoundsInfo bounds))
                    ConfigureCameraClipPlanes(bounds.Radius);
                break;

            case Key.Number1:
                if (_cameraController is not null)
                    _cameraController.Mode = CameraMode.Arcball;
                break;

            case Key.Number2:
                if (_cameraController is not null)
                    _cameraController.Mode = CameraMode.Blender;
                break;

            case Key.Number3:
                if (_cameraController is not null)
                    _cameraController.Mode = CameraMode.Fps;
                break;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _pendingWheelDelta += wheel.Y;
    }

    private Camera CreateOrReuseCamera()
    {
        Camera? existingCamera = null;
        
        foreach (Camera camera in _scene.RootNode.EnumerateDescendants<Camera>())
        {
            existingCamera = camera;
            break;
        }

        if (existingCamera is not null)
        {
            if (SceneBootstrapper.HasExplicitTransform(existingCamera))
                SceneBootstrapper.SynchronizeCameraFromNodeTransform(existingCamera);

            return existingCamera;
        }

        Camera newCamera = _scene.RootNode.AddNode(new Camera("PreviewCamera"));
        newCamera.Position = new Vector3(0, 2, 5);
        newCamera.Target = Vector3.Zero;
        newCamera.Up = Vector3.UnitY;
        return newCamera;
    }

    private void ConfigureCameraClipPlanes(float sceneRadius)
    {
        if (_camera is null)
            return;

        float distance = sceneRadius * 2.5f;
        _camera.NearPlane = MathF.Max(0.01f, distance * 0.01f);
        _camera.FarPlane = distance * 3.0f;
        _camera.Position = new Vector3(0, sceneRadius * 0.4f, distance);
        _camera.Target = Vector3.Zero;
    }

    private void ApplySceneTransformToCamera(Camera camera, Matrix4x4 sceneTransform)
    {
        camera.Position = Vector3.Transform(camera.Position, sceneTransform);
        camera.Target = Vector3.Transform(camera.Target, sceneTransform);
    }

    private void ApplyCursorMode()
    {
        if (_mouse?.Cursor is null)
            return;

        _mouse.Cursor.CursorMode = CursorMode.Normal;
    }

    private static string BuildTitle(IReadOnlyList<string> inputFiles)
    {
        if (inputFiles.Count == 0)
            return "RedFox Graphics3D Preview";

        if (inputFiles.Count == 1)
            return $"RedFox Graphics3D Preview - {Path.GetFileName(inputFiles[0])}";

        return $"RedFox Graphics3D Preview - {inputFiles.Count} files";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _renderer?.Dispose();
        
        _disposed = true;
    }
}
