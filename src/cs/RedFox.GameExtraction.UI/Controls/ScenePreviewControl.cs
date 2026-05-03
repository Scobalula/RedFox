using System.Globalization;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Avalonia;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.GameExtraction.UI.Controls;

/// <summary>
/// Displays a simple interactive 3D preview for a <see cref="Scene"/> payload.
/// </summary>
public sealed class ScenePreviewControl : UserControl
{
    private const int PreviewLightCount = 3;
    private const double AxisComboBoxWidth = 90.0;
    private const double SkinningComboBoxWidth = 180.0;

    /// <summary>
    /// Defines the <see cref="Scene"/> property.
    /// </summary>
    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<ScenePreviewControl, Scene?>(nameof(Scene));

    private readonly AvaloniaOpenGlRendererControl _rendererControl;
    private readonly TextBlock _animationCountText;
    private readonly CheckBox _gridCheckBox;
    private readonly CheckBox _pauseAnimationCheckBox;
    private readonly TextBlock _fpsText;
    private readonly ComboBox _skinningModeComboBox;
    private readonly ComboBox _upAxisComboBox;
    private readonly TextBlock _vertexCountText;
    private readonly TextBlock _faceCountText;
    private readonly TextBlock _boneCountText;
    private readonly CheckBox _viewLightingCheckBox;
    private readonly List<SkeletonAnimation> _previewAnimations = [];
    private Scene? _previewScene;
    private SceneUpAxis _upAxis = SceneUpAxis.Y;
    private SkinningMode _skinningMode = SkinningMode.Linear;
    private SceneViewportController? _viewportController;
    private bool _isAnimationPaused;
    private double _fpsSampleDuration;
    private int _fpsSampleFrameCount;
    private bool _showGrid = true;
    private bool _useViewBasedLighting;

    static ScenePreviewControl()
    {
        SceneProperty.Changed.AddClassHandler<ScenePreviewControl>((control, args) => control.OnSceneChanged(args.GetNewValue<Scene?>()));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenePreviewControl"/> class.
    /// </summary>
    public ScenePreviewControl()
    {
        _animationCountText = CreateStatsTextBlock();
        _vertexCountText = CreateStatsTextBlock();
        _faceCountText = CreateStatsTextBlock();
        _boneCountText = CreateStatsTextBlock();
        _fpsText = CreateStatsTextBlock();
        _fpsText.Text = "FPS: -";
        _fpsText.FontSize = 12;
        _rendererControl = new AvaloniaOpenGlRendererControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            UseViewBasedLighting = _useViewBasedLighting,
            IsAnimationPaused = _isAnimationPaused,
            SkinningMode = _skinningMode,
        };
        _rendererControl.RenderFrame += OnRendererFrame;
        _viewLightingCheckBox = CreateToggle("View lighting", _useViewBasedLighting, OnViewLightingToggled);
        _pauseAnimationCheckBox = CreateToggle("Pause animation", _isAnimationPaused, OnPauseAnimationToggled);
        _gridCheckBox = CreateToggle("Grid", _showGrid, OnGridToggled);
        _upAxisComboBox = CreateEnumComboBox(Enum.GetValues<SceneUpAxis>(), _upAxis, AxisComboBoxWidth, OnUpAxisChanged);
        _skinningModeComboBox = CreateEnumComboBox(Enum.GetValues<SkinningMode>(), _skinningMode, SkinningComboBoxWidth, OnSkinningModeChanged);

        Border statsBar = new()
        {
            Background = Brush.Parse("#141417"),
            BorderBrush = Brush.Parse("#2C2C31"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                Children =
                {
                    _animationCountText,
                    _vertexCountText,
                    _faceCountText,
                    _boneCountText,
                },
            },
        };

        Border controlBar = new()
        {
            Background = Brush.Parse("#17171A"),
            BorderBrush = Brush.Parse("#2C2C31"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                Children =
                {
                    _viewLightingCheckBox,
                    _pauseAnimationCheckBox,
                    _gridCheckBox,
                    CreateLabeledControl("Up axis", _upAxisComboBox),
                    CreateLabeledControl("Skinning", _skinningModeComboBox),
                },
            },
        };

        Border fpsOverlay = new()
        {
            Background = Brush.Parse("#B8141417"),
            BorderBrush = Brush.Parse("#66303036"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Child = _fpsText,
        };

        global::Avalonia.Controls.Grid rendererHost = new();
        rendererHost.Children.Add(_rendererControl);
        rendererHost.Children.Add(fpsOverlay);

        global::Avalonia.Controls.Grid root = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
        };
        global::Avalonia.Controls.Grid.SetRow(statsBar, 0);
        global::Avalonia.Controls.Grid.SetRow(controlBar, 1);
        global::Avalonia.Controls.Grid.SetRow(rendererHost, 2);
        root.Children.Add(statsBar);
        root.Children.Add(controlBar);
        root.Children.Add(rendererHost);

        Content = root;
        MinHeight = 240;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        UpdateStats(null);
    }

