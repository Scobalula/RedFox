using System.ComponentModel;
using System.Numerics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Drives the 3D preview for one or more scenes: scene and animation selection and viewport fitting. Viewer preferences live in the
/// shared <see cref="PreviewSettings"/>. Animation-only payloads can be appended to the displayed model so animations are browsed without reloading the mesh.
/// </summary>
public partial class ScenePreviewViewModel : ObservableObject, IDisposable
{
    private const string AllAnimationsLabel = "All animations";

    private static readonly Vector3 FitViewDirection = Vector3.Normalize(new Vector3(1.0f, -0.28f, -0.2f));

    private readonly List<Animation> _animations = [];
    private readonly List<Animation> _appendedAnimations = [];
    private readonly IReadOnlyDictionary<Scene, ScenePreviewBounds> _preparedSceneBounds;
    private readonly IReadOnlyDictionary<Scene, ScenePreviewData> _preparedSceneData;
    private readonly HashSet<Scene> _initialScenePreparationPending;
    private readonly HashSet<Scene> _previewLightsAddedScenes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Scene, AnimationPlayer[]> _allAnimationPlayersByScene = new(ReferenceEqualityComparer.Instance);
    private Animation? _selectedAnimation;
    private bool _updatingSelection;
    private bool _updatingTimeline;
    private AnimationSampler? _timelineLayer;

    /// <summary>
    /// Occurs when the scene was mutated in a way the renderer cannot observe and a redraw is needed.
    /// </summary>
    public event Action? SceneInvalidated;

    /// <summary>
    /// Gets the viewer preferences shared with other scene previews.
    /// </summary>
    public PreviewSettings Settings { get; }

    /// <summary>
    /// Gets or sets the command that clears the preview model selection.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelSelection))]
    public partial ICommand? ClearSelectionCommand { get; set; }

    /// <summary>
    /// Gets whether the preview has a model selection to clear.
    /// </summary>
    public bool HasModelSelection => ClearSelectionCommand is not null;

    internal SceneCombineMode? CombinationMode { get; init; }

    /// <summary>
    /// Gets the available scenes.
    /// </summary>
    public IReadOnlyList<Scene> Scenes { get; }

    /// <summary>
    /// Gets the scene names shown in the scene selector.
    /// </summary>
    public IReadOnlyList<string> SceneNames { get; }

    /// <summary>
    /// Gets the wireframe modes offered by the preview toolbar.
    /// </summary>
    public IReadOnlyList<WireframeMode> WireframeModes { get; } = Enum.GetValues<WireframeMode>();

    /// <summary>
    /// Gets a value indicating whether more than one scene is available.
    /// </summary>
    public bool HasMultipleScenes => Scenes.Count > 1;

    /// <summary>
    /// Gets or sets the index of the displayed scene.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedSceneIndex { get; set; } = -1;

    /// <summary>
    /// Gets the animation names shown in the animation selector, preceded by an entry that plays every animation.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleAnimations))]
    public partial IReadOnlyList<string> AnimationNames { get; private set; } = [AllAnimationsLabel];

    /// <summary>
    /// Gets or sets the selected animation index, where zero plays every animation.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedAnimationIndex { get; set; }

    /// <summary>
    /// Gets the scene bound to the renderer.
    /// </summary>
    [ObservableProperty]
    public partial Scene? ActiveScene { get; private set; }

    /// <summary>
    /// Gets whether the displayed scene is a model that animations can be appended to.
    /// </summary>
    public bool HasModel => ActiveScene is { } scene && IsModel(scene);

    /// <summary>
    /// Gets the viewport controller bound to the renderer.
    /// </summary>
    [ObservableProperty]
    public partial SceneViewportController? ViewportController { get; private set; }

