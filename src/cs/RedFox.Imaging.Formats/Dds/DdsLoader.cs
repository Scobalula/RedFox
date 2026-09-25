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
        return Load(memoryStream.ToArray());
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

        long requiredBytes = info.CalculateTotalByteCount();
        if (data.Length - metadata.DataOffset < requiredBytes)
            throw new InvalidDataException($"DDS pixel data is truncated. Expected {requiredBytes} bytes but found {Math.Max(0, data.Length - metadata.DataOffset)}.");

        return new Image(info, data.Slice(metadata.DataOffset, (int)requiredBytes).ToArray());
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
