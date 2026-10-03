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
/// Provides optional configuration for audio encoding operations.
/// </summary>
public class AudioEncoderOptions
{
    /// <summary>
    /// Gets or sets the target bitrate in bits per second, or <see langword="null"/> to use the codec's default.
    /// </summary>
    public int? Bitrate { get; set; }

    /// <summary>
    /// Gets or sets the quality level (typically 0-10), or <see langword="null"/> to use the codec's default.
    /// </summary>
    public int? Quality { get; set; }
}
