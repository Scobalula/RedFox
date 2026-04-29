using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RedFox.Avalonia.Themes;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Avalonia;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Samples.Examples;

internal sealed class AvaloniaAnimationCurvesViewModel : INotifyPropertyChanged
{
    private readonly List<string> _loadedPaths = [];

    private MeshSampleSceneContext? _context;
    private OrbitCamera? _camera;
    private Scene? _scene;
    private SkeletonAnimation? _selectedAnimation;
    private SkeletonAnimationCurveComponent? _selectedComponent;
    private SkeletonAnimationCurveKey? _selectedKey;
    private SceneViewportController? _viewportController;
    private double _currentFrame;
    private bool _isAnimationPaused = true;
    private bool _isUpdatingFrameFromPlayback;
    private double _maximumFrame;
    private double _minimumFrame;
    private bool _showBones = true;
    private bool _showGrid = true;
    private SkinningMode _skinningMode = SkinningMode.Linear;
    private string _status = string.Empty;
    private SceneUpAxis _upAxis = SceneUpAxis.Y;

    public AvaloniaAnimationCurvesViewModel(string[] arguments)
    {
        PlayPauseCommand = new RelayCommand(TogglePlayback, CanUseTimeline);
        StopCommand = new RelayCommand(StopPlayback, CanUseTimeline);
        FitCameraCommand = new RelayCommand(FitCameraToScene, () => ViewportController is not null);
        ClearCommand = new RelayCommand(ClearContent, HasLoadedContent);
        LoadFromArguments(arguments ?? []);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SkeletonAnimation> Animations { get; } = [];

    public IReadOnlyList<SkinningMode> SkinningModes { get; } = Enum.GetValues<SkinningMode>();

    public IReadOnlyList<SceneUpAxis> UpAxes { get; } = Enum.GetValues<SceneUpAxis>();

    public IBrush AxisBrush { get; } = new SolidColorBrush(Color.FromRgb(96, 96, 102));

    public IBrush CurveBrush { get; } = new SolidColorBrush(RedFoxThemeColors.Curve);

    public IBrush GraphBackground { get; } = new SolidColorBrush(RedFoxThemeColors.Background);

    public IBrush GraphBorderBrush { get; } = Brushes.Transparent;

    public IBrush GridBrush { get; } = new SolidColorBrush(RedFoxThemeColors.Border);

    public IBrush KeyBrush { get; } = new SolidColorBrush(RedFoxThemeColors.Curve);

    public IBrush MajorGridBrush { get; } = new SolidColorBrush(Color.FromRgb(51, 51, 56));

    public IBrush PlotBackground { get; } = new SolidColorBrush(RedFoxThemeColors.PlotSurface);

    public IBrush SelectedKeyBrush { get; } = new SolidColorBrush(RedFoxThemeColors.Accent);

    public IBrush SelectedKeyOutlineBrush { get; } = new SolidColorBrush(RedFoxThemeColors.Foreground);

    public Vector4 ClearColor { get; } = RedFoxThemeColors.SceneBackgroundVector;

    public bool UseViewBasedLighting { get; } = true;

    public SkinningMode SkinningMode
    {
        get => _skinningMode;
        set
        {
            if (SetProperty(ref _skinningMode, value))
            {
                ApplySkinningMode();
            }
        }
    }

    public SceneUpAxis UpAxis
    {
        get => _upAxis;
        set
        {
            if (SetProperty(ref _upAxis, value))
            {
                ApplyUpAxis();
            }
        }
    }

    public RelayCommand PlayPauseCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand FitCameraCommand { get; }

    public RelayCommand ClearCommand { get; }

    public Scene? Scene
    {
        get => _scene;
        private set => SetProperty(ref _scene, value);
    }

    public OrbitCamera? Camera
    {
        get => _camera;
        private set => SetProperty(ref _camera, value);
    }

    public SceneViewportController? ViewportController
    {
        get => _viewportController;
        private set
        {
            if (SetProperty(ref _viewportController, value))
            {
                FitCameraCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public SkeletonAnimation? SelectedAnimation
    {
        get => _selectedAnimation;
        set
        {
            if (SetProperty(ref _selectedAnimation, value))
            {
                SelectedKey = null;
                SelectedComponent = null;
                UpdateFrameRange();
                RaiseTimelineStateChanged();
            }
        }
    }

    public SkeletonAnimationCurveComponent? SelectedComponent
    {
        get => _selectedComponent;
        set => SetProperty(ref _selectedComponent, value);
    }

    public SkeletonAnimationCurveKey? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (SetProperty(ref _selectedKey, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedKeySummary)));
            }
        }
    }

