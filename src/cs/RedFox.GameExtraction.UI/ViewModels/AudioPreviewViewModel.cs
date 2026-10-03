using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.Audio;
using RedFox.Audio.OpenAL;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Plays an <see cref="AudioClip"/> through an <see cref="IAudioPlayer"/> that is opened on first use.
/// </summary>
public sealed partial class AudioPreviewViewModel : ObservableObject, IDisposable
{
    private const int PeakResolution = 8192;

    private readonly CancellationTokenSource _peaksCancellation = new();
    private IAudioPlayer? _player;
    private bool _playerUnavailable;
    private bool _disposed;

    /// <summary>
    /// Gets the clip being previewed.
    /// </summary>
    public AudioClip Clip { get; }

    /// <summary>
    /// Gets the per-channel min/max waveform peaks, which are built in the background and are <see langword="null"/> until then.
    /// </summary>
    [ObservableProperty]
    public partial (float Min, float Max)[][]? Peaks { get; private set; }

    /// <summary>
    /// Gets the sample rate, channel, and frame summary.
    /// </summary>
    public string InfoDisplay { get; }

    /// <summary>
    /// Gets the playback position as a fraction of the duration.
    /// </summary>
    [ObservableProperty]
    public partial double PositionFraction { get; private set; }

    /// <summary>
    /// Gets the position and duration display.
    /// </summary>
    [ObservableProperty]
    public partial string TimeDisplay { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the clip is playing.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; private set; }

    /// <summary>
    /// Gets a value indicating whether playback can be stopped.
    /// </summary>
    [ObservableProperty]
    public partial bool IsActive { get; private set; }

    /// <summary>
    /// Gets or sets the output volume in the range 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets a value indicating whether playback loops.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLooping { get; set; }

    /// <summary>
    /// Gets the error reported by the audio backend, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlay))]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an audio backend is available.
    /// </summary>
    public bool CanPlay => !_playerUnavailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPreviewViewModel"/> class.
    /// </summary>
    /// <param name="clip">The clip to preview.</param>
    public AudioPreviewViewModel(AudioClip clip) : this(clip, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPreviewViewModel"/> class.
    /// </summary>
    /// <param name="clip">The clip to preview.</param>
    /// <param name="autoPlay">Whether playback starts immediately.</param>
    public AudioPreviewViewModel(AudioClip clip, bool autoPlay)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));

        int channels = clip.Format.Channels;
        string encoding = clip.Encoded is { } encoded ? encoded.Codec.Name : DescribeSamples(clip.GetBuffer());
        string length = clip.FrameCount < 0 ? "unknown length" : $"{clip.FrameCount:N0} frames";
        InfoDisplay = $"{clip.Format.SampleRate:N0} Hz  •  {channels} channel{(channels == 1 ? string.Empty : "s")}  •  {encoding}  •  {length}";
        TimeDisplay = $"{FormatTime(TimeSpan.Zero)} / {FormatTime(clip.Duration)}";

        if (clip.IsDecoded || clip.Encoded!.Codec.CanDecode)
        {
            _ = LoadPeaksAsync();
        }
        else
        {
            ErrorMessage = $"{clip.Encoded.Codec.Name} audio cannot be decoded.";
            _playerUnavailable = true;
        }

        if (autoPlay)
        {
            TogglePlayback();
        }
    }

    /// <summary>
    /// Re-reads the player position and state. Call this periodically while the view is visible.
    /// </summary>
    public void RefreshPlayback()
    {
        if (_player is null)
        {
            return;
        }

        AudioPlaybackState state = _player.State;
        TimeSpan position = _player.Position;
        IsPlaying = state == AudioPlaybackState.Playing;
        IsActive = state != AudioPlaybackState.Stopped;
        PositionFraction = Clip.Duration > TimeSpan.Zero ? position / Clip.Duration : 0.0;
        TimeDisplay = $"{FormatTime(position)} / {FormatTime(Clip.Duration)}";
    }

    /// <summary>
    /// Moves the playback position by the given offset, clamped to the clip.
    /// </summary>
    /// <param name="offset">The offset to move by, which is negative to move backwards.</param>
    public void SeekBy(TimeSpan offset)
    {
        if (EnsurePlayer() is { } player)
        {
            TimeSpan position = player.Position + offset;
            player.Position = position < TimeSpan.Zero ? TimeSpan.Zero : position > Clip.Duration ? Clip.Duration : position;
            RefreshPlayback();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _peaksCancellation.Cancel();
        _peaksCancellation.Dispose();
        _player?.Dispose();
        _player = null;
    }

    partial void OnVolumeChanged(double value)
    {
        _player?.Volume = (float)value;
    }

    partial void OnIsLoopingChanged(bool value)
    {
        _player?.IsLooping = value;
    }

    private async Task LoadPeaksAsync()
    {
        CancellationToken cancellationToken = _peaksCancellation.Token;

        try
        {
            Peaks = await Task.Run(() => WaveformPeaks.Build(Clip, PeakResolution, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (EnsurePlayer() is not { } player)
        {
            return;
        }

        if (player.State == AudioPlaybackState.Playing)
        {
            player.Pause();
        }
        else
        {
            player.Play();
        }

        RefreshPlayback();
    }

    [RelayCommand]
    private void Stop()
    {
        _player?.Stop();
        RefreshPlayback();
    }

    [RelayCommand]
    private void Seek(double fraction)
    {
        if (EnsurePlayer() is { } player)
        {
            player.Position = Clip.Duration * Math.Clamp(fraction, 0.0, 1.0);
            RefreshPlayback();
        }
    }

    private static string DescribeSamples(AudioBuffer buffer)
    {
        return $"{buffer.ValidBitsPerSample}-bit {(SampleFormatInfo.IsFloat(buffer.SampleFormat) ? "float" : "PCM")}";
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss\.ff");
    }

    private IAudioPlayer? EnsurePlayer()
    {
        if (_player is not null || _playerUnavailable || _disposed)
        {
            return _player;
        }

        try
        {
            IAudioPlayer player = new OpenAlAudioPlayer { Volume = (float)Volume, IsLooping = IsLooping };
            player.Load(Clip);
            _player = player;
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            _playerUnavailable = true;
            ErrorMessage = exception.Message;
        }

        return _player;
    }
}
