// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.Compression
{
    /// <summary>
    /// Flags to dictate features a <see cref="CompressionCodec"/> supports.
    /// </summary>
    public enum CompressionCodecFlags
    {
        /// <summary>
        /// Indicates no extra features supported.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates the codec supports dictionaries.
        /// </summary>
        SupportsDictionaries = 1 << 0,

        /// <summary>
        /// Indicates the codec can obtain the size of the source from the input.
        /// </summary>
        SupportsKnownSize = 1 << 1,
    }
}
