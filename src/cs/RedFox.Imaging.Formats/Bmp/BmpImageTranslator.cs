using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Formats.Bmp;

/// <summary>
/// An <see cref="ImageTranslator"/> for BMP (Windows Bitmap) files.
/// Supports reading uncompressed 1/4/8/24/32-bit BMPs with optional BI_BITFIELDS.
/// Writes uncompressed 32-bit BGRA or 24-bit BGR BMPs.
/// </summary>
public sealed class BmpImageTranslator : ImageTranslator
{
    private const int BI_RGB = 0;
    private const int BI_BITFIELDS = 3;
    private const int MaxDibHeaderSize = 124;

    /// <inheritdoc/>
    public override string Name => "BMP";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions { get; } = [".bmp", ".dib"];

    /// <inheritdoc/>
    public override bool CanReadInfo => true;

    /// <inheritdoc/>
    public override Image Read(Stream stream)
    {
        long startPosition = stream.Position;
        byte[] dibHeader = ReadDibHeader(stream, out long dataOffset, out int width, out int height);
        int headerSize = dibHeader.Length + 4;
        bool topDown = BinaryPrimitives.ReadInt32LittleEndian(dibHeader.AsSpan(4)) < 0;

        int bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(dibHeader.AsSpan(10));
        int compression = (int)BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(12));
        uint colorsUsed = BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(28));

        if (compression is not (BI_RGB or BI_BITFIELDS))
            throw new NotSupportedException($"Unsupported BMP compression: {compression}. Only BI_RGB and BI_BITFIELDS are supported.");
        if (bitsPerPixel is not (1 or 4 or 8 or 16 or 24 or 32))
            throw new NotSupportedException($"Unsupported BMP bits per pixel: {bitsPerPixel}.");

        uint rMask = 0x00FF0000, gMask = 0x0000FF00, bMask = 0x000000FF, aMask = 0;

        if (compression == BI_BITFIELDS)
        {
            if (bitsPerPixel is not (16 or 32))
                throw new NotSupportedException($"BI_BITFIELDS requires 16 or 32 bpp, got {bitsPerPixel}.");

            if (headerSize >= 52)
            {
                rMask = BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(36));
                gMask = BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(40));
                bMask = BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(44));
            }
            else
            {
                Span<byte> masks = stackalloc byte[12];
                stream.ReadExactly(masks);
                rMask = BinaryPrimitives.ReadUInt32LittleEndian(masks);
                gMask = BinaryPrimitives.ReadUInt32LittleEndian(masks[4..]);
                bMask = BinaryPrimitives.ReadUInt32LittleEndian(masks[8..]);
            }

            if (headerSize >= 56)
                aMask = BinaryPrimitives.ReadUInt32LittleEndian(dibHeader.AsSpan(48));
        }

        byte[]? palette = null;
        if (bitsPerPixel <= 8)
        {
            int maxPaletteCount = 1 << bitsPerPixel;
            if (colorsUsed > maxPaletteCount)
                throw new InvalidDataException($"BMP declares {colorsUsed} palette entries for a {bitsPerPixel}-bit image.");

            int paletteCount = colorsUsed > 0 ? (int)colorsUsed : maxPaletteCount;
            palette = new byte[maxPaletteCount * 4]; // BGRA entries; unused indices stay black
            stream.ReadExactly(palette.AsSpan(0, paletteCount * 4));
        }

        stream.Position = startPosition + dataOffset;

        int rowStride = ((bitsPerPixel * width + 31) / 32) * 4; // rows are DWORD-aligned
        long rawDataSize = (long)rowStride * height;
        if (rawDataSize > Array.MaxLength || (stream.CanSeek && stream.Length - stream.Position < rawDataSize))
            throw new InvalidDataException("BMP pixel data is truncated.");

        var rawData = new byte[rawDataSize];
        stream.ReadExactly(rawData);

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
    public override ImageInfo ReadInfo(Stream stream)
    {
        ReadDibHeader(stream, out _, out int width, out int height);
        return new ImageInfo(width, height, ImageFormat.R8G8B8A8Unorm);
    }

    /// <inheritdoc/>
    public override void Write(Stream stream, Image image)
    {
        WriteEncodedImage(stream, image);
    }

    /// <inheritdoc/>
    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
    {
        if (!IsValid(filePath, extension) || header.Length < 2)
            return false;

        return header[0] == (byte)'B' && header[1] == (byte)'M';
    }

    private static void WriteEncodedImage(Stream stream, Image image)
    {
        ref readonly var slice = ref image.GetSlice(0, 0, 0);
        int width = slice.Width;
        int height = slice.Height;
        var format = image.Format;
        var sourcePixels = slice.PixelSpan;

        bool hasAlpha = format is (ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb or ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb) && HasNonOpaqueAlpha(sourcePixels);
        int bitsPerPixel = hasAlpha ? 32 : 24;
        int rowStride = ((bitsPerPixel * width + 31) / 32) * 4;
        int imageSize = rowStride * height;
        int fileSize = 14 + 40 + imageSize;

        Span<byte> fileHeader = stackalloc byte[14];
        fileHeader.Clear();
        fileHeader[0] = (byte)'B';
        fileHeader[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader[2..], (uint)fileSize);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader[10..], 14 + 40); // data offset
        stream.Write(fileHeader);

        Span<byte> dibHeader = stackalloc byte[40];
        dibHeader.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(dibHeader, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[8..], -height); // top-down
        BinaryPrimitives.WriteUInt16LittleEndian(dibHeader[12..], 1); // planes
        BinaryPrimitives.WriteUInt16LittleEndian(dibHeader[14..], (ushort)bitsPerPixel);
        BinaryPrimitives.WriteUInt32LittleEndian(dibHeader[16..], 0); // BI_RGB
        BinaryPrimitives.WriteUInt32LittleEndian(dibHeader[20..], (uint)imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[24..], 2835); // ~72 DPI
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[28..], 2835);
        stream.Write(dibHeader);

        if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb)
        {
            if (bitsPerPixel == 32)
            {
                stream.Write(sourcePixels);
            }
            else
            {
                WriteRowsBgr(stream, sourcePixels, width, height, rowStride);
            }
            return;
        }

        if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
        {
            WriteRowsSwizzled(stream, sourcePixels, width, height, rowStride, bitsPerPixel);
            return;
        }

        if (format is ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
        {
            WriteRowsBgr(stream, sourcePixels, width, height, rowStride);
            return;
        }

        if (!PixelCodecs.TryGetCodec(format, out var codec) || codec is null)
            throw new NotSupportedException($"BMP writing is not supported for format {format}.");

        WriteBmpRowsDecoded(stream, slice, width, height, rowStride, bitsPerPixel, codec);
    }

    private static void DecodeBgra32(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> destination, int destinationOffset, int width)
    {
        for (int x = 0; x < width; x++)
        {
            int sourcePixelOffset = sourceOffset + x * 4;
            int destinationPixelOffset = destinationOffset + x * 4;
            destination[destinationPixelOffset + 0] = source[sourcePixelOffset + 2];
            destination[destinationPixelOffset + 1] = source[sourcePixelOffset + 1];
            destination[destinationPixelOffset + 2] = source[sourcePixelOffset + 0];
            destination[destinationPixelOffset + 3] = source[sourcePixelOffset + 3];
        }
    }

    private static void DecodeBgr24(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> destination, int destinationOffset, int width)
    {
        for (int x = 0; x < width; x++)
        {
            int sourcePixelOffset = sourceOffset + x * 3;
            int destinationPixelOffset = destinationOffset + x * 4;
            destination[destinationPixelOffset + 0] = source[sourcePixelOffset + 2];
            destination[destinationPixelOffset + 1] = source[sourcePixelOffset + 1];
            destination[destinationPixelOffset + 2] = source[sourcePixelOffset + 0];
            destination[destinationPixelOffset + 3] = 255;
        }
    }

    private static void Decode16Rgb555(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> destination, int destinationOffset, int width)
    {
        for (int x = 0; x < width; x++)
        {
            ushort pixel = BinaryPrimitives.ReadUInt16LittleEndian(source[(sourceOffset + x * 2)..]);
            int destinationPixelOffset = destinationOffset + x * 4;
            int r = (pixel >> 10) & 0x1F;
            int g = (pixel >> 5) & 0x1F;
            int b = pixel & 0x1F;
            destination[destinationPixelOffset + 0] = (byte)((r << 3) | (r >> 2));
            destination[destinationPixelOffset + 1] = (byte)((g << 3) | (g >> 2));
            destination[destinationPixelOffset + 2] = (byte)((b << 3) | (b >> 2));
            destination[destinationPixelOffset + 3] = 255;
        }
    }

    private static void DecodeBitfields32(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> destination, int destinationOffset, int width, uint rMask, uint gMask, uint bMask, uint aMask)
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
            uint pixel = BinaryPrimitives.ReadUInt32LittleEndian(source[(sourceOffset + x * 4)..]);
            int destinationPixelOffset = destinationOffset + x * 4;
            destination[destinationPixelOffset + 0] = ScaleChannel((pixel & rMask) >> rShift, rBits);
            destination[destinationPixelOffset + 1] = ScaleChannel((pixel & gMask) >> gShift, gBits);
            destination[destinationPixelOffset + 2] = ScaleChannel((pixel & bMask) >> bShift, bBits);
            destination[destinationPixelOffset + 3] = aMask != 0 ? ScaleChannel((pixel & aMask) >> aShift, aBits) : (byte)255;
        }
    }

    private static void DecodeBitfields16(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> destination, int destinationOffset, int width, uint rMask, uint gMask, uint bMask, uint aMask)
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
            uint pixel = BinaryPrimitives.ReadUInt16LittleEndian(source[(sourceOffset + x * 2)..]);
            int destinationPixelOffset = destinationOffset + x * 4;
            destination[destinationPixelOffset + 0] = ScaleChannel((pixel & rMask) >> rShift, rBits);
            destination[destinationPixelOffset + 1] = ScaleChannel((pixel & gMask) >> gShift, gBits);
            destination[destinationPixelOffset + 2] = ScaleChannel((pixel & bMask) >> bShift, bBits);
            destination[destinationPixelOffset + 3] = aMask != 0 ? ScaleChannel((pixel & aMask) >> aShift, aBits) : (byte)255;
        }
    }

    private static byte[] ReadDibHeader(Stream stream, out long dataOffset, out int width, out int height)
    {
        Span<byte> fileHeader = stackalloc byte[18];
        stream.ReadExactly(fileHeader);

        if (BinaryPrimitives.ReadUInt16LittleEndian(fileHeader) != 0x4D42)
            throw new InvalidDataException("Not a valid BMP file.");

        dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[10..]);
        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[14..]);

        if (headerSize is < 40 or > MaxDibHeaderSize)
            throw new NotSupportedException($"Unsupported BMP DIB header size: {headerSize}. Only BITMAPINFOHEADER (40) through BITMAPV5HEADER ({MaxDibHeaderSize}) are supported.");

        var dibHeader = new byte[headerSize - 4];
        stream.ReadExactly(dibHeader);

        width = BinaryPrimitives.ReadInt32LittleEndian(dibHeader);
        int rawHeight = BinaryPrimitives.ReadInt32LittleEndian(dibHeader.AsSpan(4));
        height = rawHeight == int.MinValue ? 0 : Math.Abs(rawHeight);

        new ImageInfo(width, height, ImageFormat.R8G8B8A8Unorm).Validate();
        return dibHeader;
    }

    private static void DecodeIndexed(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> destination, int destinationOffset, int width, ReadOnlySpan<byte> palette, int bitsPerPixel)
    {
        for (int x = 0; x < width; x++)
        {
            int index;

            switch (bitsPerPixel)
            {
                case 8:
                    index = source[sourceOffset + x];
                    break;
                case 4:
                    {
                        byte packedPixels = source[sourceOffset + x / 2];
                        index = (x % 2 == 0) ? (packedPixels >> 4) : (packedPixels & 0x0F);
                        break;
                    }
                case 1:
                    {
                        byte packedPixels = source[sourceOffset + x / 8];
                        index = (packedPixels >> (7 - (x % 8))) & 1;
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }

            int paletteOffset = index * 4;
            int destinationPixelOffset = destinationOffset + x * 4;
            destination[destinationPixelOffset + 0] = palette[paletteOffset + 2];
            destination[destinationPixelOffset + 1] = palette[paletteOffset + 1];
            destination[destinationPixelOffset + 2] = palette[paletteOffset + 0];
            destination[destinationPixelOffset + 3] = 255;
        }
    }

    private static byte ScaleChannel(uint value, int bits)
    {
        if (bits == 0)
            return 0;
        if (bits >= 8)
            return (byte)(value >> (bits - 8));
        uint maxValue = (1u << bits) - 1;
        return (byte)((value * 255 + maxValue / 2) / maxValue);
    }

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
    private static void WriteRowsSwizzled(Stream stream, ReadOnlySpan<byte> rgba, int width, int height, int rowStride, int bitsPerPixel)
    {
        var rowBuffer = new byte[rowStride];

        if (bitsPerPixel == 32)
        {
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int s = rowStart + x * 4;
                    int d = x * 4;
                    rowBuffer[d + 0] = rgba[s + 2];
                    rowBuffer[d + 1] = rgba[s + 1];
                    rowBuffer[d + 2] = rgba[s + 0];
                    rowBuffer[d + 3] = rgba[s + 3];
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
                    rowBuffer[d + 0] = rgba[s + 2];
                    rowBuffer[d + 1] = rgba[s + 1];
                    rowBuffer[d + 2] = rgba[s + 0];
                }

                stream.Write(rowBuffer);
            }
        }
    }

    private static void WriteBmpRowsDecoded(Stream stream, in ImageSlice slice, int width, int height, int rowStride, int bitsPerPixel, IPixelCodec codec)
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

                if (bitsPerPixel == 32)
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

    private static bool HasNonOpaqueAlpha(ReadOnlySpan<byte> rgbaOrBgra)
    {
        for (int i = 3; i < rgbaOrBgra.Length; i += 4)
        {
            if (rgbaOrBgra[i] < 255)
                return true;
        }
        return false;
    }
}
