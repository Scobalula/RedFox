using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RedFox.GameExtraction.UI.ViewModels;
using RedFox.Graphics3D.Avalonia;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Hosts the OpenGL renderer for a <see cref="ScenePreviewViewModel"/> and forwards its redraw requests.
/// </summary>
public partial class ScenePreviewView : UserControl
{
    private const float SkyboxDegreesPerPixel = 0.5f;

    private ScenePreviewViewModel? _viewModel;
    private Point? _skyboxDragPosition;
    private Point? _contextMenuPosition;
    private double _fpsSampleDuration;
    private int _fpsSampleFrameCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenePreviewView"/> class.
    /// </summary>
    public ScenePreviewView()
    {
        InitializeComponent();
        Renderer.RenderFrame += OnRenderFrame;
        Renderer.AddHandler(PointerPressedEvent, OnRendererPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        Renderer.AddHandler(PointerMovedEvent, OnRendererPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        Renderer.AddHandler(PointerReleasedEvent, OnRendererPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SceneInvalidated -= Renderer.InvalidateScene;
        }

        _viewModel = DataContext as ScenePreviewViewModel;
        _fpsSampleFrameCount = 0;
        _fpsSampleDuration = 0.0;
        FpsText.Text = "FPS: -";

        if (_viewModel is not null)
        {
            _viewModel.SceneInvalidated += Renderer.InvalidateScene;
        }
    }

    private void OnRenderFrame(object? sender, AvaloniaRenderFrameEventArgs args)
    {
        if (_viewModel?.HasAnimations == true)
        {
            Dispatcher.UIThread.Post(() => _viewModel?.RefreshTimeline());
        }

        if (args.ElapsedTime <= TimeSpan.Zero)
        {
            return;
        }

        _fpsSampleFrameCount++;
        _fpsSampleDuration += args.ElapsedTime.TotalSeconds;
        if (_fpsSampleDuration < 0.25)
        {
            return;
        }

        double framesPerSecond = _fpsSampleFrameCount / _fpsSampleDuration;
        _fpsSampleFrameCount = 0;
        _fpsSampleDuration = 0.0;
        Dispatcher.UIThread.Post(() => FpsText.Text = $"FPS: {framesPerSecond.ToString("N1", CultureInfo.InvariantCulture)}");
    }

    private void OnRendererPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(Renderer).Properties.IsRightButtonPressed)
        {
            _contextMenuPosition = e.GetPosition(Renderer);
        }

        if (e.KeyModifiers == KeyModifiers.Shift && e.GetCurrentPoint(Renderer).Properties.IsLeftButtonPressed)
        {
            _skyboxDragPosition = e.GetPosition(Renderer);
        }
    }

    private void OnRendererPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_contextMenuPosition is { } start)
        {
            Point current = e.GetPosition(Renderer);
            if (Math.Abs(current.X - start.X) >= 4 || Math.Abs(current.Y - start.Y) >= 4)
            {
                _contextMenuPosition = null;
            }
        }

        if (_skyboxDragPosition is not { } previous || _viewModel is null)
        {
            return;
        }

        Point position		= e.GetPosition(Renderer);
        float rotation		= _viewModel.Settings.SkyboxRotation + (float)(position.X - previous.X) * SkyboxDegreesPerPixel;
        _skyboxDragPosition	= position;

        _viewModel.Settings.SkyboxRotation = (rotation % 360.0f + 360.0f) % 360.0f;
    }

    private void OnRendererPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right && _contextMenuPosition is { } start)
        {
            _contextMenuPosition	= null;
            Point end				= e.GetPosition(Renderer);
            if (Math.Abs(end.X - start.X) < 4 && Math.Abs(end.Y - start.Y) < 4)
            {
                OpenOptions();
            }
        }

        if (_skyboxDragPosition is null)
        {
            return;
        }

        _skyboxDragPosition = null;
        _viewModel?.Settings.RequestSave();
    }

    private void OnOptionsClick(object? sender, RoutedEventArgs e) => OpenOptions();

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is { } viewModel)
        {
            viewModel.Settings.IsAnimationPaused = !viewModel.Settings.IsAnimationPaused;
        }
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is { } viewModel)
        {
            viewModel.AnimationPosition = 0;
        }
    }

    private void OpenOptions()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        PreviewSettings settings	= viewModel.Settings;
        ContextMenu menu			= new();
        MenuItem fit				= new() { Header = "Fit scene" };
        fit.Click += (_, _) => viewModel.FitSceneToView();
        menu.Items.Add(fit);
        menu.Items.Add(CreateToggle("Auto fit", settings.AutoFitScene, value => settings.AutoFitScene = value));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateChoices("Merge mode", settings.MergeMode, value => settings.MergeMode = value));
        menu.Items.Add(CreateChoices("Wireframe", settings.WireframeMode, value => settings.WireframeMode = value));
        menu.Items.Add(CreateChoices("Shading", settings.LightingMode, value => settings.LightingMode = value));
        menu.Items.Add(CreateToggle("Grid", settings.ShowGrid, value => settings.ShowGrid = value));
        menu.Items.Add(CreateToggle("Vertex normals", settings.ShowNormals, value => settings.ShowNormals = value));
        menu.Items.Add(CreateToggle("Bones", settings.ShowBones, value => settings.ShowBones = value));
        menu.Items.Add(CreateToggle("Skybox background", settings.ShowSkybox, value => settings.ShowSkybox = value));

        MenuItem skyboxes = new()
        {
            Header		= "Skybox",
            IsEnabled	= settings.SkyboxFiles.Count > 0,
        };
        foreach (string path in settings.SkyboxFiles)
        {
            MenuItem item = new()
            {
                Header		= Path.GetFileNameWithoutExtension(path),
                ToggleType	= MenuItemToggleType.Radio,
                IsChecked	= string.Equals(path, settings.SkyboxFile, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) =>
            {
                settings.SkyboxFile = path;
                settings.RequestSave();
            };
            skyboxes.Items.Add(item);
        }
        menu.Items.Add(skyboxes);
        menu.Open(Renderer);
    }

    private MenuItem CreateToggle(string label, bool value, Action<bool> setValue)
    {
        MenuItem item = new()
        {
            Header		= label,
            ToggleType	= MenuItemToggleType.CheckBox,
            IsChecked	= value,
        };
        item.Click += (_, _) =>
        {
            setValue(item.IsChecked);
            _viewModel?.Settings.RequestSave();
        };
        return item;
    }

    private MenuItem CreateChoices<T>(string label, T selected, Action<T> setValue) where T : struct, Enum
    {
        MenuItem menu = new() { Header = label };
        foreach (T value in Enum.GetValues<T>())
        {
            MenuItem item = new()
            {
                Header = value.ToString() switch
                {
                    "InPlace"		=> "Merge in place",
                    "ConnectBones"	=> "Connect bones",
                    _				=> value.ToString(),
                },
                ToggleType	= MenuItemToggleType.Radio,
                IsChecked	= value.Equals(selected),
            };
            item.Click += (_, _) =>
            {
                setValue(value);
                _viewModel?.Settings.RequestSave();
            };
            menu.Items.Add(item);
        }
        return menu;
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => _viewModel?.FitSceneToView();
}
