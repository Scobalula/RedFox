using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Drives the 3D preview for one or more scenes: scene and animation selection, viewport fitting, and renderer options.
/// Animation-only payloads can be appended to the displayed model so animations are browsed without reloading the mesh.
/// </summary>
public partial class ScenePreviewViewModel : ObservableObject
{
    private const string AllAnimationsLabel = "All animations";

    private readonly List<Animation> _animations = [];
    private readonly List<Animation> _appendedAnimations = [];
    private Animation? _selectedAnimation;
    private bool _updatingSelection;

    /// <summary>
    /// Occurs when the scene was mutated in a way the renderer cannot observe and a redraw is needed.
    /// </summary>
    public event Action? SceneInvalidated;

    /// <summary>
    /// Gets the available scenes.
    /// </summary>
    public IReadOnlyList<Scene> Scenes { get; }

    /// <summary>
    /// Gets the scene names shown in the scene selector.
    /// </summary>
    public IReadOnlyList<string> SceneNames { get; }

    /// <summary>
    /// Gets a value indicating whether more than one scene is available.
    /// </summary>
    public bool HasMultipleScenes => Scenes.Count > 1;

    /// <summary>
    /// Gets the up-axis options.
    /// </summary>
    public IReadOnlyList<SceneUpAxis> UpAxisOptions { get; } = Enum.GetValues<SceneUpAxis>();

    /// <summary>
    /// Gets the skinning mode options.
    /// </summary>
    public IReadOnlyList<SkinningMode> SkinningModeOptions { get; } = Enum.GetValues<SkinningMode>();

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
    /// Gets the viewport controller bound to the renderer.
    /// </summary>
    [ObservableProperty]
    public partial SceneViewportController? ViewportController { get; private set; }

    /// <summary>
    /// Gets or sets the scene up axis.
    /// </summary>
    [ObservableProperty]
    public partial SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

