// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio;

/// <summary>
/// Plays an <see cref="AudioClip"/> through an audio output backend.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    AudioPlaybackState State { get; }

    /// <summary>
    /// Gets the duration of the loaded clip, or <see cref="TimeSpan.Zero"/> when nothing is loaded.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets or sets the playback position within the loaded clip.
    /// </summary>
    TimeSpan Position { get; set; }

    /// <summary>
    /// Gets or sets the output gain in the range 0 to 1.
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    /// Gets or sets whether playback restarts from the beginning when the end of the clip is reached.
    /// </summary>
    bool IsLooping { get; set; }

    /// <summary>
    /// Stops any current playback and loads the supplied clip, which is streamed through its decoder while it plays.
    /// </summary>
    /// <param name="clip">The clip to play.</param>
    void Load(AudioClip clip);

    /// <summary>
    /// Starts or resumes playback of the loaded clip.
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses playback, keeping the current position.
    /// </summary>
    void Pause();

    /// <summary>
    /// Stops playback and rewinds to the beginning.
    /// </summary>
    void Stop();
}
