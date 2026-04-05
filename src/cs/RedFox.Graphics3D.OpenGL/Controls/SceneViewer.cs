using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.OpenGL.Cameras;
using RedFox.Graphics3D.OpenGL.Passes;
using RedFox.Graphics3D.OpenGL.Viewing;
using RedFox.Graphics3D.Skeletal;
using Silk.NET.OpenGL;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaKey = Avalonia.Input.Key;

namespace RedFox.Graphics3D.OpenGL.Controls;

public class SceneViewer : HitTestOpenGlControlBase
{
    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<SceneViewer, Scene?>(nameof(Scene));

    public static readonly StyledProperty<ImageTranslatorManager?> ImageTranslatorManagerProperty =
        AvaloniaProperty.Register<SceneViewer, ImageTranslatorManager?>(nameof(ImageTranslatorManager));

    public static readonly StyledProperty<SceneViewerController?> ControllerProperty =
        AvaloniaProperty.Register<SceneViewer, SceneViewerController?>(nameof(Controller));

    public static readonly StyledProperty<bool> ShowBonesProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(ShowBones), true);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> ShowWireframeProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(ShowWireframe));

    public static readonly StyledProperty<bool> ShowSkyboxProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(ShowSkybox), true);

    public static readonly StyledProperty<bool> EnableBackFaceCullingProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(EnableBackFaceCulling), true);

    public static readonly StyledProperty<RendererShadingMode> ShadingModeProperty =
        AvaloniaProperty.Register<SceneViewer, RendererShadingMode>(nameof(ShadingMode), RendererShadingMode.Pbr);

    public static readonly StyledProperty<bool> AutoFitOnSceneChangedProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(AutoFitOnSceneChanged), true);

    public static readonly StyledProperty<bool> NormalizeSceneProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(NormalizeScene));

    public static readonly StyledProperty<float> NormalizeRadiusProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(NormalizeRadius), 10.0f);

    public static readonly StyledProperty<SceneUpAxis> UpAxisProperty =
        AvaloniaProperty.Register<SceneViewer, SceneUpAxis>(nameof(UpAxis), SceneUpAxis.Y);

    public static readonly StyledProperty<string?> AnimationNameProperty =
        AvaloniaProperty.Register<SceneViewer, string?>(nameof(AnimationName));

    public static readonly StyledProperty<string?> ViewBoneNameProperty =
        AvaloniaProperty.Register<SceneViewer, string?>(nameof(ViewBoneName));

    public static readonly StyledProperty<BoneViewAxis> ViewBoneForwardAxisProperty =
        AvaloniaProperty.Register<SceneViewer, BoneViewAxis>(nameof(ViewBoneForwardAxis), BoneViewAxis.NegativeZ);

    public static readonly StyledProperty<BoneViewAxis> ViewBoneUpAxisProperty =
        AvaloniaProperty.Register<SceneViewer, BoneViewAxis>(nameof(ViewBoneUpAxis), BoneViewAxis.PositiveY);

    public static readonly StyledProperty<float> AnimationSpeedProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(AnimationSpeed), 1.0f);

    public static readonly StyledProperty<float> AnimationTimeSecondsProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(AnimationTimeSeconds));

    public static readonly StyledProperty<float> AnimationDurationSecondsProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(AnimationDurationSeconds));

    public static readonly StyledProperty<float> AnimationFrameRateProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(AnimationFrameRate));

    public static readonly StyledProperty<bool> IsAnimationPlayingProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(IsAnimationPlaying), true);

    public static readonly StyledProperty<CameraMode> CameraModeProperty =
        AvaloniaProperty.Register<SceneViewer, CameraMode>(nameof(CameraMode), CameraMode.Arcball);

    public static readonly StyledProperty<float> FieldOfViewProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(FieldOfView), 60.0f);

    public static readonly StyledProperty<string?> EnvironmentMapPathProperty =
        AvaloniaProperty.Register<SceneViewer, string?>(nameof(EnvironmentMapPath));

    public static readonly StyledProperty<float> EnvironmentMapExposureProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(EnvironmentMapExposure), 1.0f);

    public static readonly StyledProperty<float> EnvironmentMapReflectionIntensityProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(EnvironmentMapReflectionIntensity), 1.0f);

    public static readonly StyledProperty<bool> EnvironmentMapBlurEnabledProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(EnvironmentMapBlurEnabled));

    public static readonly StyledProperty<float> EnvironmentMapBlurRadiusProperty =
        AvaloniaProperty.Register<SceneViewer, float>(nameof(EnvironmentMapBlurRadius), 4.0f);

    public static readonly StyledProperty<EnvironmentMapFlipMode> EnvironmentMapFlipModeProperty =
        AvaloniaProperty.Register<SceneViewer, EnvironmentMapFlipMode>(nameof(EnvironmentMapFlipMode), EnvironmentMapFlipMode.Auto);

    public static readonly StyledProperty<bool> EnableIBLProperty =
        AvaloniaProperty.Register<SceneViewer, bool>(nameof(EnableIBL), true);

    public static readonly StyledProperty<int> MsaaSamplesProperty =
        AvaloniaProperty.Register<SceneViewer, int>(nameof(MsaaSamples), 4);

    public static readonly StyledProperty<AvaloniaColor> ClearColorProperty =
        AvaloniaProperty.Register<SceneViewer, AvaloniaColor>(nameof(ClearColor), AvaloniaColor.FromArgb(255, 31, 31, 36));

    public static readonly DirectProperty<SceneViewer, IReadOnlyList<string>> AvailableAnimationNamesProperty =
        AvaloniaProperty.RegisterDirect<SceneViewer, IReadOnlyList<string>>(
            nameof(AvailableAnimationNames),
            viewer => viewer.AvailableAnimationNames);

    public static readonly DirectProperty<SceneViewer, IReadOnlyList<string>> AvailableViewBoneNamesProperty =
        AvaloniaProperty.RegisterDirect<SceneViewer, IReadOnlyList<string>>(
            nameof(AvailableViewBoneNames),
            viewer => viewer.AvailableViewBoneNames);

    public static readonly DirectProperty<SceneViewer, int> RenderedFrameCountProperty =
        AvaloniaProperty.RegisterDirect<SceneViewer, int>(
            nameof(RenderedFrameCount),
            viewer => viewer.RenderedFrameCount);

    public static readonly DirectProperty<SceneViewer, float> FramesPerSecondProperty =
        AvaloniaProperty.RegisterDirect<SceneViewer, float>(
            nameof(FramesPerSecond),
            viewer => viewer.FramesPerSecond);

    public static readonly DirectProperty<SceneViewer, float> AverageFrameTimeMillisecondsProperty =
        AvaloniaProperty.RegisterDirect<SceneViewer, float>(
            nameof(AverageFrameTimeMilliseconds),
            viewer => viewer.AverageFrameTimeMilliseconds);

    private readonly Scene _emptyScene = new("SceneViewer Empty Scene");
    private readonly HashSet<AvaloniaKey> _keysDown = [];
    private readonly HashSet<Scene> _pendingSceneReleases = [];

    private GL? _gl;
    private GLRenderer? _renderer;
    private Camera? _camera;
    private CameraController? _cameraController;
    private AnimationPlaybackController? _animationController;
    private IReadOnlyList<string> _availableAnimationNames = Array.Empty<string>();
    private IReadOnlyList<string> _availableViewBoneNames = Array.Empty<string>();
    private Point _pointerPosition;
    private Point _lastPointerPosition;
    private bool _hasPointerPosition;
    private bool _hasLastPointerPosition;
    private float _pendingWheelDelta;
    private bool _leftPointerDown;
    private bool _middlePointerDown;
    private bool _rightPointerDown;
    private long _lastRenderTimestamp;
    private int _renderedFrameCount;
    private float _framesPerSecond;
    private float _averageFrameTimeMilliseconds;
    private bool _environmentMapDirty = true;
    private bool _isSyncingAnimationState;
    private bool _isSyncingFieldOfView;
    private bool _hasExplicitFieldOfView;
    private bool _hasStoredFreeCameraState;
    private CameraState _storedFreeCameraState;

    public SceneViewer()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    public event EventHandler? FrameRendered;

    public Scene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public ImageTranslatorManager? ImageTranslatorManager
    {
        get => GetValue(ImageTranslatorManagerProperty);
        set => SetValue(ImageTranslatorManagerProperty, value);
    }

    public SceneViewerController? Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    public bool ShowBones
    {
        get => GetValue(ShowBonesProperty);
        set => SetValue(ShowBonesProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool ShowWireframe
    {
        get => GetValue(ShowWireframeProperty);
        set => SetValue(ShowWireframeProperty, value);
    }

    public bool ShowSkybox
    {
        get => GetValue(ShowSkyboxProperty);
        set => SetValue(ShowSkyboxProperty, value);
    }

    public bool EnableBackFaceCulling
    {
        get => GetValue(EnableBackFaceCullingProperty);
        set => SetValue(EnableBackFaceCullingProperty, value);
    }

    public RendererShadingMode ShadingMode
    {
        get => GetValue(ShadingModeProperty);
        set => SetValue(ShadingModeProperty, value);
    }

    public bool AutoFitOnSceneChanged
    {
        get => GetValue(AutoFitOnSceneChangedProperty);
        set => SetValue(AutoFitOnSceneChangedProperty, value);
    }

    public bool NormalizeScene
    {
        get => GetValue(NormalizeSceneProperty);
        set => SetValue(NormalizeSceneProperty, value);
    }

    public float NormalizeRadius
    {
        get => GetValue(NormalizeRadiusProperty);
        set => SetValue(NormalizeRadiusProperty, value);
    }

    public SceneUpAxis UpAxis
    {
        get => GetValue(UpAxisProperty);
        set => SetValue(UpAxisProperty, value);
    }

    public string? AnimationName
    {
        get => GetValue(AnimationNameProperty);
        set => SetValue(AnimationNameProperty, value);
    }

    public string? ViewBoneName
    {
        get => GetValue(ViewBoneNameProperty);
        set => SetValue(ViewBoneNameProperty, value);
    }

    public BoneViewAxis ViewBoneForwardAxis
    {
        get => GetValue(ViewBoneForwardAxisProperty);
        set => SetValue(ViewBoneForwardAxisProperty, value);
    }

    public BoneViewAxis ViewBoneUpAxis
    {
        get => GetValue(ViewBoneUpAxisProperty);
        set => SetValue(ViewBoneUpAxisProperty, value);
    }

    public float AnimationSpeed
    {
        get => GetValue(AnimationSpeedProperty);
        set => SetValue(AnimationSpeedProperty, value);
    }

    public bool IsAnimationPlaying
    {
        get => GetValue(IsAnimationPlayingProperty);
        set => SetValue(IsAnimationPlayingProperty, value);
    }

    public float AnimationTimeSeconds
    {
        get => GetValue(AnimationTimeSecondsProperty);
        set => SetValue(AnimationTimeSecondsProperty, value);
    }

    public float AnimationDurationSeconds
    {
        get => GetValue(AnimationDurationSecondsProperty);
        set => SetValue(AnimationDurationSecondsProperty, value);
    }

    public float AnimationFrameRate
    {
        get => GetValue(AnimationFrameRateProperty);
        set => SetValue(AnimationFrameRateProperty, value);
    }

    public CameraMode CameraMode
    {
        get => GetValue(CameraModeProperty);
        set => SetValue(CameraModeProperty, value);
    }

    public float FieldOfView
    {
        get => GetValue(FieldOfViewProperty);
        set => SetValue(FieldOfViewProperty, value);
    }

    public string? EnvironmentMapPath
    {
        get => GetValue(EnvironmentMapPathProperty);
        set => SetValue(EnvironmentMapPathProperty, value);
    }

    public float EnvironmentMapExposure
    {
        get => GetValue(EnvironmentMapExposureProperty);
        set => SetValue(EnvironmentMapExposureProperty, value);
    }

    public float EnvironmentMapReflectionIntensity
    {
        get => GetValue(EnvironmentMapReflectionIntensityProperty);
        set => SetValue(EnvironmentMapReflectionIntensityProperty, value);
    }

    public bool EnvironmentMapBlurEnabled
    {
        get => GetValue(EnvironmentMapBlurEnabledProperty);
        set => SetValue(EnvironmentMapBlurEnabledProperty, value);
    }

    public float EnvironmentMapBlurRadius
    {
        get => GetValue(EnvironmentMapBlurRadiusProperty);
        set => SetValue(EnvironmentMapBlurRadiusProperty, value);
    }

    public EnvironmentMapFlipMode EnvironmentMapFlipMode
    {
        get => GetValue(EnvironmentMapFlipModeProperty);
        set => SetValue(EnvironmentMapFlipModeProperty, value);
    }

    public bool EnableIBL
    {
        get => GetValue(EnableIBLProperty);
        set => SetValue(EnableIBLProperty, value);
    }

    public int MsaaSamples
    {
        get => GetValue(MsaaSamplesProperty);
        set => SetValue(MsaaSamplesProperty, value);
    }

    public AvaloniaColor ClearColor
    {
        get => GetValue(ClearColorProperty);
        set => SetValue(ClearColorProperty, value);
    }

    public IReadOnlyList<string> AvailableAnimationNames
    {
        get => _availableAnimationNames;
        private set => SetAndRaise(AvailableAnimationNamesProperty, ref _availableAnimationNames, value);
    }

    public IReadOnlyList<string> AvailableViewBoneNames
    {
        get => _availableViewBoneNames;
        private set => SetAndRaise(AvailableViewBoneNamesProperty, ref _availableViewBoneNames, value);
    }

    public int RenderedFrameCount
    {
        get => _renderedFrameCount;
        private set => SetAndRaise(RenderedFrameCountProperty, ref _renderedFrameCount, value);
    }

    public float FramesPerSecond
    {
        get => _framesPerSecond;
        private set => SetAndRaise(FramesPerSecondProperty, ref _framesPerSecond, value);
    }

    public float AverageFrameTimeMilliseconds
    {
        get => _averageFrameTimeMilliseconds;
        private set => SetAndRaise(AverageFrameTimeMillisecondsProperty, ref _averageFrameTimeMilliseconds, value);
    }

    public void FitCameraToScene()
    {
        if (_cameraController is null || IsBoneViewActive())
            return;

        if (TryGetSceneBounds(out SceneBoundsInfo bounds))
        {
            _cameraController.Fit(bounds.Center, bounds.Radius);
            ConfigureCameraClipPlanes(bounds);
            RequestNextFrameRendering();
        }
    }

    public void ResetCamera()
    {
        ConfigureSceneState(fitCamera: AutoFitOnSceneChanged, recreateCamera: true, resetAnimation: false);
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _gl = new GL(new AvaloniaNativeContext(gl));
        _renderer = new GLRenderer(_gl);
        _renderer.Initialize();

        _environmentMapDirty = true;
        ApplyRendererSettings();

        if (_renderer.GetPass<AnimationPass>() is null)
            _renderer.AddPass(new AnimationPass());

        ConfigureSceneState(fitCamera: AutoFitOnSceneChanged, recreateCamera: true, resetAnimation: true);
        UpdateViewportState();

        _lastRenderTimestamp = Stopwatch.GetTimestamp();
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);
        QueueSceneResourceRelease(Scene);
        FlushPendingSceneResourceReleases();
        ReleaseRenderer();
    }

    protected override void OnOpenGlLost()
    {
        base.OnOpenGlLost();
        ForgetPendingSceneResourceReleases();
        ForgetSceneResources(Scene);
        ReleaseRenderer(disposeGraphicsResources: false);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        GLRenderer? renderer = _renderer;
        GL? glApi = _gl;
        if (renderer is null || glApi is null)
            return;

        (int width, int height) = GetFramebufferSize();
        renderer.SetOutputSize(width, height);
        if (_camera is not null)
            _camera.AspectRatio = height > 0 ? width / (float)height : 16.0f / 9.0f;

        glApi.BindFramebuffer(GLEnum.Framebuffer, (uint)Math.Max(fb, 0));
        glApi.Viewport(0, 0, (uint)Math.Max(width, 1), (uint)Math.Max(height, 1));

        FlushPendingSceneResourceReleases();
        ApplyEnvironmentMapIfNeeded();

        float deltaTime = ComputeDeltaTime();
        UpdateFrameStats(deltaTime);

        AnimationPass? animationPass = renderer.GetPass<AnimationPass>();
        if (animationPass is not null)
        {
            if (IsAnimationPlaying)
                animationPass.Enabled = true;
            else
                animationPass.SampleAt(AnimationTimeSeconds);
        }

        UpdateCamera(deltaTime);
        ApplyBoneViewCamera();

        if (TryGetSceneBounds(out SceneBoundsInfo bounds))
            ConfigureCameraClipPlanes(bounds);

        renderer.Render(Scene ?? _emptyScene, deltaTime);

        if (animationPass is not null)
            SyncAnimationStateFromPass();

        RenderedFrameCount++;
        FrameRendered?.Invoke(this, EventArgs.Empty);

        _pendingWheelDelta = 0.0f;
        RequestNextFrameRendering();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ControllerProperty)
        {
            UpdateControllerSubscription(change.GetOldValue<SceneViewerController?>(), change.GetNewValue<SceneViewerController?>());
            return;
        }

        if (change.Property == SceneProperty)
        {
            QueueSceneResourceRelease(change.GetOldValue<Scene?>());
            ConfigureSceneState(fitCamera: AutoFitOnSceneChanged, recreateCamera: true, resetAnimation: true);
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == ImageTranslatorManagerProperty ||
            change.Property == ShowBonesProperty ||
            change.Property == ShowWireframeProperty ||
            change.Property == ShowSkyboxProperty ||
            change.Property == EnableBackFaceCullingProperty ||
            change.Property == ShadingModeProperty ||
            change.Property == EnvironmentMapExposureProperty ||
            change.Property == EnvironmentMapReflectionIntensityProperty ||
            change.Property == EnvironmentMapBlurEnabledProperty ||
            change.Property == EnvironmentMapBlurRadiusProperty ||
            change.Property == EnableIBLProperty ||
            change.Property == MsaaSamplesProperty ||
            change.Property == ClearColorProperty ||
            change.Property == ShowGridProperty)
        {
            if (change.Property == ImageTranslatorManagerProperty)
                _environmentMapDirty = true;

            ApplyRendererSettings();
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == EnvironmentMapPathProperty || change.Property == EnvironmentMapFlipModeProperty)
        {
            _environmentMapDirty = true;
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == CameraModeProperty)
        {
            if (_cameraController is not null)
                _cameraController.Mode = CameraMode;

            RequestNextFrameRendering();
            return;
        }

        if (change.Property == AnimationNameProperty)
        {
            ConfigureAnimationState(resetTime: true);
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == ViewBoneNameProperty)
        {
            HandleViewBoneSelectionChanged(change.GetOldValue<string?>(), change.GetNewValue<string?>());
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == ViewBoneForwardAxisProperty || change.Property == ViewBoneUpAxisProperty)
        {
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == AnimationSpeedProperty)
        {
            if (_animationController is not null)
                _animationController.Speed = Math.Max(AnimationSpeed, 0.0f);

            RequestNextFrameRendering();
            return;
        }

        if (change.Property == FieldOfViewProperty)
        {
            if (!_isSyncingFieldOfView)
            {
                _hasExplicitFieldOfView = true;
                ApplyFieldOfViewToCamera();
            }

            RequestNextFrameRendering();
            return;
        }

        if (change.Property == IsAnimationPlayingProperty)
        {
            if (!IsAnimationPlaying && _animationController is not null)
                _animationController.SampleAt(_animationController.ElapsedSeconds);

            RequestNextFrameRendering();
            return;
        }

        if (change.Property == AnimationTimeSecondsProperty && !_isSyncingAnimationState)
        {
            ApplyAnimationTimeSeconds();
            RequestNextFrameRendering();
            return;
        }

        if (change.Property == NormalizeSceneProperty ||
            change.Property == NormalizeRadiusProperty ||
            change.Property == UpAxisProperty)
        {
            ConfigureSceneState(fitCamera: AutoFitOnSceneChanged, recreateCamera: true, resetAnimation: false);
            RequestNextFrameRendering();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        Focus();
        Point point = e.GetPosition(this);
        _pointerPosition = point;
        _lastPointerPosition = point;
        _hasPointerPosition = true;
        _hasLastPointerPosition = true;

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        _leftPointerDown = properties.IsLeftButtonPressed;
        _middlePointerDown = properties.IsMiddleButtonPressed;
        _rightPointerDown = properties.IsRightButtonPressed;

        e.Pointer.Capture(this);
        e.Handled = true;
        RequestNextFrameRendering();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        _leftPointerDown = properties.IsLeftButtonPressed;
        _middlePointerDown = properties.IsMiddleButtonPressed;
        _rightPointerDown = properties.IsRightButtonPressed;

        if (!_leftPointerDown && !_middlePointerDown && !_rightPointerDown)
            e.Pointer.Capture(null);

        e.Handled = true;
        RequestNextFrameRendering();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        _pointerPosition = e.GetPosition(this);
        _hasPointerPosition = true;
        RequestNextFrameRendering();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        _pendingWheelDelta += (float)e.Delta.Y;
        e.Handled = true;
        RequestNextFrameRendering();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _leftPointerDown = false;
        _middlePointerDown = false;
        _rightPointerDown = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        _keysDown.Add(e.Key);

        switch (e.Key)
        {
            case AvaloniaKey.B:
                ShowBones = !ShowBones;
                e.Handled = true;
                break;

            case AvaloniaKey.G:
                ShowGrid = !ShowGrid;
                e.Handled = true;
                break;

            case AvaloniaKey.W:
                ShowWireframe = !ShowWireframe;
                e.Handled = true;
                break;

            case AvaloniaKey.V:
                EnvironmentMapBlurEnabled = !EnvironmentMapBlurEnabled;
                e.Handled = true;
                break;

            case AvaloniaKey.F:
                FitCameraToScene();
                e.Handled = true;
                break;

            case AvaloniaKey.P:
                IsAnimationPlaying = !IsAnimationPlaying;
                e.Handled = true;
                break;

            case AvaloniaKey.D1:
                CameraMode = CameraMode.Arcball;
                e.Handled = true;
                break;

            case AvaloniaKey.D2:
                CameraMode = CameraMode.Blender;
                e.Handled = true;
                break;

            case AvaloniaKey.D3:
                CameraMode = CameraMode.Fps;
                e.Handled = true;
                break;
        }

        RequestNextFrameRendering();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _keysDown.Remove(e.Key);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        _keysDown.Clear();
        _leftPointerDown = false;
        _middlePointerDown = false;
        _rightPointerDown = false;
        _pendingWheelDelta = 0.0f;
    }

    private void UpdateControllerSubscription(SceneViewerController? oldController, SceneViewerController? newController)
    {
        if (oldController is not null)
        {
            oldController.FitToSceneRequested -= OnFitToSceneRequested;
            oldController.ResetCameraRequested -= OnResetCameraRequested;
        }

        if (newController is not null)
        {
            newController.FitToSceneRequested += OnFitToSceneRequested;
            newController.ResetCameraRequested += OnResetCameraRequested;
        }
    }

    private void OnFitToSceneRequested(object? sender, EventArgs e) => FitCameraToScene();

    private void OnResetCameraRequested(object? sender, EventArgs e) => ResetCamera();

    private void ReleaseRenderer(bool disposeGraphicsResources = true)
    {
        _animationController = null;
        _cameraController = null;
        _camera = null;

        if (disposeGraphicsResources)
            _renderer?.Dispose();

        _renderer = null;
        (_gl as IDisposable)?.Dispose();
        _gl = null;

        _lastRenderTimestamp = 0;
        _hasLastPointerPosition = false;
        _environmentMapDirty = true;
        _hasStoredFreeCameraState = false;
        RenderedFrameCount = 0;
        FramesPerSecond = 0.0f;
        AverageFrameTimeMilliseconds = 0.0f;
        SyncAnimationStateFromPass();
    }

    private void QueueSceneResourceRelease(Scene? scene)
    {
        if (scene is null)
            return;

        _pendingSceneReleases.Add(scene);
    }

    private void FlushPendingSceneResourceReleases()
    {
        if (_renderer is null || _pendingSceneReleases.Count == 0)
            return;

        foreach (Scene scene in _pendingSceneReleases)
        {
            foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
                _renderer.UnloadMesh(mesh);

            foreach (Texture texture in scene.RootNode.EnumerateDescendants<Texture>())
                _renderer.UnloadTexture(texture);
        }

        _pendingSceneReleases.Clear();
    }

    private void ForgetPendingSceneResourceReleases()
    {
        foreach (Scene scene in _pendingSceneReleases)
            ForgetSceneResources(scene);

        _pendingSceneReleases.Clear();
    }

    private static void ForgetSceneResources(Scene? scene)
    {
        if (scene is null)
            return;

        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
            mesh.GraphicsHandle = null;

        foreach (Texture texture in scene.RootNode.EnumerateDescendants<Texture>())
            texture.GraphicsHandle = null;
    }

    private void ApplyRendererSettings()
    {
        if (_renderer is null)
            return;

        _renderer.ImageTranslatorManager = ImageTranslatorManager;
        _renderer.Settings.ShowBones = ShowBones;
        _renderer.Settings.ShowWireframe = ShowWireframe;
        _renderer.Settings.ShowSkybox = ShowSkybox;
        _renderer.Settings.EnableBackFaceCulling = EnableBackFaceCulling;
        _renderer.Settings.ShadingMode = ShadingMode;
        _renderer.Settings.EnvironmentMapExposure = EnvironmentMapExposure;
        _renderer.Settings.EnvironmentMapReflectionIntensity = EnvironmentMapReflectionIntensity;
        _renderer.Settings.EnvironmentMapBlurEnabled = EnvironmentMapBlurEnabled;
        _renderer.Settings.EnvironmentMapBlurRadius = EnvironmentMapBlurRadius;
        _renderer.Settings.EnableIBL = EnableIBL;
        _renderer.Settings.RequestedMsaaSamples = MsaaSamples;
        _renderer.Settings.BackgroundColor = ToRendererColor(ClearColor);

        if (_renderer.GetPass<GridPass>() is GridPass gridPass)
            gridPass.Enabled = ShowGrid;

        UpdateViewportState();
    }

    private void ApplyEnvironmentMapIfNeeded()
    {
        if (!_environmentMapDirty || _renderer is null || _gl is null)
            return;

        _environmentMapDirty = false;

        string? path = EnvironmentMapPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _renderer.EnvironmentMap = null;
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            _renderer.EnvironmentMap = null;
            return;
        }

        bool effectiveFlipY = ResolveEnvironmentMapFlipY(EnvironmentMapFlipMode);
        if (_renderer.EnvironmentMap is not null &&
            string.Equals(_renderer.EnvironmentMap.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase) &&
            _renderer.EnvironmentMap.EffectiveFlipY == effectiveFlipY)
        {
            return;
        }

        GLEquirectangularEnvironmentMap environmentMap = new(_gl);
        environmentMap.LoadMetadata(fullPath, effectiveFlipY);
        _renderer.EnvironmentMapFlipMode = EnvironmentMapFlipMode;
        _renderer.EnvironmentMap = environmentMap;
    }

    private void ConfigureSceneState(bool fitCamera, bool recreateCamera, bool resetAnimation)
    {
        Scene activeScene = Scene ?? _emptyScene;
        AvailableAnimationNames = EnumerateAnimationNames(activeScene);
        AvailableViewBoneNames = EnumerateViewBoneNames(activeScene);

        if (_renderer is not null)
        {
            _renderer.Settings.SceneTransform = SceneViewBootstrapper.GetSceneTransform(Scene, UpAxis, NormalizeScene, NormalizeRadius);
            _renderer.Settings.ActiveCamera = EnsureCamera(activeScene, recreateCamera);
        }

        if (resetAnimation || _animationController is null)
            ConfigureAnimationState(resetTime: true);
        else if (_animationController is not null)
            _animationController.SampleAt(_animationController.ElapsedSeconds);

        if (fitCamera)
            FitCameraToScene();
        else if (TryGetSceneBounds(out SceneBoundsInfo bounds))
            ConfigureCameraClipPlanes(bounds);
    }

    private Camera EnsureCamera(Scene scene, bool recreateCamera)
    {
        if (!recreateCamera && _camera is not null && _cameraController is not null)
            return _camera;

        Camera camera = CreateOrReuseCamera(scene);

        if (_renderer is not null)
            ApplySceneTransformToCamera(camera, _renderer.Settings.SceneTransform);

        camera.AspectRatio = GetAspectRatio();
        _camera = camera;
        if (_hasExplicitFieldOfView)
            ApplyFieldOfViewToCamera();
        else
            SyncFieldOfViewFromCamera();

        _cameraController = new CameraController(camera)
        {
            Mode = CameraMode,
        };
        _cameraController.SynchronizeFromCamera();
        return camera;
    }

    private void ConfigureAnimationState(bool resetTime)
    {
        Scene activeScene = Scene ?? _emptyScene;
        _animationController = new AnimationPlaybackController(activeScene, AnimationName)
        {
            Speed = Math.Max(AnimationSpeed, 0.0f),
            ElapsedSeconds = resetTime
                ? 0.0f
                : Math.Max(_animationController?.ElapsedSeconds ?? AnimationTimeSeconds, 0.0f),
        };

        _animationController.SampleAt(_animationController.ElapsedSeconds);
        SyncAnimationStateFromPass();
    }

    private void UpdateViewportState()
    {
        if (_renderer is null)
            return;

        (int width, int height) = GetFramebufferSize();
        _renderer.SetOutputSize(width, height);

        if (_camera is not null)
            _camera.AspectRatio = height > 0 ? width / (float)height : 16.0f / 9.0f;
    }

    private float ComputeDeltaTime()
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastRenderTimestamp == 0)
        {
            _lastRenderTimestamp = now;
            return 0.0f;
        }

        float delta = (float)(now - _lastRenderTimestamp) / Stopwatch.Frequency;
        _lastRenderTimestamp = now;
        return Math.Clamp(delta, 0.0f, 0.1f);
    }

    private void UpdateFrameStats(float deltaTime)
    {
        if (deltaTime <= 1e-6f)
            return;

        float instantaneousFps = 1.0f / deltaTime;
        float instantaneousFrameTimeMs = deltaTime * 1000.0f;

        FramesPerSecond = FramesPerSecond <= 0.0f
            ? instantaneousFps
            : float.Lerp(FramesPerSecond, instantaneousFps, 0.15f);

        AverageFrameTimeMilliseconds = AverageFrameTimeMilliseconds <= 0.0f
            ? instantaneousFrameTimeMs
            : float.Lerp(AverageFrameTimeMilliseconds, instantaneousFrameTimeMs, 0.15f);
    }

    private void HandleViewBoneSelectionChanged(string? oldBoneName, string? newBoneName)
    {
        bool wasBoneViewActive = !string.IsNullOrWhiteSpace(oldBoneName);
        bool isBoneViewActive = !string.IsNullOrWhiteSpace(newBoneName);

        if (!wasBoneViewActive && isBoneViewActive)
            StoreFreeCameraState();
        else if (wasBoneViewActive && !isBoneViewActive)
            RestoreStoredFreeCameraState();
    }

    private void ApplyAnimationTimeSeconds()
    {
        if (_animationController is null)
            return;

        float targetTime = AnimationDurationSeconds > 0.0f
            ? Math.Clamp(AnimationTimeSeconds, 0.0f, AnimationDurationSeconds)
            : Math.Max(AnimationTimeSeconds, 0.0f);

        if (MathF.Abs(targetTime - _animationController.ElapsedSeconds) < 1e-5f)
            return;

        _animationController.SampleAt(targetTime);
        SyncAnimationStateFromPass();
    }

    private void SyncAnimationStateFromPass()
    {
        if (_renderer is null)
            return;

        AnimationPass? animationPass = _renderer.GetPass<AnimationPass>();
        if (animationPass is null)
            return;

        float elapsedSeconds = animationPass.ElapsedSeconds;
        float durationSeconds = animationPass.DurationSeconds;
        float frameRate = animationPass.FrameRate;

        elapsedSeconds = durationSeconds > 0.0f
            ? Math.Clamp(elapsedSeconds, 0.0f, durationSeconds)
            : 0.0f;

        _isSyncingAnimationState = true;

        try
        {
            SetCurrentValue(AnimationDurationSecondsProperty, durationSeconds);

            SetCurrentValue(AnimationFrameRateProperty, frameRate);
            SetCurrentValue(AnimationTimeSecondsProperty, elapsedSeconds);
        }
        finally
        {
            _isSyncingAnimationState = false;
        }
    }

    private void ApplyFieldOfViewToCamera()
    {
        if (_camera is null)
            return;

        _camera.FieldOfView = Math.Clamp(FieldOfView, 1.0f, 179.0f);

        if (TryGetSceneBounds(out SceneBoundsInfo bounds))
            ConfigureCameraClipPlanes(bounds);
    }

    private void SyncFieldOfViewFromCamera()
    {
        if (_camera is null)
            return;

        _isSyncingFieldOfView = true;

        try
        {
            SetCurrentValue(FieldOfViewProperty, Math.Clamp(_camera.FieldOfView, 1.0f, 179.0f));
        }
        finally
        {
            _isSyncingFieldOfView = false;
        }
    }

    private void ApplyBoneViewCamera()
    {
        if (_camera is null || !TryGetSelectedViewBone(out SkeletonBone? bone) || bone is null)
            return;

        if (!TryGetBoneCameraState(bone, out CameraState cameraState))
            return;

        ApplyCameraState(cameraState);
    }

    private void UpdateCamera(float deltaTime)
    {
        if (_cameraController is null || IsBoneViewActive())
            return;

        CameraInputState inputState = BuildInputState();
        _cameraController.Mode = CameraMode;
        _cameraController.Update(inputState, deltaTime);
    }

    private CameraInputState BuildInputState()
    {
        Vector2 mouseDelta = Vector2.Zero;
        if (_hasPointerPosition && _hasLastPointerPosition)
        {
            mouseDelta = new Vector2(
                (float)(_pointerPosition.X - _lastPointerPosition.X),
                (float)(_pointerPosition.Y - _lastPointerPosition.Y));
        }

        if (_hasPointerPosition)
        {
            _lastPointerPosition = _pointerPosition;
            _hasLastPointerPosition = true;
        }

        bool shiftHeld = IsKeyDown(AvaloniaKey.LeftShift) || IsKeyDown(AvaloniaKey.RightShift);
        bool moveForward = IsKeyDown(AvaloniaKey.W) || IsKeyDown(AvaloniaKey.Up);
        bool moveBackward = IsKeyDown(AvaloniaKey.S) || IsKeyDown(AvaloniaKey.Down);
        bool moveLeft = IsKeyDown(AvaloniaKey.A) || IsKeyDown(AvaloniaKey.Left);
        bool moveRight = IsKeyDown(AvaloniaKey.D) || IsKeyDown(AvaloniaKey.Right);
        bool moveUp = IsKeyDown(AvaloniaKey.Space);
        bool moveDown = IsKeyDown(AvaloniaKey.LeftCtrl) || IsKeyDown(AvaloniaKey.RightCtrl);

        return new CameraInputState(
            mouseDelta,
            _pendingWheelDelta,
            _leftPointerDown,
            _middlePointerDown,
            _rightPointerDown,
            shiftHeld,
            moveForward,
            moveBackward,
            moveLeft,
            moveRight,
            moveUp,
            moveDown,
            shiftHeld);
    }

    private bool IsKeyDown(AvaloniaKey key) => _keysDown.Contains(key);

    private Camera CreateOrReuseCamera(Scene scene)
    {
        Camera? existingCamera = null;

        foreach (Camera camera in scene.RootNode.EnumerateDescendants<Camera>())
        {
            existingCamera = camera;
            break;
        }

        if (existingCamera is not null)
        {
            if (SceneViewBootstrapper.HasExplicitTransform(existingCamera))
                SceneViewBootstrapper.SynchronizeCameraFromNodeTransform(existingCamera);

            return existingCamera;
        }

        return new Camera("PreviewCamera")
        {
            Position = new Vector3(0, 2, 5),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
        };
    }

    private bool TryGetSceneBounds(out SceneBoundsInfo bounds)
    {
        if (_renderer is null || Scene is null)
        {
            bounds = default;
            return false;
        }

        return SceneBounds.TryGetBounds(Scene, _renderer.Settings.SceneTransform, out bounds);
    }

    private bool TryGetSelectedViewBone(out SkeletonBone? bone)
    {
        bone = null;

        Scene? scene = Scene;
        string? boneName = ViewBoneName;
        if (scene is null || string.IsNullOrWhiteSpace(boneName))
            return false;

        bone = scene.RootNode.EnumerateDescendants<SkeletonBone>()
            .FirstOrDefault(candidate => candidate.Name.Equals(boneName, StringComparison.OrdinalIgnoreCase));

        return bone is not null;
    }

    private bool TryGetBoneCameraState(SkeletonBone bone, out CameraState cameraState)
    {
        Matrix4x4 world = bone.GetActiveWorldMatrix();
        if (!Matrix4x4.Decompose(world, out _, out Quaternion rotation, out Vector3 translation))
        {
            cameraState = default;
            return false;
        }

        Vector3 forward = Vector3.Normalize(Vector3.Transform(GetAxisVector(ViewBoneForwardAxis), rotation));
        Vector3 up = Vector3.Normalize(Vector3.Transform(GetAxisVector(ViewBoneUpAxis), rotation));

        if (MathF.Abs(Vector3.Dot(forward, up)) > 0.999f)
        {
            up = MathF.Abs(forward.Y) < 0.999f
                ? Vector3.UnitY
                : Vector3.UnitZ;
        }

        up -= forward * Vector3.Dot(up, forward);
        if (up.LengthSquared() <= 1e-8f)
        {
            cameraState = default;
            return false;
        }

        up = Vector3.Normalize(up);

        if (!IsFinite(forward) || !IsFinite(up))
        {
            cameraState = default;
            return false;
        }

        cameraState = new CameraState(
            translation,
            translation + forward,
            up,
            Math.Clamp(FieldOfView, 1.0f, 179.0f));

        return true;
    }

    private void StoreFreeCameraState()
    {
        if (_camera is null)
            return;

        _storedFreeCameraState = new CameraState(_camera.Position, _camera.Target, _camera.Up, _camera.FieldOfView);
        _hasStoredFreeCameraState = true;
    }

    private void RestoreStoredFreeCameraState()
    {
        if (_camera is null || !_hasStoredFreeCameraState)
            return;

        ApplyCameraState(_storedFreeCameraState);
        _cameraController?.SynchronizeFromCamera();
        _hasStoredFreeCameraState = false;
    }

    private void ApplyCameraState(in CameraState cameraState)
    {
        if (_camera is null)
            return;

        _camera.Position = cameraState.Position;
        _camera.Target = cameraState.Target;
        _camera.Up = cameraState.Up;
        _camera.FieldOfView = cameraState.FieldOfView;

        if (!_hasExplicitFieldOfView)
            SyncFieldOfViewFromCamera();
    }

    private bool IsBoneViewActive() => TryGetSelectedViewBone(out _);

    private void ConfigureCameraClipPlanes(SceneBoundsInfo bounds)
    {
        if (_camera is null)
            return;

        CameraClipPlanes.Configure(_camera, bounds);
    }

    private static void ApplySceneTransformToCamera(Camera camera, Matrix4x4 sceneTransform)
    {
        camera.Position = Vector3.Transform(camera.Position, sceneTransform);
        camera.Target = Vector3.Transform(camera.Target, sceneTransform);
        Vector3 transformedUp = Vector3.TransformNormal(camera.Up, sceneTransform);
        if (transformedUp.LengthSquared() > 1e-12f)
            camera.Up = Vector3.Normalize(transformedUp);
    }

    private static IReadOnlyList<string> EnumerateAnimationNames(Scene scene)
    {
        return scene.RootNode.EnumerateDescendants<SkeletonAnimation>()
            .Select(animation => animation.Name)
            .Concat(scene.RootNode.EnumerateDescendants<BlendShapeAnimation>().Select(animation => animation.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> EnumerateViewBoneNames(Scene scene)
    {
        return scene.RootNode.EnumerateDescendants<SkeletonBone>()
            .Select(bone => bone.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private (int Width, int Height) GetFramebufferSize()
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        double scaling = topLevel?.RenderScaling ?? 1.0;
        int width = Math.Max((int)Math.Ceiling(Bounds.Width * scaling), 1);
        int height = Math.Max((int)Math.Ceiling(Bounds.Height * scaling), 1);
        return (width, height);
    }

    private float GetAspectRatio()
    {
        (int width, int height) = GetFramebufferSize();
        return height > 0 ? width / (float)height : 16.0f / 9.0f;
    }

    private static RendererColor ToRendererColor(AvaloniaColor color)
    {
        return new RendererColor(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            color.A / 255.0f);
    }

    private static bool ResolveEnvironmentMapFlipY(EnvironmentMapFlipMode flipMode)
    {
        return flipMode switch
        {
            EnvironmentMapFlipMode.ForceFlipY => true,
            EnvironmentMapFlipMode.ForceNoFlipY => false,
            _ => true,
        };
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static Vector3 GetAxisVector(BoneViewAxis axis)
    {
        return axis switch
        {
            BoneViewAxis.PositiveX => Vector3.UnitX,
            BoneViewAxis.NegativeX => -Vector3.UnitX,
            BoneViewAxis.PositiveY => Vector3.UnitY,
            BoneViewAxis.NegativeY => -Vector3.UnitY,
            BoneViewAxis.PositiveZ => Vector3.UnitZ,
            BoneViewAxis.NegativeZ => -Vector3.UnitZ,
            _ => -Vector3.UnitZ,
        };
    }

    private readonly record struct CameraState(Vector3 Position, Vector3 Target, Vector3 Up, float FieldOfView);
}
