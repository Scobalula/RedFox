using System;
using System.Buffers.Binary;
using System.IO;

namespace RedFox.Graphics2D.Png;

internal static class PngFormatValidator
{
    public static void ValidateBitDepthColorType(byte bitDepth, byte colorType)
    {
        bool valid = colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            2 => bitDepth is 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            4 => bitDepth is 8 or 16,
            6 => bitDepth is 8 or 16,
            _ => false,
        };
        if (!valid)
        {
            throw new NotSupportedException($"Invalid PNG bit-depth/color-type combination: bitDepth={bitDepth}, colorType={colorType}.");
        }
    }

    public static bool IsCriticalChunk(string type)
    {
        if (type.Length != 4)
        {
            return false;
        }
        return (type[0] & 0x20) == 0;
    }

    public static int GetChannelCount(byte colorType)
    {
        return colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new NotSupportedException($"Unsupported PNG color type {colorType}.")
        };
    }

    public static int ComputePassDimension(int fullSize, int start, int step)
    {
        if (fullSize <= start)
        {
            return 0;
        }
        return (fullSize - start + step - 1) / step;
    }

    public static void ValidateSignature(Stream stream)
    {
        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(PngConstants.PngSignature))
        {
            throw new InvalidDataException("Invalid PNG signature.");
        }
    }

    public static PngHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException("IHDR chunk must be 13 bytes.");
        }
        int width = (int)BinaryPrimitives.ReadUInt32BigEndian(data[0..4]);
        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);
        byte bitDepth = data[8];
        byte colorType = data[9];
        byte compressionMethod = data[10];
        byte filterMethod = data[11];
        byte interlaceMethod = data[12];
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG dimensions must be positive.");
        }
        if (compressionMethod != 0)
        {
            throw new NotSupportedException($"Unsupported PNG compression method {compressionMethod}.");
        }
        if (filterMethod != 0)
        {
            throw new NotSupportedException($"Unsupported PNG filter method {filterMethod}.");
        }
        if (interlaceMethod is not (0 or 1))
        {
            throw new NotSupportedException($"Unsupported PNG interlace method {interlaceMethod}.");
        }
        ValidateBitDepthColorType(bitDepth, colorType);
        return new PngHeader(width, height, bitDepth, colorType, interlaceMethod);
    }

    public static byte[] ParsePalette(ReadOnlySpan<byte> data, in PngHeader header)
    {
        if (header.ColorType is not (2 or 3 or 6))
        {
            throw new InvalidDataException("PLTE is not allowed for this PNG color type.");
        }
        if (data.IsEmpty || data.Length % 3 != 0)
        {
            throw new InvalidDataException("PLTE chunk length must be a non-zero multiple of 3.");
        }
        if (data.Length / 3 > 256)
        {
            throw new InvalidDataException("PLTE cannot contain more than 256 palette entries.");
        }
        return data.ToArray();
    }

    public static byte[] ParseTransparency(ReadOnlySpan<byte> data, in PngHeader header, byte[]? palette)
    {
        return header.ColorType switch
        {
            0 => data.Length == 2
                ? data.ToArray()
                : throw new InvalidDataException("tRNS for grayscale PNG must be 2 bytes."),
            2 => data.Length == 6
                ? data.ToArray()
                : throw new InvalidDataException("tRNS for truecolor PNG must be 6 bytes."),
            3 => ParsePaletteTransparency(data, palette),
            _ => throw new InvalidDataException("tRNS is not allowed for this PNG color type."),
        };
    }

    public static byte[] ParsePaletteTransparency(ReadOnlySpan<byte> data, byte[]? palette)
    {
        if (palette is null)
        {
            throw new InvalidDataException("tRNS for indexed PNG requires a PLTE chunk.");
        }
        int paletteEntries = palette.Length / 3;
        if (data.Length > paletteEntries)
        {
            throw new InvalidDataException("tRNS alpha table cannot exceed PLTE entry count.");
        }
        return data.ToArray();
    }
}
