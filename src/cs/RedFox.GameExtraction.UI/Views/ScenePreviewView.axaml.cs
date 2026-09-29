using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using RedFox.GameExtraction.UI.ViewModels;
using RedFox.Graphics3D.Avalonia;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Hosts the OpenGL renderer for a <see cref="ScenePreviewViewModel"/> and forwards its redraw requests.
/// </summary>
public partial class ScenePreviewView : UserControl
{
    private ScenePreviewViewModel? _viewModel;
    private double _fpsSampleDuration;
    private int _fpsSampleFrameCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenePreviewView"/> class.
    /// </summary>
    public ScenePreviewView()
    {
        InitializeComponent();
        Renderer.RenderFrame += OnRenderFrame;
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
}
