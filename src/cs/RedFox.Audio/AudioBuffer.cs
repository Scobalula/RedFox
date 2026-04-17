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
/// Represents a buffer of decoded PCM audio samples with associated format metadata.
/// Samples are stored as 16-bit signed integers in interleaved channel order.
/// </summary>
public sealed class AudioBuffer
{
    /// <summary>
    /// Gets the sample rate in samples per second.
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Gets the number of audio channels.
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Gets the decoded 16-bit PCM samples in interleaved channel order.
    /// </summary>
    public Memory<short> Samples { get; init; }

    /// <summary>
    /// Gets the total duration of the audio buffer.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / Channels / SampleRate);
}
