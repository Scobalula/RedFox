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
    private const double ViewAnimationSeconds = 0.25;

    private static readonly TimeSpan ViewAnimationStep = TimeSpan.FromMilliseconds(16.0);

    private readonly DispatcherTimer _viewAnimationTimer = new() { Interval = ViewAnimationStep };
    private ScenePreviewViewModel? _viewModel;
    private Point? _skyboxDragPosition;
    private Point? _contextMenuPosition;
    private double _fpsSampleDuration;
    private int _fpsSampleFrameCount;
    private ViewCubeOrbit _viewAnimationStart;
    private ViewCubeOrbit _viewAnimationEnd;
    private DateTime _viewAnimationStartTime;
    private ViewCubeOrbit _shownOrbit;

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
        _viewAnimationTimer.Tick += OnViewAnimationTick;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SceneInvalidated -= Renderer.InvalidateScene;
        }

        _viewAnimationTimer.Stop();
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
        Dispatcher.UIThread.Post(UpdateViewCube);

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

    private void UpdateViewCube()
    {
        if (_viewModel?.ViewportController is not { } viewportController)
        {
            return;
        }

        ViewCubeOrbit orbit = new(viewportController.Camera.YawRadians, viewportController.Camera.PitchRadians);
        if (orbit == _shownOrbit)
        {
            return;
        }

        _shownOrbit				= orbit;
        ViewCube.YawRadians		= orbit.YawRadians;
        ViewCube.PitchRadians	= orbit.PitchRadians;
    }

    private void OnViewCubeViewRequested(object? sender, ViewCubeOrbitEventArgs e)
    {
        if (_viewModel?.ViewportController is not { } viewportController)
        {
            return;
        }

        _viewAnimationStart		= new ViewCubeOrbit(viewportController.Camera.YawRadians, WrapPitch(viewportController.Camera.PitchRadians));
        _viewAnimationEnd		= e.Orbit;
        _viewAnimationStartTime	= DateTime.UtcNow;
        _viewAnimationTimer.Start();
    }

    private void OnViewCubeOrbitRequested(object? sender, ViewCubeOrbitEventArgs e)
    {
        if (_viewModel?.ViewportController is not { } viewportController)
        {
            return;
        }

        _viewAnimationTimer.Stop();
        viewportController.Camera.AddOrbit(e.Orbit.YawRadians, e.Orbit.PitchRadians);
        Renderer.InvalidateScene();
        _viewModel.CaptureCameraState();
    }

    private void OnViewCubeResetRequested(object? sender, EventArgs e)
    {
        _viewAnimationTimer.Stop();
        _viewModel?.FitSceneToView();
    }

    private void OnViewCubeFitRequested(object? sender, EventArgs e)
    {
        _viewAnimationTimer.Stop();
        _viewModel?.FrameSceneInView();
    }

    private void OnViewCubeUpAxisRequested(object? sender, EventArgs e)
    {
        _viewAnimationTimer.Stop();
        _viewModel?.CycleUpAxis();
    }

    private void OnViewAnimationTick(object? sender, EventArgs e)
    {
        if (_viewModel?.ViewportController is not { } viewportController)
        {
            _viewAnimationTimer.Stop();
            return;
        }

        float progress	= (float)Math.Min((DateTime.UtcNow - _viewAnimationStartTime).TotalSeconds / ViewAnimationSeconds, 1.0);
        float eased		= progress * progress * (3.0f - (2.0f * progress));

        viewportController.Camera.YawRadians	= float.Lerp(_viewAnimationStart.YawRadians, _viewAnimationEnd.YawRadians, eased);
        viewportController.Camera.PitchRadians	= float.Lerp(_viewAnimationStart.PitchRadians, _viewAnimationEnd.PitchRadians, eased);
        viewportController.Camera.ApplyOrbit();
        Renderer.InvalidateScene();

        if (progress >= 1.0f)
        {
            _viewAnimationTimer.Stop();
            _viewModel.CaptureCameraState();
        }
    }

    private static float WrapPitch(float pitch)
    {
        return MathF.IEEERemainder(pitch, MathF.Tau);
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
                OpenOptions(Renderer, true);
            }
        }

        if (_skyboxDragPosition is null)
        {
            return;
        }

        _skyboxDragPosition = null;
        _viewModel?.Settings.RequestSave();
    }

    private void OnOptionsClick(object? sender, RoutedEventArgs e) => OpenOptions((Control)sender!, false);

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

    private void OnSettingClick(object? sender, RoutedEventArgs e) => _viewModel?.Settings.RequestSave();

    private void OnSkyboxMenuAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not { } viewModel || sender is not MenuItem skyboxes)
        {
            return;
        }

        PreviewSettings settings = viewModel.Settings;
        skyboxes.Items.Clear();
        skyboxes.IsEnabled = settings.SkyboxFiles.Count > 0;
        foreach (string path in settings.SkyboxFiles)
        {
            MenuItem item = new()
            {
                Header		= Path.GetFileNameWithoutExtension(path),
                ToggleType	= MenuItemToggleType.Radio,
                GroupName	= "Skybox",
                IsChecked	= string.Equals(path, settings.SkyboxFile, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) =>
            {
                settings.SkyboxFile = path;
                settings.RequestSave();
            };
            skyboxes.Items.Add(item);
        }
    }

    private void OpenOptions(Control target, bool showAtPointer)
    {
        if (_viewModel is not null)
        {
            ((MenuFlyout)Resources["OptionsMenu"]!).ShowAt(target, showAtPointer);
        }
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => _viewModel?.FitSceneToView();
}
