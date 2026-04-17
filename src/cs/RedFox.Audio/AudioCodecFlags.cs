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
/// Flags that describe the capabilities of an <see cref="AudioCodec"/>.
/// </summary>
[Flags]
public enum AudioCodecFlags
{
    /// <summary>
    /// Indicates no extra capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates the codec supports decoding compressed audio to PCM.
    /// </summary>
    SupportsDecoding = 1,

    /// <summary>
    /// Indicates the codec supports encoding PCM to compressed audio.
    /// </summary>
    SupportsEncoding = 2,
}
