using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Formats.Hdr;

public sealed class RadianceHdrImageTranslator : ImageTranslator
{
    private const int MinimumRleWidth = 8;
    private const int MaximumRleWidth = 0x7FFF;
    private const float MaximumHalfValue = 65504.0f;

    public override string Name => "Radiance HDR";

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override IReadOnlyList<string> Extensions { get; } = [".hdr"];

    public override Image Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        (int width, int height) = ReadHeader(stream);
        Image image = new(width, height, ImageFormat.R16G16B16A16Float);
        Span<Half> pixels = MemoryMarshal.Cast<byte, Half>(image.PixelData);
        byte[] scanline = new byte[width * 4];

        for (int y = 0; y < height; y++)
        {
            ReadScanline(stream, scanline, width);

            for (int x = 0; x < width; x++)
            {
                int source = x * 4;
                int target = (y * width + x) * 4;
                float scale = scanline[source + 3] == 0 ? 0.0f : MathF.ScaleB(1.0f, scanline[source + 3] - 136);

                pixels[target]		= (Half)MathF.Min(scanline[source] * scale, MaximumHalfValue);
                pixels[target + 1]	= (Half)MathF.Min(scanline[source + 1] * scale, MaximumHalfValue);
                pixels[target + 2]	= (Half)MathF.Min(scanline[source + 2] * scale, MaximumHalfValue);
                pixels[target + 3]	= Half.One;
            }
        }

        return image;
    }

    public override void Write(Stream stream, Image image)
    {
        throw new NotSupportedException("Writing Radiance HDR files is not supported.");
    }

    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
    {
        return IsValid(filePath, extension) && header.StartsWith("#?"u8);
    }

    private static (int Width, int Height) ReadHeader(Stream stream)
    {
        if (!ReadLine(stream).StartsWith("#?", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid Radiance HDR file: missing #? signature.");

        string line;

        while ((line = ReadLine(stream)).Length > 0)
        {
            if (line.StartsWith("FORMAT=", StringComparison.Ordinal) && line != "FORMAT=32-bit_rle_rgbe")
                throw new NotSupportedException($"Unsupported Radiance HDR pixel format: {line[7..]}.");
        }

        // Only the standard top-to-bottom, left-to-right orientation is written by common tools.
        string[] resolution = ReadLine(stream).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (resolution.Length != 4 || resolution[0] != "-Y" || resolution[2] != "+X")
            throw new NotSupportedException($"Unsupported Radiance HDR orientation: {string.Join(' ', resolution)}.");

        return (int.Parse(resolution[3]), int.Parse(resolution[1]));
    }

    private static string ReadLine(Stream stream)
    {
        StringBuilder builder = new();
        int value;

        while ((value = stream.ReadByte()) != '\n')
        {
            if (value < 0)
                throw new EndOfStreamException("Unexpected end of Radiance HDR header.");

            builder.Append((char)value);
        }

        return builder.ToString();
    }

    private static void ReadScanline(Stream stream, byte[] scanline, int width)
    {
        stream.ReadExactly(scanline, 0, 4);

        bool isRle = width >= MinimumRleWidth && width <= MaximumRleWidth && scanline[0] == 2 && scanline[1] == 2 && (scanline[2] & 0x80) == 0;

        if (!isRle)
        {
            stream.ReadExactly(scanline, 4, scanline.Length - 4);
            return;
        }

        if (((scanline[2] << 8) | scanline[3]) != width)
            throw new InvalidDataException("Radiance HDR scanline width does not match the image width.");

        // RLE scanlines store each RGBE channel as its own run-length encoded plane.
        for (int channel = 0; channel < 4; channel++)
        {
            int x = 0;

            while (x < width)
            {
                int count = ReadByte(stream);

                if (count > 128)
                {
                    count -= 128;
                    byte value = ReadByte(stream);

                    if (x + count > width)
                        throw new InvalidDataException("Radiance HDR run exceeds the scanline width.");

                    for (int i = 0; i < count; i++)
                        scanline[(x++ * 4) + channel] = value;
                }
                else
                {
                    if (count == 0 || x + count > width)
                        throw new InvalidDataException("Radiance HDR literal run is invalid.");

                    for (int i = 0; i < count; i++)
                        scanline[(x++ * 4) + channel] = ReadByte(stream);
                }
            }
        }
    }

    private static byte ReadByte(Stream stream)
    {
        int value = stream.ReadByte();
        return value < 0 ? throw new EndOfStreamException("Unexpected end of Radiance HDR pixel data.") : (byte)value;
    }
}
