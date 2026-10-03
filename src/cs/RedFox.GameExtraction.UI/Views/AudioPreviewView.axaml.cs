using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Polls the <see cref="AudioPreviewViewModel"/> playback position while the view is visible, and maps Space to play/pause and the arrow keys to seeking.
/// </summary>
public partial class AudioPreviewView : UserControl, IPreviewKeyHandler
{
    private static readonly TimeSpan SeekStep = TimeSpan.FromSeconds(5);

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
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        if (DataContext is not AudioPreviewViewModel viewModel || modifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (key)
        {
            case Key.Space:
                viewModel.TogglePlaybackCommand.Execute(null);
                return true;
            case Key.Left:
                viewModel.SeekBy(-SeekStep);
                return true;
            case Key.Right:
                viewModel.SeekBy(SeekStep);
                return true;
            default:
                return false;
        }
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
    }
}
