using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Cameras;
using RedFox.Graphics3D.OpenGL.Controls;
using RedFox.Graphics3D.OpenGL.Viewing;
using RedFox.Graphics3D.Skeletal;
using ViewerSceneUpAxis = RedFox.Graphics3D.OpenGL.Viewing.SceneUpAxis;

namespace RedFox.Graphics3D.Preview.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SceneTranslatorManager _sceneTranslatorManager = TranslatorRegistry.CreateSceneTranslatorManager();
    private CancellationTokenSource? _reloadSceneCts;
    private int _lastEnabledMsaaSamples = 4;

    public MainWindowViewModel(PreviewCliOptions options)
    {
        ImageTranslatorManager = TranslatorRegistry.CreateImageTranslatorManager();
        ViewerController = new SceneViewerController();
        CameraModes = Enum.GetValues<CameraMode>();
        BoneViewAxes = Enum.GetValues<BoneViewAxis>();
        ShadingModes = Enum.GetValues<RendererShadingMode>();
        UpAxisModes = Enum.GetValues<ViewerSceneUpAxis>();
        MsaaSampleOptions = [0, 2, 4, 8];

        LoadedFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLoadedFiles));
        LoadedFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowEmptyState));

        ApplyOptions(options);
    }

    public ImageTranslatorManager ImageTranslatorManager { get; }

    public SceneViewerController ViewerController { get; }

    public ObservableCollection<LoadedFileItem> LoadedFiles { get; } = [];

    public ObservableCollection<AnimationOptionItem> AnimationOptions { get; } =
    [
        new AnimationOptionItem("All animations", null),
    ];

    public IReadOnlyList<CameraMode> CameraModes { get; }

    public IReadOnlyList<BoneViewAxis> BoneViewAxes { get; }

    public IReadOnlyList<RendererShadingMode> ShadingModes { get; }

    public IReadOnlyList<ViewerSceneUpAxis> UpAxisModes { get; }

    public IReadOnlyList<int> MsaaSampleOptions { get; }

    public bool HasLoadedFiles => LoadedFiles.Count > 0;

    public bool ShowEmptyState => !HasLoadedFiles;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasTimeline => AnimationDurationSeconds > 0.0;

    public bool HasSelectedViewBone => !string.IsNullOrWhiteSpace(SelectedViewBoneName);

    public decimal? AnimationTimeSecondsValue
    {
        get => (decimal)AnimationTimeSeconds;
        set
        {
            if (!value.HasValue)
                return;

            double maxValue = AnimationDurationSeconds > 0.0 ? AnimationDurationSeconds : double.MaxValue;
            AnimationTimeSeconds = Math.Clamp((double)value.Value, 0.0, maxValue);
        }
    }

    public decimal AnimationDurationSecondsValue => Math.Max((decimal)AnimationDurationSeconds, 0.0m);

    public decimal AnimationFrameDurationValue => (decimal)GetAnimationFrameDuration();

    public int CurrentAnimationFrame => AnimationFrameRate > 0.0
        ? (int)Math.Round(AnimationTimeSeconds * AnimationFrameRate, MidpointRounding.AwayFromZero)
        : 0;

    public int TotalAnimationFrames => AnimationFrameRate > 0.0
        ? (int)Math.Round(AnimationDurationSeconds * AnimationFrameRate, MidpointRounding.AwayFromZero)
        : 0;

    public string TimelineSummary => HasTimeline
        ? $"Time {AnimationTimeSeconds:F3}s / {AnimationDurationSeconds:F3}s    Frame {CurrentAnimationFrame} / {TotalAnimationFrames} @ {AnimationFrameRate:F2} fps"
        : "No animation data loaded.";

    [ObservableProperty]
    private Scene? scene;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFileCommand))]
    private LoadedFileItem? selectedFile;

    [ObservableProperty]
    private AnimationOptionItem? selectedAnimationOption;

    [ObservableProperty]
    private string? selectedAnimation;

    [ObservableProperty]
    private string? selectedViewBoneName;

    [ObservableProperty]
    private BoneViewAxis selectedViewBoneForwardAxis = BoneViewAxis.NegativeZ;

    [ObservableProperty]
    private BoneViewAxis selectedViewBoneUpAxis = BoneViewAxis.PositiveY;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusText = "Drop models, materials, or animations onto the viewer to start exploring the scene.";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool showBones = true;

    [ObservableProperty]
    private bool showGrid = true;

    [ObservableProperty]
    private bool showWireframe;

    [ObservableProperty]
    private bool showSkybox = true;

    [ObservableProperty]
    private RendererShadingMode shadingMode = RendererShadingMode.Pbr;

    [ObservableProperty]
    private bool enableIBL = true;

    [ObservableProperty]
    private bool environmentMapBlurEnabled;

    [ObservableProperty]
    private float environmentMapBlurRadius = 4.0f;

    [ObservableProperty]
    private string? environmentMapPath;

    [ObservableProperty]
    private float environmentMapExposure = 1.0f;

    [ObservableProperty]
    private float environmentMapReflectionIntensity = 1.0f;

    [ObservableProperty]
    private EnvironmentMapFlipMode environmentMapFlipMode = EnvironmentMapFlipMode.Auto;

    [ObservableProperty]
    private bool normalizeScene;

    [ObservableProperty]
    private float normalizeRadius = 10.0f;

    [ObservableProperty]
    private bool autoFitOnSceneChanged = true;

    [ObservableProperty]
    private int msaaSamples = 4;

    [ObservableProperty]
    private bool isMsaaEnabled = true;

    [ObservableProperty]
    private bool isAnimationPlaying = true;

    [ObservableProperty]
    private float animationSpeed = 1.0f;

    [ObservableProperty]
    private double animationTimeSeconds;

    [ObservableProperty]
    private double animationDurationSeconds;

    [ObservableProperty]
    private double animationFrameRate;

    [ObservableProperty]
    private CameraMode cameraMode = CameraMode.Arcball;

    [ObservableProperty]
    private float cameraFieldOfView = 60.0f;

    [ObservableProperty]
    private ViewerSceneUpAxis upAxis = ViewerSceneUpAxis.Y;

    partial void OnSelectedAnimationOptionChanged(AnimationOptionItem? value)
    {
        SelectedAnimation = value?.Value;
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasErrorMessage));
    }

    partial void OnSelectedViewBoneNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSelectedViewBone));
    }

    partial void OnMsaaSamplesChanged(int value)
    {
        if (value > 1)
        {
            _lastEnabledMsaaSamples = value;

            if (!IsMsaaEnabled)
                IsMsaaEnabled = true;
        }
        else if (IsMsaaEnabled)
        {
            IsMsaaEnabled = false;
        }
    }

    partial void OnIsMsaaEnabledChanged(bool value)
    {
        int targetSamples = value ? Math.Max(_lastEnabledMsaaSamples, 2) : 0;
        if (MsaaSamples != targetSamples)
            MsaaSamples = targetSamples;
    }

    partial void OnAnimationTimeSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(AnimationTimeSecondsValue));
        OnPropertyChanged(nameof(CurrentAnimationFrame));
        OnPropertyChanged(nameof(TimelineSummary));
    }

    partial void OnAnimationDurationSecondsChanged(double value)
    {
        if (value <= 0.0 && AnimationTimeSeconds != 0.0)
            AnimationTimeSeconds = 0.0;
        else if (value > 0.0 && AnimationTimeSeconds > value)
            AnimationTimeSeconds = value;

        OnPropertyChanged(nameof(HasTimeline));
        OnPropertyChanged(nameof(AnimationDurationSecondsValue));
        OnPropertyChanged(nameof(TotalAnimationFrames));
        OnPropertyChanged(nameof(TimelineSummary));
    }

    partial void OnAnimationFrameRateChanged(double value)
    {
        OnPropertyChanged(nameof(AnimationFrameDurationValue));
        OnPropertyChanged(nameof(CurrentAnimationFrame));
        OnPropertyChanged(nameof(TotalAnimationFrames));
        OnPropertyChanged(nameof(TimelineSummary));
    }

    partial void OnCameraFieldOfViewChanged(float value)
    {
        float clamped = Math.Clamp(value, 1.0f, 179.0f);
        if (MathF.Abs(clamped - value) > float.Epsilon)
            CameraFieldOfView = clamped;
    }

    private void ApplyOptions(PreviewCliOptions options)
    {
        ShowBones = options.ShowBones;
        ShowGrid = options.ShowGrid;
        ShowWireframe = options.Wireframe;
        ShowSkybox = options.ShowSkybox;
        ShadingMode = options.ShadingMode;
        AutoFitOnSceneChanged = options.AutoFitOnLoad;
        NormalizeScene = options.NormalizeScene;
        NormalizeRadius = options.NormalizeRadius;
        AnimationSpeed = options.AnimationSpeed;
        CameraMode = options.CameraMode;
        UpAxis = MapUpAxis(options.UpAxis);
        EnvironmentMapPath = options.EnvironmentMapPath;
        EnvironmentMapExposure = options.EnvironmentMapExposure;
        EnvironmentMapReflectionIntensity = options.EnvironmentMapReflectionIntensity;
        EnvironmentMapBlurEnabled = options.EnvironmentMapBlur;
        EnvironmentMapBlurRadius = options.EnvironmentMapBlurRadius;
        EnvironmentMapFlipMode = options.EnvironmentMapFlipMode;
        EnableIBL = options.EnableIBL;
        _lastEnabledMsaaSamples = options.MsaaSamples > 1 ? options.MsaaSamples : 4;
        MsaaSamples = options.MsaaSamples > 1 ? options.MsaaSamples : 0;
        IsMsaaEnabled = MsaaSamples > 1;

        foreach (string inputFile in options.InputFiles)
        {
            string fullPath = Path.GetFullPath(inputFile);
            if (!ContainsFile(fullPath))
                LoadedFiles.Add(new LoadedFileItem(fullPath));
        }

        if (!string.IsNullOrWhiteSpace(options.AnimationName))
            SelectedAnimation = options.AnimationName;

        _ = ReloadSceneAsync();
    }

    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        bool changed = false;
        foreach (string filePath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                continue;

            string fullPath = Path.GetFullPath(filePath);
            if (ContainsFile(fullPath))
                continue;

            LoadedFiles.Add(new LoadedFileItem(fullPath));
            changed = true;
        }

        if (changed)
            await ReloadSceneAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFile))]
    private async Task RemoveSelectedFileAsync()
    {
        if (SelectedFile is null)
            return;

        LoadedFiles.Remove(SelectedFile);
        SelectedFile = null;
        await ReloadSceneAsync();
    }

    private bool CanRemoveSelectedFile() => SelectedFile is not null;

    [RelayCommand]
    private async Task ClearFilesAsync()
    {
        LoadedFiles.Clear();
        SelectedFile = null;
        await ReloadSceneAsync();
    }

    [RelayCommand]
    private void ToggleAnimationPlayback()
    {
        IsAnimationPlaying = !IsAnimationPlaying;
    }

    [RelayCommand]
    private void StepBackwardFrame()
    {
        if (!HasTimeline)
            return;

        IsAnimationPlaying = false;
        AnimationTimeSeconds = Math.Max(0.0, AnimationTimeSeconds - GetAnimationFrameDuration());
    }

    [RelayCommand]
    private void StepForwardFrame()
    {
        if (!HasTimeline)
            return;

        IsAnimationPlaying = false;
        AnimationTimeSeconds = Math.Min(AnimationDurationSeconds, AnimationTimeSeconds + GetAnimationFrameDuration());
    }

    [RelayCommand]
    private void JumpToAnimationStart()
    {
        if (!HasTimeline)
            return;

        IsAnimationPlaying = false;
        AnimationTimeSeconds = 0.0;
    }

    [RelayCommand]
    private void JumpToAnimationEnd()
    {
        if (!HasTimeline)
            return;

        IsAnimationPlaying = false;
        AnimationTimeSeconds = AnimationDurationSeconds;
    }

    [RelayCommand]
    private void FitCamera()
    {
        ViewerController.FitToScene();
    }

    [RelayCommand]
    private void ResetCamera()
    {
        ViewerController.ResetCamera();
    }

    [RelayCommand]
    private void ClearViewBone()
    {
        SelectedViewBoneName = null;
    }

    public void ClearEnvironmentMap()
    {
        EnvironmentMapPath = null;
    }

    private async Task ReloadSceneAsync()
    {
        _reloadSceneCts?.Cancel();
        _reloadSceneCts?.Dispose();
        _reloadSceneCts = new CancellationTokenSource();

        CancellationToken cancellationToken = _reloadSceneCts.Token;
        List<string> filePaths = LoadedFiles.Select(file => file.FullPath).ToList();

        if (filePaths.Count == 0)
        {
            Scene = null;
            RefreshAnimationOptions(null);
            AnimationTimeSeconds = 0.0;
            AnimationDurationSeconds = 0.0;
            AnimationFrameRate = 0.0;
            ErrorMessage = string.Empty;
            StatusText = "Drop models, materials, or animations onto the viewer to start exploring the scene.";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            StatusText = $"Loading {filePaths.Count} file{(filePaths.Count == 1 ? string.Empty : "s")}...";

            Scene loadedScene = await Task.Run(
                () => SceneViewBootstrapper.LoadScene(
                    filePaths,
                    _sceneTranslatorManager,
                    normalizeScene: false,
                    normalizeRadius: NormalizeRadius,
                    upAxis: ViewerSceneUpAxis.Y),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            Scene = loadedScene;
            RefreshAnimationOptions(loadedScene);
            StatusText = BuildSceneSummary(loadedScene, filePaths.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            Scene = null;
            RefreshAnimationOptions(null);
            AnimationTimeSeconds = 0.0;
            AnimationDurationSeconds = 0.0;
            AnimationFrameRate = 0.0;
            ErrorMessage = ex.Message;
            StatusText = "Scene loading failed.";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private void RefreshAnimationOptions(Scene? loadedScene)
    {
        string? requestedAnimation = SelectedAnimation;

        AnimationOptions.Clear();
        AnimationOptions.Add(new AnimationOptionItem("All animations", null));

        if (loadedScene is not null)
        {
            foreach (string animationName in loadedScene.RootNode.EnumerateDescendants<SkeletonAnimation>()
                         .Select(animation => animation.Name)
                         .Concat(loadedScene.RootNode.EnumerateDescendants<BlendShapeAnimation>().Select(animation => animation.Name))
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                AnimationOptions.Add(new AnimationOptionItem(animationName, animationName));
            }
        }

        SelectedAnimationOption = AnimationOptions
            .FirstOrDefault(option => string.Equals(option.Value, requestedAnimation, StringComparison.OrdinalIgnoreCase))
            ?? AnimationOptions[0];
    }

    private string BuildSceneSummary(Scene loadedScene, int fileCount)
    {
        int meshCount = loadedScene.RootNode.EnumerateDescendants<Mesh>().Count();
        int materialCount = loadedScene.RootNode.EnumerateDescendants<Material>().Count();
        int textureCount = loadedScene.RootNode.EnumerateDescendants<Texture>().Count();
        int animationCount = loadedScene.RootNode.EnumerateDescendants<SkeletonAnimation>().Count()
            + loadedScene.RootNode.EnumerateDescendants<BlendShapeAnimation>().Count();

        return $"Loaded {fileCount} file{(fileCount == 1 ? string.Empty : "s")} with {meshCount} mesh(es), {materialCount} material(s), {textureCount} texture(s), and {animationCount} animation track(s).";
    }

    private bool ContainsFile(string fullPath)
    {
        return LoadedFiles.Any(file => string.Equals(file.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private double GetAnimationFrameDuration()
    {
        return AnimationFrameRate > 0.0 ? 1.0 / AnimationFrameRate : 1.0 / 30.0;
    }

    private static ViewerSceneUpAxis MapUpAxis(global::RedFox.Graphics3D.Preview.SceneUpAxis upAxis)
    {
        return upAxis switch
        {
            global::RedFox.Graphics3D.Preview.SceneUpAxis.X => ViewerSceneUpAxis.X,
            global::RedFox.Graphics3D.Preview.SceneUpAxis.Z => ViewerSceneUpAxis.Z,
            _ => ViewerSceneUpAxis.Y,
        };
    }
}
