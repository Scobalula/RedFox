namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Constants used for reading and writing TIFF (Tagged Image File Format) files,
    /// including IFD tag identifiers, compression codes, photometric interpretation
    /// values, and IFD entry data types.
    /// </summary>
    public static class TiffConstants
    {
        // ──────────────────────────────────────────────
        // IFD Tag Identifiers (Baseline TIFF 6.0)
        // ──────────────────────────────────────────────

        /// <summary>
        /// The number of columns in the image (pixels per row).
        /// </summary>
        public const ushort TagImageWidth = 256;

        /// <summary>
        /// The number of rows of pixels in the image.
        /// </summary>
        public const ushort TagImageLength = 257;

        /// <summary>
        /// Number of bits per component sample.
        /// </summary>
        public const ushort TagBitsPerSample = 258;

        /// <summary>
        /// Compression scheme used on the image data.
        /// </summary>
        public const ushort TagCompression = 259;

        /// <summary>
        /// Bit fill order for compressed data.
        /// </summary>
        public const ushort TagFillOrder = 266;

        /// <summary>
        /// The color space of the image data.
        /// </summary>
        public const ushort TagPhotometric = 262;

        /// <summary>
        /// For each strip, the byte offset of that strip's data.
        /// </summary>
        public const ushort TagStripOffsets = 273;

        /// <summary>
        /// The number of components per pixel.
        /// </summary>
        public const ushort TagSamplesPerPixel = 277;

        /// <summary>
        /// The number of rows per strip of data.
        /// </summary>
        public const ushort TagRowsPerStrip = 278;

        /// <summary>
        /// For each strip, the number of compressed bytes in that strip.
        /// </summary>
        public const ushort TagStripByteCounts = 279;

        /// <summary>
        /// Horizontal resolution of the image in pixels per resolution unit.
        /// </summary>
        public const ushort TagXResolution = 282;

        /// <summary>
        /// Vertical resolution of the image in pixels per resolution unit.
        /// </summary>
        public const ushort TagYResolution = 283;

        /// <summary>
        /// The unit of measurement for <see cref="TagXResolution"/> and <see cref="TagYResolution"/>.
        /// </summary>
        public const ushort TagResolutionUnit = 296;

        /// <summary>
        /// Predictor used by certain compression schemes such as LZW and Deflate.
        /// </summary>
        public const ushort TagPredictor = 317;

        /// <summary>
        /// Tile width for tiled TIFF images.
        /// </summary>
        public const ushort TagTileWidth = 322;

        /// <summary>
        /// Tile length for tiled TIFF images.
        /// </summary>
        public const ushort TagTileLength = 323;

        /// <summary>
        /// Byte offsets for each tile.
        /// </summary>
        public const ushort TagTileOffsets = 324;

        /// <summary>
        /// Byte counts for each tile.
        /// </summary>
        public const ushort TagTileByteCounts = 325;

        /// <summary>
        /// Describes the meaning of extra components (e.g., alpha channel).
        /// </summary>
        public const ushort TagExtraSamples = 338;

        /// <summary>
        /// Planar configuration for multi-sample TIFF images.
        /// </summary>
        public const ushort TagPlanarConfiguration = 284;

        // ──────────────────────────────────────────────
        // Compression Codes
        // ──────────────────────────────────────────────

        /// <summary>
        /// No compression (uncompressed data).
        /// </summary>
        public const ushort CompressionNone = 1;

        /// <summary>
        /// LZW (Lempel-Ziv-Welch) compression.
        /// </summary>
        public const ushort CompressionLZW = 5;

        /// <summary>
        /// Deflate/ZIP compression.
        /// </summary>
        public const ushort CompressionDeflate = 8;

        /// <summary>
        /// Legacy Adobe Deflate compression.
        /// </summary>
        public const ushort CompressionAdobeDeflate = 32946;

        /// <summary>
        /// PackBits run-length compression.
        /// </summary>
        public const ushort CompressionPackBits = 32773;

        // ──────────────────────────────────────────────
        // Photometric Interpretation
        // ──────────────────────────────────────────────

        /// <summary>
        /// Grayscale where 0 represents white.
        /// </summary>
        public const ushort PhotometricMinIsWhite = 0;

        /// <summary>
        /// Grayscale where 0 represents black.
        /// </summary>
        public const ushort PhotometricMinIsBlack = 1;

        /// <summary>
        /// RGB color model.
        /// </summary>
        public const ushort PhotometricRGB = 2;

        // ──────────────────────────────────────────────
        // IFD Entry Data Types
        // ──────────────────────────────────────────────

        /// <summary>
        /// 8-bit unsigned integer.
        /// </summary>
        public const ushort TypeByte = 1;

        /// <summary>
        /// 8-bit ASCII character.
        /// </summary>
        public const ushort TypeAscii = 2;

        /// <summary>
        /// 16-bit unsigned integer.
        /// </summary>
        public const ushort TypeShort = 3;

        /// <summary>
        /// 32-bit unsigned integer.
        /// </summary>
        public const ushort TypeLong = 4;

        /// <summary>
        /// Two 32-bit unsigned integers representing a fraction (numerator/denominator).
        /// </summary>
        public const ushort TypeRational = 5;

        // ──────────────────────────────────────────────
        // LZW Constants
        // ──────────────────────────────────────────────

        /// <summary>
        /// LZW clear code — resets the string table.
        /// </summary>
        public const int LzwClearCode = 256;

        /// <summary>
        /// LZW end-of-information code — signals end of compressed data.
        /// </summary>
        public const int LzwEoiCode = 257;

        /// <summary>
        /// First assignable code in the LZW string table (entries 0–255 are single bytes, 256–257 reserved).
        /// </summary>
        public const int LzwFirstCode = 258;

        /// <summary>
        /// Maximum number of entries in the LZW string table (12-bit codes).
        /// </summary>
        public const int LzwMaxTableSize = 4096;

        /// <summary>
        /// Initial code size in bits for TIFF LZW (starts at 9 after the 8-bit alphabet + 2 special codes).
        /// </summary>
        public const int LzwInitialCodeSize = 9;

        /// <summary>
        /// Maximum code size in bits for TIFF LZW.
        /// </summary>
        public const int LzwMaxCodeSize = 12;
    }
}
