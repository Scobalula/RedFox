using System;
using System.IO;

namespace RedFox.Imaging.Formats.Dds;

/// <summary>
/// Loads DDS (DirectDraw Surface) files into <see cref="Image"/> instances.
/// Supports legacy headers and DX10-extended headers.
/// </summary>
public static class DdsLoader
{
    /// <summary>
    /// Loads a DDS file from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the DDS file.</param>
    /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
    public static Image Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        byte[] data = File.ReadAllBytes(filePath);
        return Load(data);
    }

    /// <summary>
    /// Loads a DDS file from a readable stream.
    /// </summary>
    /// <param name="stream">The stream containing the DDS data.</param>
    /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
    public static Image Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) { throw new IOException("The supplied stream is not readable."); }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return Load(memoryStream.GetBuffer().AsSpan(0, checked((int)memoryStream.Length)));
    }

    /// <summary>
    /// Loads a DDS file from a byte array.
    /// </summary>
    /// <param name="data">The raw DDS bytes.</param>
    /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
    public static Image Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Load(data.AsSpan());
    }

    /// <summary>
    /// Loads a DDS file from a read-only span of bytes.
    /// </summary>
    /// <param name="data">The raw DDS bytes.</param>
    /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
    public static Image Load(ReadOnlySpan<byte> data)
    {
        DdsMetadata metadata = DdsMetadataReader.Read(data);
        ImageInfo info = metadata.Info;
        info.Validate();

        if (metadata.IsLegacyRgb24)
            return LoadLegacyRgb24(data, metadata, info);

        long requiredBytes = info.CalculateTotalByteCount();
        if (data.Length - metadata.DataOffset < requiredBytes)
            throw new InvalidDataException($"DDS pixel data is truncated. Expected {requiredBytes} bytes but found {Math.Max(0, data.Length - metadata.DataOffset)}.");

        return new Image(info, data.Slice(metadata.DataOffset, (int)requiredBytes).ToArray());
    }

    private static Image LoadLegacyRgb24(ReadOnlySpan<byte> data, DdsMetadata metadata, ImageInfo info)
    {
        long requiredSourceBytes = CalculateLegacyRgb24ByteCount(info);
        long requiredDestinationBytes = info.CalculateTotalByteCount();
        if (data.Length - metadata.DataOffset < requiredSourceBytes)
            throw new InvalidDataException($"DDS pixel data is truncated. Expected {requiredSourceBytes} bytes but found {Math.Max(0, data.Length - metadata.DataOffset)}.");

        var pixels = new byte[checked((int)requiredDestinationBytes)];
        int sourceOffset = metadata.DataOffset;
        int destinationOffset = 0;
        for (int arrayIndex = 0; arrayIndex < info.ArraySize; arrayIndex++)
        {
            for (int mipLevel = 0; mipLevel < info.MipLevels; mipLevel++)
            {
                int width = Math.Max(1, info.Width >> mipLevel);
                int height = Math.Max(1, info.Height >> mipLevel);
                int depth = Math.Max(1, info.Depth >> mipLevel);
                int pixelCount = checked(width * height * depth);
                for (int pixel = 0; pixel < pixelCount; pixel++)
                {
                    pixels[destinationOffset++] = data[sourceOffset++];
                    pixels[destinationOffset++] = data[sourceOffset++];
                    pixels[destinationOffset++] = data[sourceOffset++];
                    pixels[destinationOffset++] = 255;
                }
            }
        }

        return new Image(info, pixels);
    }

    private static long CalculateLegacyRgb24ByteCount(ImageInfo info)
    {
        long byteCount = 0;
        for (int arrayIndex = 0; arrayIndex < info.ArraySize; arrayIndex++)
        {
            for (int mipLevel = 0; mipLevel < info.MipLevels; mipLevel++)
            {
                long width = Math.Max(1, info.Width >> mipLevel);
                long height = Math.Max(1, info.Height >> mipLevel);
                long depth = Math.Max(1, info.Depth >> mipLevel);
                byteCount = checked(byteCount + width * height * depth * 3);
            }
        }

        return byteCount;
    }

    /// <summary>
    /// Reads the image layout from the DDS headers at the current position of a stream without reading pixel data.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the DDS data.</param>
    /// <returns>The validated image layout.</returns>
    public static ImageInfo LoadInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> header = stackalloc byte[DdsMetadataReader.MaxHeaderSize];
        int headerSize = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        ImageInfo info = DdsMetadataReader.Read(header[..headerSize]).Info;
        info.Validate();
        return info;
    }
}
