using System;
using System.Collections.Generic;
using System.IO;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Formats.Jpeg;

/// <summary>
/// JPEG reader/writer implementation for the RedFox image translator pipeline.
/// Supports baseline (SOF0) and progressive (SOF2) JPEG decoding, and baseline JPEG encoding.
/// </summary>
public sealed class JpegImageTranslator : ImageTranslator
{
    /// <summary>
    /// Gets or sets the encoder options used when writing JPEG files.
    /// </summary>
    public JpegEncoderOptions EncoderOptions { get; set; } = new();

    /// <inheritdoc/>
    public override string Name => "JPEG";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override bool CanReadInfo => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions { get; } = [".jpg", ".jpeg", ".jpe", ".jfif"];

    /// <inheritdoc/>
    public override Image Read(Stream stream)
    {
        var decoder = new JpegDecoder(stream);
        var decoded = decoder.Decode();
        return ConvertToImage(decoded);
    }

    /// <inheritdoc/>
    public override ImageInfo ReadInfo(Stream stream)
    {
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
            throw new InvalidDataException("Invalid JPEG: missing SOI marker.");

        Span<byte> segment = stackalloc byte[7];
        Span<byte> skipped = stackalloc byte[256];

        while (true)
        {
            int marker = ReadMarker(stream);

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
                continue;
            if (marker is 0xDA or 0xD9)
                throw new InvalidDataException("JPEG contains no frame header before its scan data.");

            stream.ReadExactly(segment[..2]);
            int length = (segment[0] << 8) | segment[1];

            if (length < 2)
                throw new InvalidDataException("Invalid JPEG segment length.");

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                stream.ReadExactly(segment[2..]);
                ImageInfo info = new((segment[5] << 8) | segment[6], (segment[3] << 8) | segment[4], ImageFormat.R8G8B8A8Unorm);
                info.Validate();
                return info;
            }

            int bytesRemaining = length - 2;
            while (bytesRemaining > 0)
            {
                int count = Math.Min(bytesRemaining, skipped.Length);
                stream.ReadExactly(skipped[..count]);
                bytesRemaining -= count;
            }
        }
    }

    /// <inheritdoc/>
    public override void Write(Stream stream, Image image)
    {
        WriteEncodedImage(stream, image, EncoderOptions);
    }

    /// <inheritdoc/>
    public override void Write(Stream stream, Image image, ImageTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        WriteEncodedImage(stream, image, ResolveEncoderOptions(options));
    }

    /// <inheritdoc/>
    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
    {
        if (!IsValid(filePath, extension) || header.Length < 3)
            return false;

        return IsJpegHeader(header);
    }

    private static int ReadMarker(Stream stream)
    {
        int value;

        do
        {
            value = stream.ReadByte();
        }
        while (value is >= 0 and not 0xFF);

        while (value == 0xFF)
            value = stream.ReadByte();

        if (value < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream.");

        return value;
    }

    private static bool IsJpegHeader(ReadOnlySpan<byte> header)
    {
        return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    }

    private JpegEncoderOptions ResolveEncoderOptions(ImageTranslatorOptions options)
    {
        return new JpegEncoderOptions
        {
            Quality = options.Quality ?? EncoderOptions.Quality,
            Subsampling = ResolveSubsampling(EncoderOptions.Subsampling, options.Compression),
            OptimizeHuffmanTables = ResolveOptimizeHuffmanTables(EncoderOptions.OptimizeHuffmanTables, options.Compression),
        };
    }

    private static JpegChromaSubsampling ResolveSubsampling(JpegChromaSubsampling defaultSubsampling, ImageCompressionPreference compressionPreference)
    {
        return compressionPreference switch
        {
            ImageCompressionPreference.None => JpegChromaSubsampling.Yuv444,
            ImageCompressionPreference.Fast => JpegChromaSubsampling.Yuv420,
            ImageCompressionPreference.Balanced => JpegChromaSubsampling.Yuv422,
            ImageCompressionPreference.SmallestSize => JpegChromaSubsampling.Yuv420,
            _ => defaultSubsampling,
        };
    }

    private static bool ResolveOptimizeHuffmanTables(bool defaultOptimizeHuffmanTables, ImageCompressionPreference compressionPreference)
    {
        return compressionPreference switch
        {
            ImageCompressionPreference.Fast => false,
            ImageCompressionPreference.SmallestSize => true,
            _ => defaultOptimizeHuffmanTables,
        };
    }

    private static void WriteEncodedImage(Stream stream, Image image, JpegEncoderOptions encoderOptions)
    {
        JpegEncoder encoder = new(stream, encoderOptions);
        encoder.Encode(image);
    }

    /// <summary>
    /// Converts the decoded JPEG component planes to an <see cref="Image"/> in R8G8B8A8Unorm format.
    /// </summary>
    private static Image ConvertToImage(DecodedJpegImage decoded)
    {
        int width = decoded.Width;
        int height = decoded.Height;
        var pixels = new byte[width * height * 4];

        switch (decoded.ColorSpace)
        {
            case JpegColorSpace.Grayscale:
                ConvertGrayscale(decoded, pixels, width, height);
                break;

            case JpegColorSpace.YCbCr:
                ConvertYCbCr(decoded, pixels, width, height);
                break;

            case JpegColorSpace.Cmyk:
                ConvertCmyk(decoded, pixels, width, height, ycck: false);
                break;

            case JpegColorSpace.Ycck:
                ConvertCmyk(decoded, pixels, width, height, ycck: true);
                break;

            default:
                ConvertYCbCr(decoded, pixels, width, height);
                break;
        }

        return new Image(width, height, ImageFormat.R8G8B8A8Unorm, pixels);
    }

    /// <summary>
    /// Converts grayscale component data to RGBA.
    /// </summary>
    private static void ConvertGrayscale(DecodedJpegImage decoded, byte[] pixels, int width, int height)
    {
        var grayPlane = decoded.ComponentData[0];
        int grayWidth = decoded.ComponentWidths[0];

        // Extract only the valid image region from the (possibly padded) component plane
        var trimmed = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            grayPlane.AsSpan(y * grayWidth, width).CopyTo(trimmed.AsSpan(y * width));
        }

        JpegColorConverter.GrayscaleToRgba(trimmed, pixels, width * height);
    }

    /// <summary>
    /// Converts YCbCr component data to RGBA with chroma upsampling.
    /// </summary>
    private static void ConvertYCbCr(DecodedJpegImage decoded, byte[] pixels, int width, int height)
    {
        var yPlane = ExtractPlane(decoded.ComponentData[0], decoded.ComponentWidths[0], width, height, decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[0], decoded.ComponentVSamples[0]);
        var cbPlane = ExtractPlane(decoded.ComponentData[1], decoded.ComponentWidths[1], width, height, decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[1], decoded.ComponentVSamples[1]);
        var crPlane = ExtractPlane(decoded.ComponentData[2], decoded.ComponentWidths[2], width, height, decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[2], decoded.ComponentVSamples[2]);

        int cbWidth = (width * decoded.ComponentHSamples[1] + decoded.MaxHSample - 1) / decoded.MaxHSample;

        var chroma = new ChromaSampling(cbWidth, decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[1], decoded.ComponentVSamples[1]);

        JpegColorConverter.YCbCrToRgba(yPlane, cbPlane, crPlane, pixels, width, height, chroma);
    }

    /// <summary>
    /// Extracts the valid region from a (possibly padded) component sample plane.
    /// </summary>
    private static byte[] ExtractPlane(byte[] source, int sourceWidth, int imageWidth, int imageHeight, int maxH, int maxV, int compH, int compV)
    {
        int planeWidth = (imageWidth * compH + maxH - 1) / maxH;
        int planeHeight = (imageHeight * compV + maxV - 1) / maxV;
        int copyLength = Math.Min(planeWidth, sourceWidth);
        var result = new byte[planeWidth * planeHeight];

        for (int y = 0; y < planeHeight && (y + 1L) * sourceWidth <= source.Length; y++)
            source.AsSpan(y * sourceWidth, copyLength).CopyTo(result.AsSpan(y * planeWidth));

        return result;
    }

    private static void ConvertCmyk(DecodedJpegImage decoded, byte[] pixels, int width, int height, bool ycck)
    {
        byte[][] planes = new byte[4][];
        int[] planeWidths = new int[4];
        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = ExtractPlane(decoded.ComponentData[i], decoded.ComponentWidths[i], width, height, decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[i], decoded.ComponentVSamples[i]);
            planeWidths[i] = (width * decoded.ComponentHSamples[i] + decoded.MaxHSample - 1) / decoded.MaxHSample;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                int c = SampleCmykPlane(planes[0], planeWidths[0], decoded, 0, x, y);
                int m = SampleCmykPlane(planes[1], planeWidths[1], decoded, 1, x, y);
                int yellow = SampleCmykPlane(planes[2], planeWidths[2], decoded, 2, x, y);
                int black = SampleCmykPlane(planes[3], planeWidths[3], decoded, 3, x, y);

                if (ycck)
                {
                    int red = Math.Clamp((int)(c + 1.402 * (yellow - 128)), 0, 255);
                    int green = Math.Clamp((int)(c - 0.344136 * (m - 128) - 0.714136 * (yellow - 128)), 0, 255);
                    int blue = Math.Clamp((int)(c + 1.772 * (m - 128)), 0, 255);
                    c = 255 - red;
                    m = 255 - green;
                    yellow = 255 - blue;
                }

                bool adobeCmyk = decoded.IsAdobeCmyk || ycck;
                int redOut = adobeCmyk ? c * black / 255 : (255 - c) * (255 - black) / 255;
                int greenOut = adobeCmyk ? m * black / 255 : (255 - m) * (255 - black) / 255;
                int blueOut = adobeCmyk ? yellow * black / 255 : (255 - yellow) * (255 - black) / 255;
                pixels[offset] = (byte)redOut;
                pixels[offset + 1] = (byte)greenOut;
                pixels[offset + 2] = (byte)blueOut;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static byte SampleCmykPlane(byte[] plane, int planeWidth, DecodedJpegImage decoded, int component, int x, int y)
    {
        int sampleX = x * decoded.ComponentHSamples[component] / decoded.MaxHSample;
        int sampleY = y * decoded.ComponentVSamples[component] / decoded.MaxVSample;
        return plane[sampleY * planeWidth + sampleX];
    }
}
