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
/// Describes the playback state of an <see cref="IAudioPlayer"/>.
/// </summary>
public enum AudioPlaybackState
{
    /// <summary>
    /// Playback is stopped or no buffer is loaded.
    /// </summary>
    Stopped,

    /// <summary>
    /// The loaded buffer is playing.
    /// </summary>
    Playing,

    /// <summary>
    /// Playback is paused and can resume from the current position.
    /// </summary>
    Paused,
}
