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
/// Describes the format of compressed audio data, including sample rate, channel count,
/// bit depth, and block alignment.
/// </summary>
public sealed class AudioFormat
{
    /// <summary>
    /// Gets the sample rate in samples per second (e.g. 44100).
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2 for stereo).
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Gets the number of bits per sample in the compressed representation (e.g. 4 for ADPCM).
    /// </summary>
    public int BitsPerSample { get; init; }

    /// <summary>
    /// Gets the block alignment in bytes. For block-based codecs like ADPCM, this is the size
    /// of a single block including the block header.
    /// </summary>
    public int BlockAlign { get; init; }

    /// <summary>
    /// Gets the number of bytes per second of audio data.
    /// </summary>
    public int BytesPerSecond => SampleRate * Channels * BitsPerSample / 8;
}
