using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Avalonia;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.GameExtraction.UI.Controls;

/// <summary>
/// Displays a simple interactive 3D preview for a <see cref="Scene"/> payload.
/// </summary>
public sealed class ScenePreviewControl : UserControl
{
    /// <summary>
    /// Defines the <see cref="Scene"/> property.
    /// </summary>
    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<ScenePreviewControl, Scene?>(nameof(Scene));

    private readonly AvaloniaOpenGlRendererControl _rendererControl;
    private readonly TextBlock _vertexCountText;
    private readonly TextBlock _faceCountText;
    private readonly TextBlock _boneCountText;

    static ScenePreviewControl()
    {
        SceneProperty.Changed.AddClassHandler<ScenePreviewControl>((control, args) => control.OnSceneChanged(args.GetNewValue<Scene?>()));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenePreviewControl"/> class.
    /// </summary>
    public ScenePreviewControl()
    {
        _vertexCountText = CreateStatsTextBlock();
        _faceCountText = CreateStatsTextBlock();
        _boneCountText = CreateStatsTextBlock();
        _rendererControl = new AvaloniaOpenGlRendererControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            UseViewBasedLighting = true,
            IsAnimationPaused = true,
        };

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
                    _vertexCountText,
                    _faceCountText,
                    _boneCountText,
                },
            },
        };

        global::Avalonia.Controls.Grid root = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        global::Avalonia.Controls.Grid.SetRow(statsBar, 0);
        global::Avalonia.Controls.Grid.SetRow(_rendererControl, 1);
        root.Children.Add(statsBar);
        root.Children.Add(_rendererControl);

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
        _rendererControl.Scene = scene;
        _rendererControl.ViewportController = scene is null ? null : CreateViewportController(scene);
        UpdateStats(scene);
    }

    private static SceneViewportController CreateViewportController(Scene scene)
    {
        OrbitCamera camera = new("ScenePreviewCamera")
        {
            FieldOfView = 55.0f,
            NearPlane = 0.01f,
            FarPlane = 1000.0f,
        };

        SceneViewportController controller = new(scene, camera)
        {
            RefreshAnimatedSceneBounds = true,
            UpdateClipPlanesFromBounds = true,
        };
        controller.RecomputeBounds();
        controller.FitCameraToScene();
        return controller;
    }

    private void UpdateStats(Scene? scene)
    {
        if (scene is null)
        {
            _vertexCountText.Text = "Vertices: -";
            _faceCountText.Text = "Faces: -";
            _boneCountText.Text = "Bones: -";
            return;
        }

        int vertexCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.VertexCount);
        int faceCount = scene.EnumerateDescendants<Mesh>().Sum(mesh => mesh.FaceCount);
        int boneCount = scene.EnumerateDescendants<SkeletonBone>().Count();

        _vertexCountText.Text = $"Vertices: {vertexCount.ToString("N0", CultureInfo.InvariantCulture)}";
        _faceCountText.Text = $"Faces: {faceCount.ToString("N0", CultureInfo.InvariantCulture)}";
        _boneCountText.Text = $"Bones: {boneCount.ToString("N0", CultureInfo.InvariantCulture)}";
    }
}