    /// <summary>
    /// Gets the vertex, face, bone, and animation summary of the displayed scene.
    /// </summary>
    [ObservableProperty]
    public partial string StatsDisplay { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the displayed scene has more than one animation.
    /// </summary>
    public bool HasMultipleAnimations => AnimationNames.Count > 2;

    /// <summary>
    /// Gets the available playback action.
    /// </summary>
    public string PlaybackAction => Settings.IsAnimationPaused ? "Play" : "Pause";

    /// <summary>
    /// Gets whether the scene contains animations.
    /// </summary>
    public bool HasAnimations => _animations.Count > 0;

    /// <summary>
    /// Gets the playback duration in seconds.
    /// </summary>
    [ObservableProperty]
    public partial double AnimationDuration { get; private set; }

    /// <summary>
    /// Gets or sets the playback position in seconds.
    /// </summary>
    [ObservableProperty]
    public partial double AnimationPosition { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenePreviewViewModel"/> class.
    /// </summary>
    /// <param name="scenes">The scenes to preview. The first scene is shown initially.</param>
    /// <param name="settings">The viewer preferences shared with other scene previews.</param>
    /// <param name="preparedSceneBounds">Optional scene bounds computed before the scenes reach the UI thread.</param>
    /// <param name="preparedSceneData">Optional scene traversal data computed before the scenes reach the UI thread.</param>
    public ScenePreviewViewModel(
        IReadOnlyList<Scene> scenes,
        PreviewSettings settings,
        IReadOnlyDictionary<Scene, ScenePreviewBounds>? preparedSceneBounds = null,
        IReadOnlyDictionary<Scene, ScenePreviewData>? preparedSceneData = null)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _preparedSceneBounds = preparedSceneBounds ?? new Dictionary<Scene, ScenePreviewBounds>(ReferenceEqualityComparer.Instance);
        _preparedSceneData = preparedSceneData ?? new Dictionary<Scene, ScenePreviewData>(ReferenceEqualityComparer.Instance);
        _initialScenePreparationPending = new HashSet<Scene>(_preparedSceneData.Keys, ReferenceEqualityComparer.Instance);
        foreach (Scene scene in _preparedSceneData.Keys)
        {
            _allAnimationPlayersByScene[scene] = [.. scene.AnimationPlayers];
        }
        Settings.PropertyChanged += OnSettingsChanged;
        Scenes = scenes;
        SceneNames = [.. scenes.Select(scene => scene.Name)];
        SelectedSceneIndex = scenes.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Stops observing the shared settings so the preview can be collected.
    /// </summary>
    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Moves the animations of animation-only scenes onto the displayed model, replacing previously appended animations.
    /// </summary>
    /// <param name="scenes">The incoming scenes.</param>
    /// <returns><see langword="true"/> when the displayed scene is a model and every incoming scene only carried animations.</returns>
    public bool TryAppendAnimations(IReadOnlyList<Scene> scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        if (ActiveScene is not { } scene || !IsModel(scene) || scenes.Count == 0 || scenes.Any(IsModel))
        {
            return false;
        }

        List<Animation> animations = [.. scenes.SelectMany(incoming => incoming.EnumerateDescendants<Animation>())];
        if (animations.Count == 0)
        {
            return false;
        }

        int previousAppendedAnimationCount = _appendedAnimations.Count;
        long sceneVersionBeforeAppending = scene.Version;
        foreach (Animation animation in _appendedAnimations)
        {
            animation.MoveTo(null, ReparentTransformMode.PreserveExisting);
            animation.Dispose();
        }

        _appendedAnimations.Clear();
        foreach (Animation animation in animations)
        {
            EnsureUniqueChildName(scene.RootNode, animation);
            animation.MoveTo(scene.RootNode, ReparentTransformMode.PreserveExisting);
            _appendedAnimations.Add(animation);
        }

        if (previousAppendedAnimationCount == 0)
        {
            SceneTraversal.AppendPreparedPostOrder(scene, animations.Cast<SceneNode>().ToArray(), sceneVersionBeforeAppending);
        }

        _selectedAnimation = animations[0];
        RefreshAnimations(scene);
        ConfigureAnimationPlayers(scene, rebuildPlayers: true, resetPose: true);
        UpdateStats(scene);

        if (Settings.AutoFitScene && ViewportController is { } viewportController)
        {
            viewportController.FitCameraAfterNextUpdate = true;
        }

        SceneInvalidated?.Invoke();
        return true;
    }

    /// <summary>
    /// Updates the timeline from the active animation player.
    /// </summary>
    public void RefreshTimeline()
    {
        AnimationSampler? layer	= _timelineLayer;
        _updatingTimeline		= true;
        AnimationPosition		= layer is { FrameRate: > 0 } ? layer.CurrentTime / layer.FrameRate : 0;
        _updatingTimeline		= false;
    }

    /// <summary>
    /// Gets the yaw of the view cube's front face, which follows the model front setting.
    /// </summary>
    public float ViewCubeFrontYaw => PreviewSettings.GetFrontYaw(Settings.UpAxis, Settings.ModelFront);

    /// <summary>
    /// Fits the preview camera to the active scene and keeps the angle it looks from.
    /// </summary>
    public void FrameSceneInView()
    {
        ViewportController?.FrameCameraToScene();
        CaptureCameraState();
        SceneInvalidated?.Invoke();
    }

    /// <summary>
    /// Moves to the next up axis.
    /// </summary>
    public void CycleUpAxis()
    {
        Settings.UpAxis = Settings.UpAxis switch
        {
            SceneUpAxis.X	=> SceneUpAxis.Y,
            SceneUpAxis.Y	=> SceneUpAxis.Z,
            _				=> SceneUpAxis.X,
        };
        Settings.RequestSave();
    }

    /// <summary>
    /// Fits the preview camera to the active scene.
    /// </summary>
    public void FitSceneToView()
    {
        if (ActiveScene is { } scene)
        {
            FitScene(scene, true);
            CaptureCameraState();
            SceneInvalidated?.Invoke();
        }
    }

    /// <summary>
    /// Saves the current orbit so it can be restored when another model is previewed.
    /// </summary>
    public void CaptureCameraState()
    {
        if (ViewportController is not { } viewportController)
        {
            return;
        }

        OrbitCamera camera		= viewportController.Camera;
        Settings.CameraState	= new ScenePreviewCameraState(camera.OrbitTarget, camera.YawRadians, camera.PitchRadians, camera.Distance);
    }

    partial void OnAnimationPositionChanged(double value)
    {
        if (_updatingTimeline || ActiveScene is not { } scene)
        {
            return;
        }

        Settings.IsAnimationPaused = true;
        foreach (AnimationPlayer player in scene.AnimationPlayers)
        {
            player.Update((float)Math.Clamp(value, 0, AnimationDuration) * player.FrameRate, AnimationSampleType.AbsoluteFrameTime);
        }
        SceneInvalidated?.Invoke();
    }

    partial void OnSelectedSceneIndexChanged(int value)
    {
        ShowScene(value >= 0 && value < Scenes.Count ? Scenes[value] : null);
    }

    partial void OnSelectedAnimationIndexChanged(int value)
    {
        if (_updatingSelection || ActiveScene is not { } scene)
        {
            return;
        }

        _selectedAnimation = value > 0 && value <= _animations.Count ? _animations[value - 1] : null;
        ConfigureAnimationPlayers(scene, rebuildPlayers: _appendedAnimations.Count > 0);
        if (_appendedAnimations.Count == 0)
        {
            SceneTraversal.RefreshPreparedPostOrderVersion(scene);
        }

        if (Settings.AutoFitScene && ViewportController is { } viewportController)
        {
            viewportController.FitCameraAfterNextUpdate = true;
        }
        SceneInvalidated?.Invoke();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ActiveScene is not { } scene)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(PreviewSettings.UpAxis):
            case nameof(PreviewSettings.ModelFront):
                scene.UpAxis = Settings.UpAxis;
                FitScene(scene, Settings.AutoFitScene);
                OnPropertyChanged(nameof(ViewCubeFrontYaw));
                SceneInvalidated?.Invoke();
                break;

            case nameof(PreviewSettings.IsAnimationPaused):
                OnPropertyChanged(nameof(PlaybackAction));
                scene.IsAnimationPaused = Settings.IsAnimationPaused;
                break;

            case nameof(PreviewSettings.ShowGrid):
                scene.Grid.Enabled = Settings.ShowGrid;
                SceneInvalidated?.Invoke();
                break;

            case nameof(PreviewSettings.SkyboxTexture):
            case nameof(PreviewSettings.ShowSkybox):
            case nameof(PreviewSettings.SkyboxBlur):
            case nameof(PreviewSettings.SkyboxRotation):
                ConfigureSkybox(scene.Skybox);
                if (e.PropertyName == nameof(PreviewSettings.SkyboxTexture) && _previewLightsAddedScenes.Contains(scene))
                {
                    foreach (Light light in scene.EnumerateDescendants<Light>().Where(light => light.Name.StartsWith("PreviewLight_", StringComparison.Ordinal)))
                    {
                        light.Enabled = !Settings.HasSkybox;
                    }
                }
                SceneInvalidated?.Invoke();
                break;

            case nameof(PreviewSettings.ShowBones):
                SetBoneVisibility(scene, Settings.ShowBones);
                if (_appendedAnimations.Count == 0)
                {
                    SceneTraversal.RefreshPreparedPostOrderVersion(scene);
                }

                RecomputeBoundsAndFitCamera(Settings.AutoFitScene);
                SceneInvalidated?.Invoke();
                break;

            case nameof(PreviewSettings.AutoFitScene):
                if (Settings.AutoFitScene)
                {
                    FitScene(scene, true);
                    CaptureCameraState();
                    SceneInvalidated?.Invoke();
                }
                break;
        }
    }

