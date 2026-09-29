using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Polls the <see cref="AudioPreviewViewModel"/> playback position while the view is visible.
/// </summary>
public partial class AudioPreviewView : UserControl
{
    private readonly DispatcherTimer _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPreviewView"/> class.
    /// </summary>
    public AudioPreviewView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => (DataContext as AudioPreviewViewModel)?.RefreshPlayback());
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
        (DataContext as AudioPreviewViewModel)?.Dispose();
    }
}
