// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.IO;

/// <summary>
/// Specifies options for writing audio files through a translator.
/// </summary>
public sealed class AudioTranslatorOptions
{
    /// <summary>
    /// Gets or sets the target sample format for writing, or <see langword="null"/> to keep the current format.
    /// </summary>
    public SampleFormat? SampleFormat { get; set; }

    /// <summary>
    /// Gets or sets the codec to use for encoding, or <see langword="null"/> to use the translator's default.
    /// </summary>
    public AudioCodec? Codec { get; set; }

    /// <summary>
    /// Gets or sets the encoder options (e.g., bitrate, quality), or <see langword="null"/> for defaults.
    /// </summary>
    public AudioEncoderOptions? Encoder { get; set; }
}
