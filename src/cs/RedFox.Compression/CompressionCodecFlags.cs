// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression;

/// <summary>
/// Describes the operations and features supported by a compression codec.
/// </summary>
[Flags]
public enum CompressionCodecFlags
{
    /// <summary>
    /// Indicates no supported operations or features.
    /// </summary>
    None					= 0,

    /// <summary>
    /// Indicates buffer-based compression support.
    /// </summary>
    Compression				= 1,

    /// <summary>
    /// Indicates buffer-based decompression support.
    /// </summary>
    Decompression			= 2,

    /// <summary>
    /// Indicates compression stream support.
    /// </summary>
    CompressionStreams		= 4,

    /// <summary>
    /// Indicates decompression stream support.
    /// </summary>
    DecompressionStreams	= 8,

    /// <summary>
    /// Indicates preset dictionary support.
    /// </summary>
    Dictionaries			= 16,

    /// <summary>
    /// Indicates that decompressed size metadata can be inspected.
    /// </summary>
    SizeInspection			= 32
}
