// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Flac;

/// <summary>
/// Provides FLAC specific encoder options. <see cref="AudioEncoderOptions.Quality"/> maps to the
/// FLAC compression level (0-8).
/// </summary>
public sealed class FlacEncoderOptions : AudioEncoderOptions
{
    /// <summary>
    /// Gets or sets the bits per sample stored in the FLAC stream (4-32), or <see langword="null"/>
    /// to use the source bit depth when writing through a translator, or 16 otherwise.
    /// </summary>
    public int? BitsPerSample { get; set; }
}
