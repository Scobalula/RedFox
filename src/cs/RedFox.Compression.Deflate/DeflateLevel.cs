// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression.Deflate
{
    /// <summary>
    /// Describes the compression level to use for deflating files.
    /// </summary>
    public enum DeflateLevel : int
    {
        /// <summary>
        /// No compression.
        /// </summary>
        NoCompression = 0,

        /// <summary>
        /// Favour speed.
        /// </summary>
        BestSpeed = 1,

        /// <summary>
        /// Favour compression.
        /// </summary>
        BestCompression = 9,

        /// <summary>
        /// Favour compression even more.
        /// </summary>
        UberCompression = 10,

        /// <summary>
        /// Default compression level
        /// </summary>
        DefaultLevel = 6,

        /// <summary>
        /// Default compression level.
        /// </summary>
        DefaultCompression = -1
    };
}
