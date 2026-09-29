using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.Audio;
using RedFox.Audio.OpenAL;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Plays an <see cref="AudioBuffer"/> through an <see cref="IAudioPlayer"/> that is opened on first use.
/// </summary>
public sealed partial class AudioPreviewViewModel : ObservableObject, IDisposable
{
    private IAudioPlayer? _player;
    private bool _playerUnavailable;

    /// <summary>
    /// Gets the buffer being previewed.
    /// </summary>
    public AudioBuffer Audio { get; }

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
    /// Gets a value indicating whether the buffer is playing.
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
    /// <param name="audio">The buffer to preview.</param>
    public AudioPreviewViewModel(AudioBuffer audio)
    {
        Audio = audio ?? throw new ArgumentNullException(nameof(audio));
        int frameCount = audio.Samples.Length / Math.Max(1, audio.Channels);
        InfoDisplay = $"{audio.SampleRate:N0} Hz  •  {audio.Channels} channel{(audio.Channels == 1 ? string.Empty : "s")}  •  16-bit PCM  •  {frameCount:N0} frames";
        TimeDisplay = $"{FormatTime(TimeSpan.Zero)} / {FormatTime(audio.Duration)}";
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
        TimeSpan position = state == AudioPlaybackState.Stopped ? TimeSpan.Zero : _player.Position;
        IsPlaying = state == AudioPlaybackState.Playing;
        IsActive = state != AudioPlaybackState.Stopped;
        PositionFraction = Audio.Duration > TimeSpan.Zero ? position / Audio.Duration : 0.0;
        TimeDisplay = $"{FormatTime(position)} / {FormatTime(Audio.Duration)}";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
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
            player.Position = Audio.Duration * Math.Clamp(fraction, 0.0, 1.0);
            RefreshPlayback();
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss\.ff");
    }

    private IAudioPlayer? EnsurePlayer()
    {
        if (_player is not null || _playerUnavailable)
        {
            return _player;
        }

        try
        {
            IAudioPlayer player = new OpenAlAudioPlayer { Volume = (float)Volume, IsLooping = IsLooping };
            player.Load(Audio);
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