    private static bool IsModel(Scene scene)
    {
        return scene.EnumerateDescendants<Mesh>().Any() || scene.EnumerateDescendants<SkeletonBone>().Any();
    }

    private void SetBoneVisibility(Scene scene, bool isVisible)
    {
        IEnumerable<SkeletonBone> bones = _preparedSceneData.TryGetValue(scene, out ScenePreviewData? preparedData)
            ? preparedData.Bones
            : scene.EnumerateDescendants<SkeletonBone>();
        foreach (SkeletonBone bone in bones)
        {
            bone.ShowSkeletonBone = isVisible;
        }
    }

    private static void EnsureUniqueChildName(SceneNode root, SceneNode node)
    {
        string baseName = node.Name;
        int suffix = 2;
        while (root.TryFindChild(node.Name, StringComparison.OrdinalIgnoreCase, out _))
        {
            node.Name = $"{baseName}_{suffix++}";
        }
    }

    private static SceneViewportController CreateViewportController(Scene scene, ScenePreviewCameraState? cameraState)
    {
        OrbitCamera camera = new("ScenePreviewCamera")
        {
            AspectRatio = 16.0f / 9.0f,
            NearPlane = 0.01f,
            FarPlane = 5000.0f,
            FieldOfView = 60.0f,
            MoveSpeed = 2.5f,
            BoostMultiplier = 3.0f,
            MinDistance = 0.05f,
            MaxDistance = 1000000.0f,
            UsePitchLimits = false,
            InvertX = true,
            InvertY = true,
        };

        if (cameraState is { } state)
        {
            camera.OrbitTarget = state.OrbitTarget;
            camera.YawRadians = state.YawRadians;
            camera.PitchRadians = state.PitchRadians;
            camera.Distance = state.Distance;
        }

        camera.ApplyOrbit();

        return new SceneViewportController(scene, camera)
        {
            IncludeNodeInBounds = node => node is not SkeletonBone { ShowSkeletonBone: false },
            UpdateClipPlanesFromBounds = true,
        };
    }

