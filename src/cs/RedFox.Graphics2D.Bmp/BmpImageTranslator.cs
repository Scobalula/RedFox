using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Bmp
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for BMP (Windows Bitmap) files.
    /// Supports reading uncompressed 1/4/8/24/32-bit BMPs with optional BI_BITFIELDS.
    /// Writes uncompressed 32-bit BGRA or 24-bit BGR BMPs.
    /// </summary>
    public sealed class BmpImageTranslator : ImageTranslator
    {
        // Compression types
        private const int BI_RGB = 0;
        private const int BI_BITFIELDS = 3;

        /// <inheritdoc/>
        public override string Name => "BMP";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".bmp", ".dib"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            long startPosition = stream.Position;

            // ── BITMAPFILEHEADER (14 bytes) ──
            Span<byte> fileHeader = stackalloc byte[14];
            stream.ReadExactly(fileHeader);

            ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(fileHeader);
            if (magic != 0x4D42) // 'BM'
                throw new InvalidDataException("Not a valid BMP file.");

            int dataOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[10..]);

            // ── DIB header (at least BITMAPINFOHEADER = 40 bytes) ──
            Span<byte> headerSizeBuf = stackalloc byte[4];
            stream.ReadExactly(headerSizeBuf);
            int headerSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(headerSizeBuf);

            if (headerSize < 40)
                throw new NotSupportedException($"Unsupported BMP DIB header size: {headerSize}. Only BITMAPINFOHEADER (40+) is supported.");

            var dibHeader = new byte[headerSize - 4];
            stream.ReadExactly(dibHeader);

            int width = BinaryPrimitives.ReadInt32LittleEndian(dibHeader);
            int rawHeight = BinaryPrimitives.ReadInt32LittleEndian(dibHeader.AsSpan(4));
            bool topDown = rawHeight < 0;
            int height = Math.Abs(rawHeight);

            if (width <= 0 || height <= 0)
                throw new InvalidDataException($"Invalid BMP dimensions: {width}x{height}.");

            int bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(dibHeader.AsSpan(10));
            int compression = (int)BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(12));
            int colorsUsed = (int)BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(28));

            if (compression is not (BI_RGB or BI_BITFIELDS))
                throw new NotSupportedException($"Unsupported BMP compression: {compression}. Only BI_RGB and BI_BITFIELDS are supported.");

            // ── Channel masks for BI_BITFIELDS ──
            uint rMask = 0x00FF0000, gMask = 0x0000FF00, bMask = 0x000000FF, aMask = 0xFF000000;

            if (compression == BI_BITFIELDS)
            {
                if (bitsPerPixel is not (16 or 32))
                    throw new NotSupportedException($"BI_BITFIELDS requires 16 or 32 bpp, got {bitsPerPixel}.");

                // Masks follow the DIB header (or are embedded in V4/V5 headers)
                Span<byte> masks = stackalloc byte[12];
                stream.ReadExactly(masks);
                rMask = BinaryPrimitives.ReadUInt32LittleEndian(masks);
                gMask = BinaryPrimitives.ReadUInt32LittleEndian(masks[4..]);
                bMask = BinaryPrimitives.ReadUInt32LittleEndian(masks[8..]);

                // V4/V5 headers (headerSize >= 56) include an alpha mask
                if (headerSize >= 56)
                    aMask = BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(48));
                else
                    aMask = ~(rMask | gMask | bMask);
            }

            // ── Color table (for indexed images) ──
            byte[]? palette = null;
            if (bitsPerPixel <= 8)
            {
                int paletteCount = colorsUsed > 0 ? colorsUsed : (1 << bitsPerPixel);
                palette = new byte[paletteCount * 4]; // BGRA entries
                stream.ReadExactly(palette);
            }

            // ── Seek to pixel data ──
            stream.Position = startPosition + dataOffset;

            // ── Read pixel data ──
            int rowStride = ((bitsPerPixel * width + 31) / 32) * 4; // rows are DWORD-aligned
            var rawData = new byte[rowStride * height];
            stream.ReadExactly(rawData);

            // ── Decode to RGBA8 ──
            var output = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                // BMP default: bottom-up. If topDown, rows are already in order.
                int srcRow = topDown ? y : (height - 1 - y);
                int srcOffset = srcRow * rowStride;
                int dstOffset = y * width * 4;

                switch (bitsPerPixel)
                {
                    case 32 when compression == BI_RGB:
                        DecodeBgra32(rawData, srcOffset, output, dstOffset, width);
                        break;
                    case 32 when compression == BI_BITFIELDS:
                        DecodeBitfields32(rawData, srcOffset, output, dstOffset, width, rMask, gMask, bMask, aMask);
                        break;
                    case 24:
                        DecodeBgr24(rawData, srcOffset, output, dstOffset, width);
                        break;
                    case 16 when compression == BI_BITFIELDS:
                        DecodeBitfields16(rawData, srcOffset, output, dstOffset, width, rMask, gMask, bMask, aMask);
                        break;
                    case 16:
                        // Default 16-bit: X1R5G5B5
                        Decode16Rgb555(rawData, srcOffset, output, dstOffset, width);
                        break;
                    case 8:
                        DecodeIndexed(rawData, srcOffset, output, dstOffset, width, palette!, 8);
                        break;
                    case 4:
                        DecodeIndexed(rawData, srcOffset, output, dstOffset, width, palette!, 4);
                        break;
                    case 1:
                        DecodeIndexed(rawData, srcOffset, output, dstOffset, width, palette!, 1);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported BMP bits per pixel: {bitsPerPixel}.");
                }
            }

            return new Image(width, height, ImageFormat.R8G8B8A8Unorm, output);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            ref readonly var slice = ref image.GetSlice(0, 0, 0);
            int width = slice.Width;
            int height = slice.Height;
            var format = image.Format;
            var src = slice.PixelSpan;

            // Determine if we need alpha
            bool hasAlpha = false;
            int bpp;

            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb
                or ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb)
            {
                hasAlpha = HasNonOpaqueAlpha(src, stride: 4, alphaOffset: format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb ? 3 : 3);
            }

            bpp = hasAlpha ? 32 : 24;
            int rowStride = ((bpp * width + 31) / 32) * 4;
            int imageSize = rowStride * height;
            int fileSize = 14 + 40 + imageSize;

            // ── BITMAPFILEHEADER (14 bytes) ──
            Span<byte> fileHeader = stackalloc byte[14];
            fileHeader.Clear();
            fileHeader[0] = (byte)'B';
            fileHeader[1] = (byte)'M';
            BinaryPrimitives.WriteUInt32LittleEndian(fileHeader[2..], (uint)fileSize);
            BinaryPrimitives.WriteUInt32LittleEndian(fileHeader[10..], 14 + 40); // data offset
            stream.Write(fileHeader);

            // ── BITMAPINFOHEADER (40 bytes) ──
            Span<byte> dibHeader = stackalloc byte[40];
            dibHeader.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(dibHeader, 40);
            BinaryPrimitives.WriteInt32LittleEndian(dibHeader[4..], width);
            BinaryPrimitives.WriteInt32LittleEndian(dibHeader[8..], -height); // top-down
            BinaryPrimitives.WriteUInt16LittleEndian(dibHeader[12..], 1); // planes
            BinaryPrimitives.WriteUInt16LittleEndian(dibHeader[14..], (ushort)bpp);
            BinaryPrimitives.WriteUInt32LittleEndian(dibHeader[16..], 0); // BI_RGB
            BinaryPrimitives.WriteUInt32LittleEndian(dibHeader[20..], (uint)imageSize);
            BinaryPrimitives.WriteInt32LittleEndian(dibHeader[24..], 2835); // ~72 DPI
            BinaryPrimitives.WriteInt32LittleEndian(dibHeader[28..], 2835);
            stream.Write(dibHeader);

            // ── Pixel data ──
            // Fast path: source is already BGRA
            if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb)
            {
                if (bpp == 32)
                {
                    // Zero-copy bulk write — row stride equals width*4 at 32bpp
                    stream.Write(src);
                }
                else
                {
                    WriteRowsBgr(stream, src, width, height, rowStride);
                }
                return;
            }

            // Fast path: source is RGBA — inline swizzle
            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
            {
                WriteRowsSwizzled(stream, src, width, height, rowStride, bpp);
                return;
            }

            // B8G8R8X8 (opaque BGRA, no alpha — always 24bpp since no alpha)
            if (format is ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
            {
                WriteRowsBgr(stream, src, width, height, rowStride);
                return;
            }

            // General path: per-pixel decode via codec
            if (!PixelCodecRegistry.TryGetCodec(format, out var codec) || codec is null)
                throw new NotSupportedException($"BMP writing is not supported for format {format}.");

            WriteBmpRowsDecoded(stream, slice, width, height, rowStride, bpp, codec);
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            if (!IsValid(filePath, extension) || header.Length < 2)
                return false;

            return header[0] == (byte)'B' && header[1] == (byte)'M';
        }

        // ──────────────────────────────────────────────
        // Read helpers
        // ──────────────────────────────────────────────

        private static void DecodeBgra32(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff, int width)
        {
            for (int x = 0; x < width; x++)
            {
                int s = srcOff + x * 4;
                int d = dstOff + x * 4;
                dst[d + 0] = src[s + 2]; // R
                dst[d + 1] = src[s + 1]; // G
                dst[d + 2] = src[s + 0]; // B
                dst[d + 3] = src[s + 3]; // A
            }
        }

        private static void DecodeBgr24(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff, int width)
        {
            for (int x = 0; x < width; x++)
            {
                int s = srcOff + x * 3;
                int d = dstOff + x * 4;
                dst[d + 0] = src[s + 2]; // R
                dst[d + 1] = src[s + 1]; // G
                dst[d + 2] = src[s + 0]; // B
                dst[d + 3] = 255;
            }
        }

        private static void Decode16Rgb555(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff, int width)
        {
            for (int x = 0; x < width; x++)
            {
                ushort pixel = BinaryPrimitives.ReadUInt16LittleEndian(src[(srcOff + x * 2)..]);
                int d = dstOff + x * 4;
                int r = (pixel >> 10) & 0x1F;
                int g = (pixel >> 5) & 0x1F;
                int b = pixel & 0x1F;
                dst[d + 0] = (byte)((r << 3) | (r >> 2));
                dst[d + 1] = (byte)((g << 3) | (g >> 2));
                dst[d + 2] = (byte)((b << 3) | (b >> 2));
                dst[d + 3] = 255;
            }
        }

        private static void DecodeBitfields32(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff, int width,
            uint rMask, uint gMask, uint bMask, uint aMask)
        {
            int rShift = BitOperations.TrailingZeroCount(rMask);
            int gShift = BitOperations.TrailingZeroCount(gMask);
            int bShift = BitOperations.TrailingZeroCount(bMask);
            int aShift = aMask != 0 ? BitOperations.TrailingZeroCount(aMask) : 0;
            int rBits = BitOperations.PopCount(rMask);
            int gBits = BitOperations.PopCount(gMask);
            int bBits = BitOperations.PopCount(bMask);
            int aBits = aMask != 0 ? BitOperations.PopCount(aMask) : 0;

            for (int x = 0; x < width; x++)
            {
                uint pixel = BinaryPrimitives.ReadUInt32LittleEndian(src[(srcOff + x * 4)..]);
                int d = dstOff + x * 4;
                dst[d + 0] = ScaleChannel((pixel & rMask) >> rShift, rBits);
                dst[d + 1] = ScaleChannel((pixel & gMask) >> gShift, gBits);
                dst[d + 2] = ScaleChannel((pixel & bMask) >> bShift, bBits);
                dst[d + 3] = aMask != 0 ? ScaleChannel((pixel & aMask) >> aShift, aBits) : (byte)255;
            }
        }

        private static void DecodeBitfields16(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff, int width,
            uint rMask, uint gMask, uint bMask, uint aMask)
        {
            int rShift = BitOperations.TrailingZeroCount(rMask);
            int gShift = BitOperations.TrailingZeroCount(gMask);
            int bShift = BitOperations.TrailingZeroCount(bMask);
            int aShift = aMask != 0 ? BitOperations.TrailingZeroCount(aMask) : 0;
            int rBits = BitOperations.PopCount(rMask);
            int gBits = BitOperations.PopCount(gMask);
            int bBits = BitOperations.PopCount(bMask);
            int aBits = aMask != 0 ? BitOperations.PopCount(aMask) : 0;

            for (int x = 0; x < width; x++)
            {
                uint pixel = BinaryPrimitives.ReadUInt16LittleEndian(src[(srcOff + x * 2)..]);
                int d = dstOff + x * 4;
                dst[d + 0] = ScaleChannel((pixel & rMask) >> rShift, rBits);
                dst[d + 1] = ScaleChannel((pixel & gMask) >> gShift, gBits);
                dst[d + 2] = ScaleChannel((pixel & bMask) >> bShift, bBits);
                dst[d + 3] = aMask != 0 ? ScaleChannel((pixel & aMask) >> aShift, aBits) : (byte)255;
            }
        }

        private static void DecodeIndexed(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff,
            int width, ReadOnlySpan<byte> palette, int bitsPerPixel)
        {
            for (int x = 0; x < width; x++)
            {
                int index;

                switch (bitsPerPixel)
                {
                    case 8:
                        index = src[srcOff + x];
                        break;
                    case 4:
                        {
                            byte b = src[srcOff + x / 2];
                            index = (x % 2 == 0) ? (b >> 4) : (b & 0x0F);
                            break;
                        }
                    case 1:
                        {
                            byte b = src[srcOff + x / 8];
                            index = (b >> (7 - (x % 8))) & 1;
                            break;
                        }
                    default:
                        throw new NotSupportedException();
                }

                int paletteOffset = index * 4;
                int d = dstOff + x * 4;
                dst[d + 0] = palette[paletteOffset + 2]; // R (palette is BGRA)
                dst[d + 1] = palette[paletteOffset + 1]; // G
                dst[d + 2] = palette[paletteOffset + 0]; // B
                dst[d + 3] = palette[paletteOffset + 3]; // A
            }
        }

        private static byte ScaleChannel(uint value, int bits)
        {
            if (bits >= 8)
                return (byte)(value >> (bits - 8));
            // Replicate upper bits into lower bits for accuracy
            return (byte)((value << (8 - bits)) | (value >> (2 * bits - 8)));
        }

        // ──────────────────────────────────────────────
        // Write helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Writes BGR rows from BGRA source, stripping the alpha byte.
        /// </summary>
        private static void WriteRowsBgr(Stream stream, ReadOnlySpan<byte> bgra, int width, int height, int rowStride)
        {
            var rowBuffer = new byte[rowStride];

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int s = rowStart + x * 4;
                    int d = x * 3;
                    rowBuffer[d + 0] = bgra[s + 0];
                    rowBuffer[d + 1] = bgra[s + 1];
                    rowBuffer[d + 2] = bgra[s + 2];
                }

                stream.Write(rowBuffer);
            }
        }

        /// <summary>
        /// Writes from RGBA source with R↔B swizzle to produce BGRA/BGR output.
        /// </summary>
        private static void WriteRowsSwizzled(Stream stream, ReadOnlySpan<byte> rgba, int width, int height, int rowStride, int bpp)
        {
            var rowBuffer = new byte[rowStride];

            if (bpp == 32)
            {
                for (int y = 0; y < height; y++)
                {
                    int rowStart = y * width * 4;

                    for (int x = 0; x < width; x++)
                    {
                        int s = rowStart + x * 4;
                        int d = x * 4;
                        rowBuffer[d + 0] = rgba[s + 2]; // B
                        rowBuffer[d + 1] = rgba[s + 1]; // G
                        rowBuffer[d + 2] = rgba[s + 0]; // R
                        rowBuffer[d + 3] = rgba[s + 3]; // A
                    }

                    stream.Write(rowBuffer);
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int rowStart = y * width * 4;

                    for (int x = 0; x < width; x++)
                    {
                        int s = rowStart + x * 4;
                        int d = x * 3;
                        rowBuffer[d + 0] = rgba[s + 2]; // B
                        rowBuffer[d + 1] = rgba[s + 1]; // G
                        rowBuffer[d + 2] = rgba[s + 0]; // R
                    }

                    stream.Write(rowBuffer);
                }
            }
        }

        private static void WriteBmpRowsDecoded(Stream stream, in ImageSlice slice, int width, int height, int rowStride, int bpp, IPixelCodec codec)
        {
            var rowBuffer = new byte[rowStride];
            var pixels = new Vector4[width];

            for (int y = 0; y < height; y++)
            {
                codec.DecodeRows(slice.PixelSpan, pixels, y, 1, width, height);

                for (int x = 0; x < width; x++)
                {
                    Vector4 pixel = pixels[x];
                    byte r = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
                    byte g = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
                    byte b = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
                    byte a = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);

                    if (bpp == 32)
                    {
                        int d = x * 4;
                        rowBuffer[d + 0] = b;
                        rowBuffer[d + 1] = g;
                        rowBuffer[d + 2] = r;
                        rowBuffer[d + 3] = a;
                    }
                    else
                    {
                        int d = x * 3;
                        rowBuffer[d + 0] = b;
                        rowBuffer[d + 1] = g;
                        rowBuffer[d + 2] = r;
                    }
                }

                stream.Write(rowBuffer);
            }
        }

        private static bool HasNonOpaqueAlpha(ReadOnlySpan<byte> data, int stride, int alphaOffset)
        {
            for (int i = alphaOffset; i < data.Length; i += stride)
            {
                if (data[i] < 255)
                    return true;
            }
            return false;
        }
    }
}