    /// <summary>
    /// Gets or sets the scene displayed by the preview control.
    /// </summary>
    public Scene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>
    /// Attempts to append animation content from the supplied scene onto the currently displayed scene.
    /// </summary>
    /// <param name="scene">The incoming scene that contains animation nodes.</param>
    /// <returns><see langword="true"/> when animation nodes were merged into the current scene; otherwise, <see langword="false"/>.</returns>
    public bool TryAppendAnimationScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_previewScene is null)
        {
            return false;
        }

        SkeletonAnimation[] animations = [.. scene.EnumerateDescendants<SkeletonAnimation>()];
        if (animations.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < _previewAnimations.Count; index++)
        {
            SkeletonAnimation animation = _previewAnimations[index];
            if (animation.Parent is not null)
            {
                animation.MoveTo(null, ReparentTransformMode.PreserveExisting);
            }

            animation.Dispose();
        }

        _previewAnimations.Clear();

        for (int index = 0; index < animations.Length; index++)
        {
            SkeletonAnimation animation = animations[index];
            EnsureUniqueChildName(_previewScene.RootNode, animation);
            animation.MoveTo(_previewScene.RootNode, ReparentTransformMode.PreserveExisting);
            _previewAnimations.Add(animation);
        }

        if (_viewportController is null || !ReferenceEquals(_viewportController.Scene, _previewScene))
        {
            ConfigureScene(_previewScene, false, true, true, true);
            return true;
        }

        ResetLiveTransforms(_previewScene);
        _previewScene.UpAxis = _upAxis;
        _previewScene.IsAnimationPaused = _isAnimationPaused;
        IReadOnlyList<AnimationPlayer> animationPlayers = _previewScene.CreateAnimationPlayers();
        _viewportController.RefreshAnimatedSceneBounds = animationPlayers.Count > 0;
        ApplyRendererSettings(_previewScene);
        UpdateStats(_previewScene);
        _rendererControl.InvalidateScene();
        return true;
    }

    private static TextBlock CreateStatsTextBlock()
    {
        return new TextBlock
        {
            Foreground = Brush.Parse("#E8E8EA"),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Text = "-",
        };
    }

    private void OnSceneChanged(Scene? scene)
    {
        _previewAnimations.Clear();
        _previewScene = scene;
        ResetFrameStats();
        if (scene is null)
        {
            _viewportController = null;
            _rendererControl.Scene = null;
            _rendererControl.Camera = null;
            _rendererControl.ViewportController = null;
            UpdateStats(null);
            return;
        }

        ConfigureScene(scene, true, true, true, false);
    }

    private static CheckBox CreateToggle(string text, bool isChecked, EventHandler<RoutedEventArgs> handler)
    {
        CheckBox checkBox = new()
        {
            Content = text,
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
        };

        checkBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == ToggleButton.IsCheckedProperty)
            {
                handler(checkBox, new RoutedEventArgs());
            }
        };

        return checkBox;
    }

    private static ComboBox CreateEnumComboBox<TEnum>(IReadOnlyList<TEnum> items, TEnum selectedItem, double width, EventHandler<SelectionChangedEventArgs> handler)
        where TEnum : struct, Enum
    {
        ComboBox comboBox = new()
        {
            ItemsSource = items,
            SelectedItem = selectedItem,
            Width = width,
            VerticalAlignment = VerticalAlignment.Center,
        };

        comboBox.SelectionChanged += handler;
        return comboBox;
    }

    private static Control CreateLabeledControl(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = Brush.Parse("#D8D8DC"),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                control,
            },
        };
    }

    private static OrbitCamera CreatePreviewCamera()
    {
        OrbitCamera camera = new("ScenePreviewCamera")
        {
            AspectRatio = 1280.0f / 720.0f,
            NearPlane = 0.01f,
            FarPlane = 5000.0f,
            FieldOfView = 60.0f,
            LookSensitivity = 1.0f,
            ZoomSensitivity = 1.0f,
            PanSensitivity = 1.0f,
            MoveSpeed = 2.5f,
            BoostMultiplier = 3.0f,
            MinDistance = 0.05f,
            MaxDistance = 1000000.0f,
            UsePitchLimits = false,
            InvertX = true,
            InvertY = true,
        };

        camera.ApplyOrbit();
        return camera;
    }

    private static SceneViewportController CreateViewportController(Scene scene)
    {
        OrbitCamera camera = CreatePreviewCamera();

        SceneViewportController controller = new(scene, camera)
        {
            IncludeNodeInBounds = ShouldIncludeInBounds,
            UpdateClipPlanesFromBounds = true,
        };
        return controller;
    }

    private static bool ShouldIncludeInBounds(SceneNode node)
    {
        if (node is SkeletonBone bone && !bone.ShowSkeletonBone)
        {
            return false;
        }

        return true;
    }

    private static void ResetLiveTransforms(Scene scene)
    {
        foreach (SceneNode node in scene.EnumerateDescendants())
        {
            node.ResetLiveTransform();
        }
    }

    private static void EnsureUniqueChildName(SceneRoot root, SceneNode node)
    {
        if (!root.TryFindChild(node.Name, StringComparison.OrdinalIgnoreCase, out _))
        {
            return;
        }

        string baseName = node.Name;
        int suffix = 2;
        while (root.TryFindChild($"{baseName}_{suffix}", StringComparison.OrdinalIgnoreCase, out _))
        {
            suffix++;
        }

        node.Name = $"{baseName}_{suffix}";
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

        Vector3 min = bounds.Min;
        Vector3 max = bounds.Max;
        Vector3[] corners =
        [
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
        ];

        Vector3 transformedMin = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 transformedMax = new(float.MinValue, float.MinValue, float.MinValue);

        for (int index = 0; index < corners.Length; index++)
        {
            Vector3 transformed = Vector3.Transform(corners[index], sceneAxisMatrix);
            transformedMin = Vector3.Min(transformedMin, transformed);
            transformedMax = Vector3.Max(transformedMax, transformed);
        }

        return new SceneBounds(transformedMin, transformedMax);
    }

    private static void ConfigureGrid(RedFox.Graphics3D.Grid grid, bool enabled, bool hasBounds, SceneBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (hasBounds && bounds.IsValid)
        {
            grid.ConfigureForBounds(bounds);
        }
        else
        {
            grid.Spacing = 1.0f;
            grid.MajorStep = 10;
            grid.LineWidth = 1.1f;
            grid.EdgeLineWidthScale = 1.2f;
            grid.MinimumPixelsBetweenCells = 2.5f;
        }

        grid.Enabled = enabled;
    }

    private static void AddFallbackLight(Scene scene)
    {
        if (scene.EnumerateDescendants<Light>().Any())
        {
            return;
        }

        Light light = scene.RootNode.AddNode<Light>("PreviewLight_Fallback");
        light.Position = new Vector3(2.0f, 3.0f, 1.5f);
        light.Color = new Vector3(1.0f, 0.98f, 0.9f);
        light.Intensity = 1.0f;
        light.Enabled = true;
    }

    private static void EnsurePreviewLights(Scene scene, SceneBounds bounds)
    {
        if (scene.EnumerateDescendants<Light>().Any())
        {
            return;
        }

        float radius = MathF.Max(bounds.Radius, 1.0f);
        Vector3 center = bounds.Center;
        float lightDistance = radius * 1.85f;

        Vector3[] directions =
        [
            Vector3.Normalize(new Vector3(0.9f, 1.25f, 0.35f)),
            Vector3.Normalize(new Vector3(-1.15f, 0.4f, -0.7f)),
            Vector3.Normalize(new Vector3(-0.2f, 0.95f, 1.15f)),
        ];

        Vector3[] colors =
        [
            new Vector3(1.0f, 0.92f, 0.78f),
            new Vector3(0.55f, 0.66f, 0.95f),
            new Vector3(0.82f, 0.88f, 1.0f),
        ];

        float[] intensities = [0.72f, 0.2f, 0.34f];

        for (int index = 0; index < PreviewLightCount; index++)
        {
            Light light = scene.RootNode.AddNode<Light>($"PreviewLight_{index + 1}");
            light.Position = center + (directions[index] * lightDistance);
            light.Color = colors[index];
            light.Intensity = intensities[index];
            light.Enabled = true;
        }
    }

    private void ConfigureScene(Scene scene, bool fitCamera, bool rebuildAnimationPlayers, bool resetLiveTransforms, bool preserveViewportController)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (!preserveViewportController || _viewportController is null || !ReferenceEquals(_viewportController.Scene, scene))
        {
            _viewportController = CreateViewportController(scene);
            fitCamera = true;
        }

        if (resetLiveTransforms)
        {
            ResetLiveTransforms(scene);
        }

        scene.UpAxis = _upAxis;
        scene.IsAnimationPaused = _isAnimationPaused;

        if (rebuildAnimationPlayers)
        {
            IReadOnlyList<AnimationPlayer> animationPlayers = scene.CreateAnimationPlayers();
            _viewportController.RefreshAnimatedSceneBounds = animationPlayers.Count > 0;
        }
        else
        {
            _viewportController.RefreshAnimatedSceneBounds = scene.AnimationPlayers.Count > 0;
        }

        if (_viewportController.RecomputeBounds())
        {
            SceneBounds adjustedBounds = GetAxisAdjustedBounds(_viewportController.Bounds, scene.UpAxis);
            ConfigureGrid(scene.Grid, _showGrid, true, adjustedBounds);
            EnsurePreviewLights(scene, adjustedBounds);
            if (fitCamera)
            {
                _viewportController.FitCameraToScene();
            }
        }
        else
        {
            ConfigureGrid(scene.Grid, _showGrid, false, SceneBounds.Invalid);
            AddFallbackLight(scene);
        }

        _rendererControl.Scene = scene;
        _rendererControl.Camera = _viewportController.Camera;
        _rendererControl.ViewportController = _viewportController;
        ApplyRendererSettings(scene);
        UpdateStats(scene);
        _rendererControl.InvalidateScene();
    }

    private void ApplyRendererSettings(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        scene.UpAxis = _upAxis;
        scene.IsAnimationPaused = _isAnimationPaused;
        scene.Grid.Enabled = _showGrid;
        _rendererControl.UseViewBasedLighting = _useViewBasedLighting;
        _rendererControl.IsAnimationPaused = _isAnimationPaused;
        _rendererControl.SkinningMode = _skinningMode;
    }

    private void RefreshSceneForOptions(bool fitCamera)
    {
        if (_previewScene is null)
        {
            return;
        }

        ConfigureScene(_previewScene, fitCamera, false, false, true);
    }

    private void OnViewLightingToggled(object? sender, RoutedEventArgs args)
    {
        _useViewBasedLighting = _viewLightingCheckBox.IsChecked == true;
        if (_previewScene is null)
        {
            _rendererControl.UseViewBasedLighting = _useViewBasedLighting;
            return;
        }

        ApplyRendererSettings(_previewScene);
        _rendererControl.InvalidateScene();
    }

    private void OnPauseAnimationToggled(object? sender, RoutedEventArgs args)
    {
        _isAnimationPaused = _pauseAnimationCheckBox.IsChecked == true;
        if (_previewScene is null)
        {
            _rendererControl.IsAnimationPaused = _isAnimationPaused;
            return;
        }

        ApplyRendererSettings(_previewScene);
        _rendererControl.InvalidateScene();
    }

    private void OnGridToggled(object? sender, RoutedEventArgs args)
    {
        _showGrid = _gridCheckBox.IsChecked == true;
        if (_previewScene is null)
        {
            return;
        }

        _previewScene.Grid.Enabled = _showGrid;
        _rendererControl.InvalidateScene();
    }

    private void OnUpAxisChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_upAxisComboBox.SelectedItem is not SceneUpAxis upAxis)
        {
            return;
        }

        _upAxis = upAxis;
        RefreshSceneForOptions(true);
    }

    private void OnSkinningModeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_skinningModeComboBox.SelectedItem is not SkinningMode skinningMode)
        {
            return;
        }

        _skinningMode = skinningMode;
        if (_previewScene is null)
        {
            _rendererControl.SkinningMode = _skinningMode;
            return;
        }

        ApplyRendererSettings(_previewScene);
        _rendererControl.InvalidateScene();
    }

    private void OnRendererFrame(object? sender, AvaloniaRenderFrameEventArgs args)
    {
        if (args.ElapsedTime <= TimeSpan.Zero)
        {
            return;
        }

        _fpsSampleFrameCount++;
        _fpsSampleDuration += args.ElapsedTime.TotalSeconds;
        if (_fpsSampleDuration < 0.25d)
        {
            return;
        }

        double framesPerSecond = _fpsSampleFrameCount / _fpsSampleDuration;
        _fpsSampleFrameCount = 0;
        _fpsSampleDuration = 0.0d;

        Dispatcher.UIThread.Post(() =>
        {
            _fpsText.Text = $"FPS: {framesPerSecond.ToString("N1", CultureInfo.InvariantCulture)}";
        });
    }

    private void UpdateStats(Scene? scene)
    {
        if (scene is null)
        {
            _animationCountText.Text = "Animations: -";
            _vertexCountText.Text = "Vertices: -";
            _faceCountText.Text = "Faces: -";
            _boneCountText.Text = "Bones: -";
            return;
        }

        int animationCount = scene.EnumerateDescendants<SkeletonAnimation>().Count();
        int vertexCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.VertexCount);
        int faceCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.FaceCount);
        int boneCount = scene.EnumerateDescendants<SkeletonBone>().Count();

        _animationCountText.Text = $"Animations: {animationCount.ToString("N0", CultureInfo.InvariantCulture)}";
        _vertexCountText.Text = $"Vertices: {vertexCount.ToString("N0", CultureInfo.InvariantCulture)}";
        _faceCountText.Text = $"Faces: {faceCount.ToString("N0", CultureInfo.InvariantCulture)}";
        _boneCountText.Text = $"Bones: {boneCount.ToString("N0", CultureInfo.InvariantCulture)}";
    }

    private void ResetFrameStats()
    {
        _fpsSampleFrameCount = 0;
        _fpsSampleDuration = 0.0d;
        _fpsText.Text = "FPS: -";
    }
}