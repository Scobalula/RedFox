namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Constants used for reading and writing TIFF (Tagged Image File Format) files,
    /// including IFD tag identifiers, compression codes, photometric interpretation
    /// values, and IFD entry data types.
    /// </summary>
    internal static class TiffConstants
    {
        // ──────────────────────────────────────────────
        // IFD Tag Identifiers (Baseline TIFF 6.0)
        // ──────────────────────────────────────────────

        /// <summary>The number of columns in the image (pixels per row).</summary>
        internal const ushort TagImageWidth = 256;

        /// <summary>The number of rows of pixels in the image.</summary>
        internal const ushort TagImageLength = 257;

        /// <summary>Number of bits per component sample.</summary>
        internal const ushort TagBitsPerSample = 258;

        /// <summary>Compression scheme used on the image data.</summary>
        internal const ushort TagCompression = 259;

        /// <summary>The color space of the image data.</summary>
        internal const ushort TagPhotometric = 262;

        /// <summary>For each strip, the byte offset of that strip's data.</summary>
        internal const ushort TagStripOffsets = 273;

        /// <summary>The number of components per pixel.</summary>
        internal const ushort TagSamplesPerPixel = 277;

        /// <summary>The number of rows per strip of data.</summary>
        internal const ushort TagRowsPerStrip = 278;

        /// <summary>For each strip, the number of compressed bytes in that strip.</summary>
        internal const ushort TagStripByteCounts = 279;

        /// <summary>Horizontal resolution of the image in pixels per resolution unit.</summary>
        internal const ushort TagXResolution = 282;

        /// <summary>Vertical resolution of the image in pixels per resolution unit.</summary>
        internal const ushort TagYResolution = 283;

        /// <summary>The unit of measurement for <see cref="TagXResolution"/> and <see cref="TagYResolution"/>.</summary>
        internal const ushort TagResolutionUnit = 296;

        /// <summary>Describes the meaning of extra components (e.g., alpha channel).</summary>
        internal const ushort TagExtraSamples = 338;

        // ──────────────────────────────────────────────
        // Compression Codes
        // ──────────────────────────────────────────────

        /// <summary>No compression (uncompressed data).</summary>
        internal const ushort CompressionNone = 1;

        /// <summary>LZW (Lempel-Ziv-Welch) compression.</summary>
        internal const ushort CompressionLZW = 5;

        /// <summary>PackBits run-length compression.</summary>
        internal const ushort CompressionPackBits = 32773;

        // ──────────────────────────────────────────────
        // Photometric Interpretation
        // ──────────────────────────────────────────────

        /// <summary>Grayscale where 0 represents white.</summary>
        internal const ushort PhotometricMinIsWhite = 0;

        /// <summary>Grayscale where 0 represents black.</summary>
        internal const ushort PhotometricMinIsBlack = 1;

        /// <summary>RGB color model.</summary>
        internal const ushort PhotometricRGB = 2;

        // ──────────────────────────────────────────────
        // IFD Entry Data Types
        // ──────────────────────────────────────────────

        /// <summary>8-bit unsigned integer.</summary>
        internal const ushort TypeByte = 1;

        /// <summary>8-bit ASCII character.</summary>
        internal const ushort TypeAscii = 2;

        /// <summary>16-bit unsigned integer.</summary>
        internal const ushort TypeShort = 3;

        /// <summary>32-bit unsigned integer.</summary>
        internal const ushort TypeLong = 4;

        /// <summary>Two 32-bit unsigned integers representing a fraction (numerator/denominator).</summary>
        internal const ushort TypeRational = 5;

        // ──────────────────────────────────────────────
        // LZW Constants
        // ──────────────────────────────────────────────

        /// <summary>LZW clear code — resets the string table.</summary>
        internal const int LzwClearCode = 256;

        /// <summary>LZW end-of-information code — signals end of compressed data.</summary>
        internal const int LzwEoiCode = 257;

        /// <summary>First assignable code in the LZW string table (entries 0–255 are single bytes, 256–257 reserved).</summary>
        internal const int LzwFirstCode = 258;

        /// <summary>Maximum number of entries in the LZW string table (12-bit codes).</summary>
        internal const int LzwMaxTableSize = 4096;

        /// <summary>Initial code size in bits for TIFF LZW (starts at 9 after the 8-bit alphabet + 2 special codes).</summary>
        internal const int LzwInitialCodeSize = 9;

        /// <summary>Maximum code size in bits for TIFF LZW.</summary>
        internal const int LzwMaxCodeSize = 12;
    }
}