    private static SceneBounds GetAxisAdjustedBounds(SceneBounds bounds, SceneUpAxis upAxis)
    {
        Matrix4x4 sceneAxisMatrix = upAxis switch
        {
            SceneUpAxis.X	=> Matrix4x4.CreateRotationZ(MathF.PI * 0.5f),
            SceneUpAxis.Z	=> Matrix4x4.CreateRotationX(-MathF.PI * 0.5f),
            _				=> Matrix4x4.Identity,
        };

        if (sceneAxisMatrix == Matrix4x4.Identity)
        {
            return bounds;
        }

        Vector3 transformedMin = new(float.MaxValue);
        Vector3 transformedMax = new(float.MinValue);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = new((corner & 1) == 0 ? bounds.Min.X : bounds.Max.X, (corner & 2) == 0 ? bounds.Min.Y : bounds.Max.Y, (corner & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
            Vector3 transformed = Vector3.Transform(point, sceneAxisMatrix);
            transformedMin = Vector3.Min(transformedMin, transformed);
            transformedMax = Vector3.Max(transformedMax, transformed);
        }

        return new SceneBounds(transformedMin, transformedMax);
    }

    private void ConfigureSkybox(Skybox skybox)
    {
        skybox.Texture			= Settings.SkyboxTexture;
        skybox.Enabled			= Settings.HasSkybox;
        skybox.ShowBackground	= Settings.ShowSkybox;
        skybox.BackgroundBlur	= Settings.SkyboxBlur;
        skybox.Rotation			= float.DegreesToRadians(Settings.SkyboxRotation);
    }

    private static void ConfigureGrid(Grid grid, SceneBounds bounds)
    {
        if (bounds.IsValid)
        {
            grid.ConfigureForBounds(bounds);
            return;
        }

        grid.Spacing = 1.0f;
        grid.MajorStep = 10;
        grid.LineWidth = 1.1f;
        grid.EdgeLineWidthScale = 1.2f;
        grid.MinimumPixelsBetweenCells = 2.5f;
    }

    private static SceneNode[] EnsurePreviewLights(Scene scene, SceneBounds bounds, bool? hasLights = null)
    {
        if (hasLights ?? scene.EnumerateDescendants<Light>().Any())
        {
            return [];
        }

        if (!bounds.IsValid)
        {
            Light fallback = scene.RootNode.AddNode<Light>("PreviewLight_Fallback");
            fallback.Position = new Vector3(2.0f, 3.0f, 1.5f);
            fallback.Color = new Vector3(1.0f, 0.98f, 0.9f);
            fallback.Intensity = 1.0f;
            fallback.Enabled = true;
            return [fallback];
        }

        float lightDistance = MathF.Max(bounds.Radius, 1.0f) * 1.85f;
        (Vector3 Direction, Vector3 Color, float Intensity)[] lights =
        [
            (Vector3.Normalize(new Vector3(0.9f, 1.25f, 0.35f)), new Vector3(1.0f, 0.92f, 0.78f), 0.72f),
            (Vector3.Normalize(new Vector3(-1.15f, 0.4f, -0.7f)), new Vector3(0.55f, 0.66f, 0.95f), 0.2f),
            (Vector3.Normalize(new Vector3(-0.2f, 0.95f, 1.15f)), new Vector3(0.82f, 0.88f, 1.0f), 0.34f),
        ];
        Light[] addedLights = new Light[lights.Length];

        for (int index = 0; index < lights.Length; index++)
        {
            Light light = scene.RootNode.AddNode<Light>($"PreviewLight_{index + 1}");
            addedLights[index] = light;
            light.Position = bounds.Center + (lights[index].Direction * lightDistance);
            light.Color = lights[index].Color;
            light.Intensity = lights[index].Intensity;
            light.Enabled = true;
        }

        return addedLights;
    }

    private void ShowScene(Scene? scene)
    {
        CaptureCameraState();

        if (scene is not null)
        {
            MoveAppendedAnimations(scene);
        }

        _selectedAnimation = _selectedAnimation is not null && _appendedAnimations.Contains(_selectedAnimation) ? _selectedAnimation : _appendedAnimations.FirstOrDefault();
        RefreshAnimations(scene);

        if (scene is null)
        {
            ViewportController	= null;
            ActiveScene			= null;
            StatsDisplay		= string.Empty;
            _timelineLayer		= null;
            AnimationDuration	= 0;
            _updatingTimeline	= true;
            AnimationPosition	= 0;
            _updatingTimeline	= false;
            return;
        }

        scene.UpAxis = Settings.UpAxis;
        scene.TextureMipType = MipType.High;
        scene.IsAnimationPaused = Settings.IsAnimationPaused;
        scene.Grid.Enabled = Settings.ShowGrid;
        ConfigureSkybox(scene.Skybox);
        SetBoneVisibility(scene, Settings.ShowBones);
        bool fitCamera = Settings.AutoFitScene || Settings.CameraState is null;
        SceneViewportController viewportController = CreateViewportController(scene, Settings.CameraState);
        ViewportController = viewportController;
        ConfigureAnimationPlayers(scene, rebuildPlayers: _appendedAnimations.Count > 0);
        if (_appendedAnimations.Count == 0)
        {
            SceneTraversal.RefreshPreparedPostOrderVersion(scene);
        }

        FitScene(scene, fitCamera);
        ActiveScene = scene;
        CaptureCameraState();
        UpdateStats(scene);
    }

    private void MoveAppendedAnimations(Scene scene)
    {
        foreach (Animation animation in _appendedAnimations)
        {
            if (ReferenceEquals(animation.Parent, scene.RootNode))
            {
                continue;
            }

            EnsureUniqueChildName(scene.RootNode, animation);
            animation.MoveTo(scene.RootNode, ReparentTransformMode.PreserveExisting);
        }
    }

    private void FitScene(Scene scene, bool fitCamera)
    {
        if (ViewportController is not { } viewportController)
        {
            return;
        }

        viewportController.FitViewDirection = PreviewSettings.OrientToModelFront(FitViewDirection, scene.UpAxis, Settings.ModelFront);

        SceneBounds sceneBounds;
        if (_preparedSceneData.TryGetValue(scene, out ScenePreviewData? preparedData))
        {
            sceneBounds = Settings.ShowBones ? preparedData.Bounds.WithBones : preparedData.Bounds.WithoutBones;
            viewportController.SetBounds(sceneBounds);
        }
        else if (_preparedSceneBounds.TryGetValue(scene, out ScenePreviewBounds preparedBounds))
        {
            sceneBounds = Settings.ShowBones ? preparedBounds.WithBones : preparedBounds.WithoutBones;
            viewportController.SetBounds(sceneBounds);
        }
        else
        {
            sceneBounds = viewportController.RecomputeBounds() ? viewportController.Bounds : SceneBounds.Invalid;
        }

        SceneBounds bounds = GetAxisAdjustedBounds(sceneBounds, scene.UpAxis);
        ConfigureGrid(scene.Grid, bounds);
        long sceneVersionBeforeLights = scene.Version;
        bool? hasLights = _previewLightsAddedScenes.Contains(scene) ? true : preparedData?.HasLights;

        SceneNode[] addedLights = Settings.HasSkybox ? [] : EnsurePreviewLights(scene, bounds, hasLights);
        if (addedLights.Length > 0)
        {
            _previewLightsAddedScenes.Add(scene);
            SceneTraversal.AppendPreparedPostOrder(scene, addedLights, sceneVersionBeforeLights);
        }

        if (fitCamera && bounds.IsValid)
        {
            viewportController.FitCameraToScene();
        }
    }

    private void RecomputeBoundsAndFitCamera(bool fitCamera)
    {
        if (ViewportController is not { } viewportController)
        {
            return;
        }

        if (viewportController.RecomputeBounds() && fitCamera)
        {
            viewportController.FitCameraToScene();
            CaptureCameraState();
        }
    }

    private void ConfigureAnimationPlayers(Scene scene, bool rebuildPlayers = false, bool resetPose = true)
    {
        bool useWorkerPreparedPose = !rebuildPlayers && _initialScenePreparationPending.Remove(scene);
        if (resetPose && !useWorkerPreparedPose)
        {
            IEnumerable<SceneNode> nodes = _preparedSceneData.TryGetValue(scene, out ScenePreviewData? preparedData)
                ? preparedData.Nodes
                : scene.EnumerateDescendants();
            foreach (SceneNode node in nodes)
            {
                node.ResetLiveTransform();
            }

            IEnumerable<Mesh> meshes = preparedData?.Meshes ?? scene.EnumerateDescendants<Mesh>();
            foreach (Mesh mesh in meshes)
            {
                if (mesh.Morph is { } morph)
                {
                    Array.Clear(morph.Weights);
                }
            }
        }

        if (rebuildPlayers || !_preparedSceneData.ContainsKey(scene))
        {
            if (CombinationMode is { } mode)
            {
                SceneCombiner.BindAnimations(scene, mode, CancellationToken.None);
            }
            else
            {
                scene.CreateAnimationPlayers();
            }
            _allAnimationPlayersByScene[scene] = [.. scene.AnimationPlayers];
        }
        else if (_allAnimationPlayersByScene.TryGetValue(scene, out AnimationPlayer[]? allPlayers))
        {
            scene.AnimationPlayers.Clear();
            scene.AnimationPlayers.AddRange(allPlayers);
        }

        _initialScenePreparationPending.Remove(scene);
        if (resetPose && !useWorkerPreparedPose)
        {
            foreach (AnimationPlayer player in scene.AnimationPlayers)
            {
                player.ResetAll();
            }
        }

        if (_selectedAnimation is { } selected)
        {
            scene.AnimationPlayers.RemoveAll(player => !player.Layers.Exists(layer => ReferenceEquals(layer.Animation, selected)));
        }

        _timelineLayer		= scene.AnimationPlayers.SelectMany(player => player.Layers).MaxBy(layer => layer.Length);
        AnimationDuration	= _timelineLayer?.Length ?? 0;
        _updatingTimeline	= true;
        AnimationPosition	= 0;
        _updatingTimeline	= false;

        if (ViewportController is { } viewportController)
        {
            viewportController.RefreshAnimatedSceneBounds = scene.AnimationPlayers.Count > 0;
        }
    }

    private void RefreshAnimations(Scene? scene)
    {
        _animations.Clear();
        if (scene is not null)
        {
            if (_preparedSceneData.TryGetValue(scene, out ScenePreviewData? preparedData))
            {
                _animations.AddRange(preparedData.Animations);
                _animations.AddRange(_appendedAnimations);
            }
            else
            {
                _animations.AddRange(scene.EnumerateDescendants<Animation>());
            }
        }

        OnPropertyChanged(nameof(HasAnimations));
        _updatingSelection = true;
        AnimationNames = [AllAnimationsLabel, .. _animations.Select(animation => animation.Name)];

        // A new item source can keep or drop the combobox selection, so the index must change for the binding to refresh.
        SelectedAnimationIndex = -1;
        SelectedAnimationIndex = _selectedAnimation is null ? 0 : _animations.IndexOf(_selectedAnimation) + 1;
        _updatingSelection = false;
    }

    private void UpdateStats(Scene scene)
    {
        if (_preparedSceneData.TryGetValue(scene, out ScenePreviewData? preparedData))
        {
            StatsDisplay = $"{preparedData.VertexCount:N0} vertices  •  {preparedData.FaceCount:N0} faces  •  {preparedData.BoneCount:N0} bones  •  {_animations.Count:N0} animations";
            return;
        }

        int vertexCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.VertexCount);
        int faceCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.FaceCount);
        int boneCount = scene.EnumerateDescendants<SkeletonBone>().Count();
        StatsDisplay = $"{vertexCount:N0} vertices  •  {faceCount:N0} faces  •  {boneCount:N0} bones  •  {_animations.Count:N0} animations";
    }
}
