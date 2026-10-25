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
        if (e.KeyModifiers == KeyModifiers.Shift && e.GetCurrentPoint(Renderer).Properties.IsLeftButtonPressed)
        {
            _skyboxDragPosition = e.GetPosition(Renderer);
        }
    }

    private void OnRendererPointerMoved(object? sender, PointerEventArgs e)
    {
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
        if (_skyboxDragPosition is null)
        {
            return;
        }

        _skyboxDragPosition = null;
        _viewModel?.Settings.RequestSave();
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => _viewModel?.FitSceneToView();
}
