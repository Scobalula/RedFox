// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression.Deflate;

/// <summary>
/// Specifies the compression effort used by the Deflate codecs.
/// </summary>
public enum DeflateLevel
{
    /// <summary>
    /// Disables compression.
    /// </summary>
    NoCompression		= 0,

    /// <summary>
    /// Prioritizes compression speed.
    /// </summary>
    BestSpeed			= 1,

    /// <summary>
    /// Prioritizes compression ratio.
    /// </summary>
    BestCompression		= 9,

    /// <summary>
    /// Uses the standard compression level.
    /// </summary>
    DefaultLevel		= 6,

    /// <summary>
    /// Uses the library's default compression level.
    /// </summary>
    DefaultCompression	= -1
}
