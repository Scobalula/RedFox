using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// A single attribute key-value pair for the preview details panel.
/// </summary>
public class PreviewAttribute
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

public partial class PreviewWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Preview";

    [ObservableProperty]
    public partial bool DrawSkeleton { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial double CurrentFrame { get; set; }

    [ObservableProperty]
    public partial double MinFrame { get; set; }

    [ObservableProperty]
    public partial double MaxFrame { get; set; }

    [ObservableProperty]
    public partial double FrameRate { get; set; } = 30;

    private Stopwatch? _playbackStopwatch;
    private DispatcherTimer? _playbackTimer;
    private double _playbackStartFrame;

    [ObservableProperty]
    public partial PreviewData? PreviewData { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Caller-provided attributes for the preview details panel.</summary>
    public ObservableCollection<PreviewAttribute> Attributes { get; } = [];

    // Visibility — driven by PreviewData.Type flags with priority: Model3D > Image > Text > Hex
    // Only one content view is shown at a time (highest priority wins)
    public bool ShowModel3DTab => PreviewData?.Type.HasFlag(PreviewType.Model3D) == true;

    public bool ShowImageTab => !ShowModel3DTab
                                && PreviewData?.Type.HasFlag(PreviewType.Image) == true;

    public bool ShowTextTab => !ShowModel3DTab && !ShowImageTab
                               && PreviewData?.Type.HasFlag(PreviewType.Text) == true;

    public bool ShowHexTab => !ShowModel3DTab && !ShowImageTab && !ShowTextTab
                              && PreviewData?.Type.HasFlag(PreviewType.Hex) == true;

    /// <summary>Show a placeholder when nothing is selected.</summary>
    public bool ShowEmptyState => !IsLoading && PreviewData is null && string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Whether there are attributes to display.</summary>
    public bool HasAttributes => Attributes.Count > 0;

    partial void OnPreviewDataChanged(PreviewData? value)
    {
        OnPropertyChanged(nameof(ShowHexTab));
        OnPropertyChanged(nameof(ShowTextTab));
        OnPropertyChanged(nameof(ShowModel3DTab));
        OnPropertyChanged(nameof(ShowImageTab));
        OnPropertyChanged(nameof(ShowEmptyState));

        // Populate attributes from preview data
        Attributes.Clear();
        if (value?.Attributes is not null)
        {
            foreach (var kvp in value.Attributes)
                Attributes.Add(new PreviewAttribute { Key = kvp.Key, Value = kvp.Value });
        }
        OnPropertyChanged(nameof(HasAttributes));
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (value && MaxFrame > MinFrame)
        {
            _playbackStartFrame = CurrentFrame;
            _playbackStopwatch = Stopwatch.StartNew();
            if (_playbackTimer is null)
            {
                _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _playbackTimer.Tick += OnPlaybackTick;
            }
            _playbackTimer.Start();
        }
        else
        {
            _playbackTimer?.Stop();
            _playbackStopwatch?.Stop();
        }
    }

    private void OnPlaybackTick(object? sender, EventArgs e)
    {
        if (_playbackStopwatch is null || FrameRate <= 0) return;
        double elapsed = _playbackStopwatch.Elapsed.TotalSeconds;
        double duration = MaxFrame - MinFrame;
        if (duration <= 0) return;
        CurrentFrame = MinFrame + ((_playbackStartFrame - MinFrame + elapsed * FrameRate) % duration);
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(ShowEmptyState));

    /// <summary>
    /// Update preview for the given set of selected assets.
    /// Previews the first asset; multi-select is for compositing (e.g. animations on a model).
    /// </summary>
    public async Task UpdatePreviewAsync(IReadOnlyList<IAssetEntry> selectedAssets, IPreviewHandler handler)
    {
        if (selectedAssets.Count == 0)
        {
            PreviewData = null;
            Title = "Preview";
            ErrorMessage = string.Empty;
            return;
        }

    }
}