    public string SelectedKeySummary => SelectedKey is null
        ? "No key selected"
        : $"{SelectedKey.Component.DisplayName}  Frame {SelectedKey.Frame:0.###}  Value {SelectedKey.Value:0.###}";

    public bool ShowBones
    {
        get => _showBones;
        set
        {
            if (SetProperty(ref _showBones, value))
            {
                ApplyBoneVisibility();
            }
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (SetProperty(ref _showGrid, value))
            {
                ApplyGridVisibility();
            }
        }
    }

    public bool IsAnimationPaused
    {
        get => _isAnimationPaused;
        set
        {
            if (SetProperty(ref _isAnimationPaused, value))
            {
                if (Scene is not null)
                {
                    Scene.IsAnimationPaused = value;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayPauseText)));
            }
        }
    }

    public bool CanScrub => SelectedAnimation is not null;

    public string PlayPauseText => IsAnimationPaused ? "Play" : "Pause";

    public double CurrentFrame
    {
        get => _currentFrame;
        set
        {
            double frame = ClampFrame(value);
            if (SetProperty(ref _currentFrame, frame))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameSummary)));
                if (!_isUpdatingFrameFromPlayback)
                {
                    SeekFrame(frame);
                }
            }
        }
    }

    public double MinimumFrame
    {
        get => _minimumFrame;
        private set
        {
            if (SetProperty(ref _minimumFrame, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameSummary)));
            }
        }
    }

    public double MaximumFrame
    {
        get => _maximumFrame;
        private set
        {
            if (SetProperty(ref _maximumFrame, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameSummary)));
            }
        }
    }

    public string FrameSummary => SelectedAnimation is null
        ? "Frame 0"
        : $"Frame {CurrentFrame:0.###} / {MaximumFrame:0.###}";

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public async Task OpenFilesAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Animation, Skeleton, or Model Files",
            AllowMultiple = true,
            FileTypeFilter = [CreateSceneFileType(), FilePickerFileTypes.All],
        });

        string[] paths = files.Select(file => file.Path.LocalPath).Where(File.Exists).ToArray();
        if (paths.Length == 0)
        {
            Status = "No local files were selected.";
            return;
        }

        LoadFiles(paths);
    }

    public void OnRenderFrame(AvaloniaRenderFrameEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (IsAnimationPaused)
        {
            return;
        }

        AnimationSampler? layer = GetSelectedAnimationLayer();
        AnimationPlayer? player = GetSelectedAnimationPlayer();
        if (layer is null || player is null)
        {
            return;
        }

        ResetSceneLiveTransforms();
        player.Update((float)e.ElapsedTime.TotalSeconds, AnimationSampleType.DeltaTime);
        ViewportController?.RecomputeBounds();
        SetCurrentFrameFromPlayback(layer.CurrentTime);
    }

    private void LoadFromArguments(string[] arguments)
    {
        MeshSampleOptions options = MeshSampleSceneFactory.ParseOptions(arguments);
        if (options.ScenePaths.Count == 0)
        {
            Status = "No animation or skeleton file loaded.";
            return;
        }

        if (!HasSkeletonVisibilityArgument(arguments))
        {
            options.ShowSkeletonBones = true;
        }

        ShowGrid = options.ShowGrid;
        ShowBones = options.ShowSkeletonBones;
        UpAxis = options.UpAxis;
        SkinningMode = options.SkinningMode;
        options.UseViewBasedLighting = UseViewBasedLighting;
        _loadedPaths.Clear();
        AppendLoadedPaths(options.ScenePaths);
        LoadOptions(options, Path.GetFileName(options.ScenePaths[0]));
    }

    private void LoadFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            Status = "No animation or skeleton file loaded.";
            return;
        }

        int addedCount = AppendLoadedPaths(paths);
        if (addedCount == 0)
        {
            Status = "Selected file(s) are already loaded.";
            return;
        }

        MeshSampleOptions options = CreateOptionsForPaths(_loadedPaths);
        LoadOptions(options, Path.GetFileName(paths[0]));
    }

    private void LoadOptions(MeshSampleOptions options, string fallbackSceneName)
    {
        if (!MeshSampleSceneFactory.TryCreate(options, fallbackSceneName, out MeshSampleSceneContext? context, out string? error))
        {
            Status = error ?? "Failed to load animation input(s).";
            return;
        }

        ArgumentNullException.ThrowIfNull(context);
        ApplyContext(context);
    }

    private MeshSampleOptions CreateOptionsForPaths(IEnumerable<string> paths)
    {
        MeshSampleOptions options = new()
        {
            ShowGrid = ShowGrid,
            ShowSkeletonBones = ShowBones,
            UpAxis = UpAxis,
            UseViewBasedLighting = UseViewBasedLighting,
            SkinningMode = SkinningMode,
        };

        foreach (string path in paths)
        {
            options.ScenePaths.Add(path);
        }

        return options;
    }

    private void ApplyContext(MeshSampleSceneContext context)
    {
        _context = context;
        Scene = context.Scene;
        Camera = context.Camera;
        ViewportController = context.ViewportController;
        IsAnimationPaused = true;

        SetProperty(ref _upAxis, context.Scene.UpAxis, nameof(UpAxis));
        SetProperty(ref _skinningMode, context.Options.SkinningMode, nameof(SkinningMode));

        _showGrid = context.Grid?.Enabled == true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowGrid)));

        SkeletonBone[] bones = context.Scene.GetDescendants<SkeletonBone>();
        _showBones = bones.Length == 0 || bones.Any(bone => bone.ShowSkeletonBone);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowBones)));

        RefreshAnimations(context.Scene);
        FitCameraToScene();
        ClearCommand.RaiseCanExecuteChanged();
        Status = BuildLoadedStatus(context, bones.Length);
    }

    private void RefreshAnimations(Scene scene)
    {
        Animations.Clear();
        foreach (SkeletonAnimation animation in scene.EnumerateDescendants<SkeletonAnimation>())
        {
            Animations.Add(animation);
        }

        SelectedAnimation = Animations.Count > 0 ? Animations[0] : null;
        SelectedComponent = null;
        SelectedKey = null;
        RaiseTimelineStateChanged();
    }

    private int AppendLoadedPaths(IEnumerable<string> paths)
    {
        int addedCount = 0;
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(path);
            if (_loadedPaths.Any(loadedPath => loadedPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _loadedPaths.Add(fullPath);
            addedCount++;
        }

        ClearCommand.RaiseCanExecuteChanged();
        return addedCount;
    }

    private void UpdateFrameRange()
    {
        if (SelectedAnimation is null)
        {
            MinimumFrame = 0.0;
            MaximumFrame = 0.0;
            SetCurrentFrameFromPlayback(0.0);
            return;
        }

        (float minFrame, float maxFrame) = SelectedAnimation.GetAnimationFrameRange();
        if (!float.IsFinite(minFrame) || !float.IsFinite(maxFrame) || minFrame == float.MaxValue || maxFrame == float.MinValue)
        {
            minFrame = 0.0f;
            maxFrame = 0.0f;
        }

        if (maxFrame < minFrame)
        {
            maxFrame = minFrame;
        }

        MinimumFrame = minFrame;
        MaximumFrame = maxFrame;
        CurrentFrame = MinimumFrame;
    }

    private void TogglePlayback()
    {
        if (!CanUseTimeline())
        {
            return;
        }

        IsAnimationPaused = !IsAnimationPaused;
        Status = IsAnimationPaused ? "Playback paused." : "Playback started.";
    }

    private void StopPlayback()
    {
        if (!CanUseTimeline())
        {
            return;
        }

        IsAnimationPaused = true;
        CurrentFrame = MinimumFrame;
        Status = "Playback stopped.";
    }

    private void SeekFrame(double frame)
    {
        AnimationPlayer? player = GetSelectedAnimationPlayer();
        if (player is not null)
        {
            ResetSceneLiveTransforms();
            player.Update((float)frame, AnimationSampleType.AbsoluteFrameTime);
            ViewportController?.RecomputeBounds();
        }
    }

    private void ClearContent()
    {
        _loadedPaths.Clear();
        _context = null;
        IsAnimationPaused = true;
        Scene = null;
        Camera = null;
        ViewportController = null;
        Animations.Clear();
        SelectedAnimation = null;
        SelectedComponent = null;
        SelectedKey = null;
        MinimumFrame = 0.0;
        MaximumFrame = 0.0;
        SetCurrentFrameFromPlayback(0.0);
        RaiseTimelineStateChanged();
        ClearCommand.RaiseCanExecuteChanged();
        Status = "Cleared.";
    }

    private void ApplySkinningMode()
    {
        if (_context is not null)
        {
            _context.Options.SkinningMode = SkinningMode;
        }

        if (SelectedAnimation is not null)
        {
            SeekFrame(CurrentFrame);
        }

        Status = $"Skinning: {SkinningMode}.";
    }

    private void ApplyUpAxis()
    {
        if (Scene is null)
        {
            return;
        }

        Scene.UpAxis = UpAxis;
        if (_context is not null)
        {
            _context.Options.UpAxis = UpAxis;
        }

        if (ViewportController is not null)
        {
            ViewportController.RecomputeBounds();
            ViewportController.FitCameraToScene();
        }

        Status = $"Up axis: {UpAxis}.";
    }

    private void SetCurrentFrameFromPlayback(double frame)
    {
        _isUpdatingFrameFromPlayback = true;
        try
        {
            CurrentFrame = frame;
        }
        finally
        {
            _isUpdatingFrameFromPlayback = false;
        }
    }

    private void FitCameraToScene()
    {
        if (ViewportController is null)
        {
            return;
        }

        if (ViewportController.RecomputeBounds() && ViewportController.FitCameraToScene())
        {
            Status = "Camera fit to scene.";
        }
    }

    private void ApplyBoneVisibility()
    {
        if (Scene is null)
        {
            return;
        }

        SkeletonBone[] bones = Scene.GetDescendants<SkeletonBone>();
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].ShowSkeletonBone = ShowBones;
        }

        ViewportController?.RecomputeBounds();
        Status = $"Skeleton bones {(ShowBones ? "visible" : "hidden")}.";
    }

    private void ApplyGridVisibility()
    {
        if (_context?.Grid is null)
        {
            return;
        }

        _context.Grid.Enabled = ShowGrid;
        Status = $"Grid {(ShowGrid ? "visible" : "hidden")}.";
    }

    private AnimationPlayer? GetSelectedAnimationPlayer()
    {
        if (_context is null || SelectedAnimation is null)
        {
            return null;
        }

        foreach (AnimationPlayer player in _context.AnimationPlayers)
        {
            for (int i = 0; i < player.Layers.Count; i++)
            {
                if (ReferenceEquals(player.Layers[i].Animation, SelectedAnimation))
                {
                    return player;
                }
            }
        }

        return null;
    }

    private void ResetSceneLiveTransforms()
    {
        if (Scene is null)
        {
            return;
        }

        foreach (SceneNode node in Scene.EnumerateDescendants())
        {
            node.ResetLiveTransform();
        }
    }

    private AnimationSampler? GetSelectedAnimationLayer()
    {
        AnimationPlayer? player = GetSelectedAnimationPlayer();
        if (player is null || SelectedAnimation is null)
        {
            return null;
        }

        for (int i = 0; i < player.Layers.Count; i++)
        {
            AnimationSampler layer = player.Layers[i];
            if (ReferenceEquals(layer.Animation, SelectedAnimation))
            {
                return layer;
            }
        }

        return null;
    }

    private double ClampFrame(double frame)
    {
        if (MaximumFrame <= MinimumFrame)
        {
            return MinimumFrame;
        }

        return Math.Clamp(frame, MinimumFrame, MaximumFrame);
    }

    private bool CanUseTimeline() => SelectedAnimation is not null;

    private bool HasLoadedContent() => Scene is not null || _loadedPaths.Count > 0;

    private void RaiseTimelineStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanScrub)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayPauseText)));
        PlayPauseCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    private static string BuildLoadedStatus(MeshSampleSceneContext context, int boneCount)
    {
        int animationCount = context.Scene.GetDescendants<SkeletonAnimation>().Length;
        int meshCount = context.Scene.GetDescendants<Mesh>().Length;
        return $"Loaded {animationCount} animation(s), {boneCount} bone(s), {meshCount} mesh(es), {context.AnimationPlayers.Count} player(s).";
    }

    private static bool HasSkeletonVisibilityArgument(string[] arguments)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            string argument = arguments[i];
            if (argument.Equals("--show-skeleton", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("--show-bones", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("--hide-skeleton", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("--hide-bones", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static FilePickerFileType CreateSceneFileType()
    {
        return new FilePickerFileType("RedFox Animation and Scene Files")
        {
            Patterns = ["*.seanim", "*.bvh", "*.md5anim", "*.smd", "*.fbx", "*.ma", "*.cast", "*.semodel", "*.gltf", "*.glb", "*.obj", "*.md5mesh"],
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