    /// <summary>
    /// Gets or sets the renderer skinning mode.
    /// </summary>
    [ObservableProperty]
    public partial SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    /// <summary>
    /// Gets or sets a value indicating whether lighting follows the camera.
    /// </summary>
    [ObservableProperty]
    public partial bool UseViewBasedLighting { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether animation playback is paused.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAnimationPaused { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the ground grid is drawn.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

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
    /// Initializes a new instance of the <see cref="ScenePreviewViewModel"/> class.
    /// </summary>
    /// <param name="scenes">The scenes to preview. The first scene is shown initially.</param>
    public ScenePreviewViewModel(IReadOnlyList<Scene> scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        Scenes = scenes;
        SceneNames = [.. scenes.Select(scene => scene.Name)];
        SelectedSceneIndex = scenes.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Copies viewer preferences from another scene preview when a different model replaces it.
    /// </summary>
    internal void CopyViewerSettingsFrom(ScenePreviewViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        UpAxis = other.UpAxis;
        SkinningMode = other.SkinningMode;
        UseViewBasedLighting = other.UseViewBasedLighting;
        IsAnimationPaused = other.IsAnimationPaused;
        ShowGrid = other.ShowGrid;
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

        _selectedAnimation = animations.Count == 1 ? animations[0] : null;
        RefreshAnimations(scene);
        ConfigureAnimationPlayers(scene);
        UpdateStats(scene);
        SceneInvalidated?.Invoke();
        return true;
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
        ConfigureAnimationPlayers(scene);
        SceneInvalidated?.Invoke();
    }

    partial void OnUpAxisChanged(SceneUpAxis value)
    {
        if (ActiveScene is { } scene)
        {
            scene.UpAxis = value;
            FitScene(scene, true);
            SceneInvalidated?.Invoke();
        }
    }

    partial void OnIsAnimationPausedChanged(bool value)
    {
        if (ActiveScene is { } scene)
        {
            scene.IsAnimationPaused = value;
        }
    }

    partial void OnShowGridChanged(bool value)
    {
        if (ActiveScene is { } scene)
        {
            scene.Grid.Enabled = value;
            SceneInvalidated?.Invoke();
        }
    }

    private static bool IsModel(Scene scene)
    {
        return scene.EnumerateDescendants<Mesh>().Any() || scene.EnumerateDescendants<SkeletonBone>().Any();
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

    private static SceneViewportController CreateViewportController(Scene scene)
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
            SceneUpAxis.X => Matrix4x4.CreateRotationZ(MathF.PI * 0.5f),
            SceneUpAxis.Z => Matrix4x4.CreateRotationX(-MathF.PI * 0.5f),
            _ => Matrix4x4.Identity,
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

    private static void EnsurePreviewLights(Scene scene, SceneBounds bounds)
    {
        if (scene.EnumerateDescendants<Light>().Any())
        {
            return;
        }

        if (!bounds.IsValid)
        {
            Light fallback = scene.RootNode.AddNode<Light>("PreviewLight_Fallback");
            fallback.Position = new Vector3(2.0f, 3.0f, 1.5f);
            fallback.Color = new Vector3(1.0f, 0.98f, 0.9f);
            fallback.Intensity = 1.0f;
            fallback.Enabled = true;
            return;
        }

        float lightDistance = MathF.Max(bounds.Radius, 1.0f) * 1.85f;
        (Vector3 Direction, Vector3 Color, float Intensity)[] lights =
        [
            (Vector3.Normalize(new Vector3(0.9f, 1.25f, 0.35f)), new Vector3(1.0f, 0.92f, 0.78f), 0.72f),
            (Vector3.Normalize(new Vector3(-1.15f, 0.4f, -0.7f)), new Vector3(0.55f, 0.66f, 0.95f), 0.2f),
            (Vector3.Normalize(new Vector3(-0.2f, 0.95f, 1.15f)), new Vector3(0.82f, 0.88f, 1.0f), 0.34f),
        ];

        for (int index = 0; index < lights.Length; index++)
        {
            Light light = scene.RootNode.AddNode<Light>($"PreviewLight_{index + 1}");
            light.Position = bounds.Center + (lights[index].Direction * lightDistance);
            light.Color = lights[index].Color;
            light.Intensity = lights[index].Intensity;
            light.Enabled = true;
        }
    }

    private void ShowScene(Scene? scene)
    {
        _selectedAnimation = null;
        _appendedAnimations.Clear();
        RefreshAnimations(scene);

        if (scene is null)
        {
            ViewportController = null;
            ActiveScene = null;
            StatsDisplay = string.Empty;
            return;
        }

        scene.UpAxis = UpAxis;
        scene.IsAnimationPaused = IsAnimationPaused;
        scene.Grid.Enabled = ShowGrid;
        SceneViewportController viewportController = CreateViewportController(scene);
        ViewportController = viewportController;
        ConfigureAnimationPlayers(scene);
        FitScene(scene, true);
        ActiveScene = scene;
        UpdateStats(scene);
    }

    private void FitScene(Scene scene, bool fitCamera)
    {
        if (ViewportController is not { } viewportController)
        {
            return;
        }

        SceneBounds bounds = viewportController.RecomputeBounds() ? GetAxisAdjustedBounds(viewportController.Bounds, scene.UpAxis) : SceneBounds.Invalid;
        ConfigureGrid(scene.Grid, bounds);
        EnsurePreviewLights(scene, bounds);
        if (fitCamera && bounds.IsValid)
        {
            viewportController.FitCameraToScene();
        }
    }

    private void ConfigureAnimationPlayers(Scene scene)
    {
        foreach (SceneNode node in scene.EnumerateDescendants())
        {
            node.ResetLiveTransform();
        }

        foreach (Mesh mesh in scene.EnumerateDescendants<Mesh>())
        {
            if (mesh.Morph is { } morph)
            {
                Array.Clear(morph.Weights);
            }
        }

        scene.CreateAnimationPlayers();
        if (_selectedAnimation is { } selected)
        {
            scene.AnimationPlayers.RemoveAll(player => !player.Layers.Exists(layer => ReferenceEquals(layer.Animation, selected)));
        }

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
            _animations.AddRange(scene.EnumerateDescendants<Animation>());
        }

        _updatingSelection = true;
        AnimationNames = [AllAnimationsLabel, .. _animations.Select(animation => animation.Name)];
        SelectedAnimationIndex = _selectedAnimation is null ? 0 : _animations.IndexOf(_selectedAnimation) + 1;
        _updatingSelection = false;
    }

    private void UpdateStats(Scene scene)
    {
        int vertexCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.VertexCount);
        int faceCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.FaceCount);
        int boneCount = scene.EnumerateDescendants<SkeletonBone>().Count();
        StatsDisplay = $"{vertexCount:N0} vertices  •  {faceCount:N0} faces  •  {boneCount:N0} bones  •  {_animations.Count:N0} animations";
    }
}
