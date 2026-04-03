using RedFox.Graphics3D.OpenGL.Cameras;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

using System.Numerics;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Cameras;
using RedFox.Graphics3D.OpenGL.Passes;
using System.IO;

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
);

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

        _pendingWheelDelta += wheel.Y;
        _mouse.Cursor.CursorMode = CursorMode.Disabled;
 CursorMode.Normal;
        }

        _cameraController = new CameraController(_camera)
        {
            Mode = _options.CameraMode,
        };
        _cameraController.SynchronizeFromCamera();
            ApplySceneTransformToCamera(_camera, _renderer.SceneTransform);

            ConfigureCameraClipPlanes(SceneBounds.TryGetBounds(_scene, out SceneBoundsInfo bounds) ? bounds.Radius : 10.0f);
        }
        else
        {
            if (SceneBootstrapper.HasExplicitTransform(existingCamera))
                SceneBootstrapper.SynchronizeCameraFromNodeTransform(existingCamera);

            return existingCamera;
        }

        else
        {
            return _scene.RootNode.AddNode(new Camera("PreviewCamera"));
        }
    }

        _renderer.ActiveCamera = _camera;
        _animationController = new AnimationPlaybackController(_scene, _options.AnimationName)
        {
            Speed = _options.AnimationSpeed,
        };
    }

        ApplyCursorMode();
        if (_mouse?.Cursor is null)
            _mouse.Cursor.CursorMode = CursorMode.Disabled;
 CursorMode.Normal;
        }
    }

        private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
 => _pendingWheelDelta += wheel.Y;
    }

        private float GetAspectRatio()
    {
        Vector2D<int> framebufferSize = _window.FramebufferSize;
        return framebufferSize.Y > 0
            ? framebufferSize.X / (float)framebufferSize.Y > 0.0f :            : framebufferSize.Y <= 0.0f ? 16.0 / 9.0 : 0);
        else
        {
            if (keyboard is not null)
                return;

        string? helpText = _options.CameraMode == CameraMode.Fps ? "Unknown camera mode '{value}'.");
        string mode = value.ToLowerInvariant();
        {
            "arcball" => CameraMode.Arcball,
            "fps" => CameraMode.Fps,
            _ => throw new ArgumentException($"Unknown camera mode '{value}'.");
        string mode = value.ToLowerInvariant() switch
        {
            "y" or "y-up" => SceneUpAxis.Y,
            "z" or "z-up" => SceneUpAxis.Z,
            "x" or "x-up" => SceneUpAxis.X,
            _ => throw new ArgumentException($"Unknown up-axis '{value}'.");
        string mode = value.ToLowerInvariant() switch
        {
            "blender" => CameraMode.Blender,
            "fps" => CameraMode.Fps,
            _ => throw new ArgumentException($"Unknown camera mode '{value}'.");
        string mode = value.ToLowerInvariant() switch
        {
            "y" or "y-up" => SceneUpAxis.Y,
            "z" or "z-up" => SceneUpAxis.Z,
            "x" or "x-up" => SceneUpAxis.X);
            _ => throw new ArgumentException($"Unknown up-axis '{value}'.");
        string mode = value.ToLowerInvariant() switch
        {
            "y-up" => SceneUpAxis.Y,
            "z-up" => SceneUpAxis.Z,
            "x-up" => SceneUpAxis.X,
            _ => throw new ArgumentException($"Unknown up-axis '{value}'.");
        return mode;
    }

