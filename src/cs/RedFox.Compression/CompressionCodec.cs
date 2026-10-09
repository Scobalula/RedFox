// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression;

/// <summary>
/// Provides buffer, allocation, and stream operations for a compression format.
/// </summary>
public abstract class CompressionCodec : IDisposable
{
    /// <summary>
    /// Gets the operations and features supported by this codec.
    /// </summary>
    public abstract CompressionCodecFlags Flags { get; }

    /// <summary>
    /// Compresses data into a caller-provided buffer.
    /// </summary>
    /// <param name="source">The data to compress.</param>
    /// <param name="destination">The buffer that receives compressed data.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    public virtual int Compress(ReadOnlySpan<byte> source, Span<byte> destination) => throw new NotSupportedException("This codec does not support compression.");

    /// <summary>
    /// Decompresses data into a caller-provided buffer.
    /// </summary>
    /// <param name="source">The compressed data.</param>
    /// <param name="destination">The buffer that receives decompressed data.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    public abstract int Decompress(ReadOnlySpan<byte> source, Span<byte> destination);

    /// <summary>
    /// Gets an upper bound for the compressed size of the specified input.
    /// </summary>
    /// <param name="inputSize">The number of input bytes.</param>
    /// <returns>The required destination buffer size.</returns>
    public virtual int GetMaxCompressedSize(int inputSize) => throw new NotSupportedException("This codec does not support compression bounds.");

    /// <summary>
    /// Compresses data and allocates a buffer for the result.
    /// </summary>
    /// <param name="source">The data to compress.</param>
    /// <returns>The compressed data.</returns>
    public byte[] Compress(ReadOnlySpan<byte> source)
    {
        int capacity = GetMaxCompressedSize(source.Length);
        if (capacity < 0 || capacity > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(source), "The compressed size bound exceeds the .NET array limit.");

        byte[] destination = new byte[capacity];
        int written = Compress(source, destination);
        Array.Resize(ref destination, written);
        return destination;
    }

    /// <summary>
    /// Decompresses data, obtaining its size from the format metadata.
    /// </summary>
    /// <param name="source">The compressed data.</param>
    /// <returns>The decompressed data.</returns>
    public byte[] Decompress(ReadOnlySpan<byte> source) => Decompress(source, null, int.MaxValue);

    /// <summary>
    /// Decompresses data using an optional exact output size.
    /// </summary>
    /// <param name="source">The compressed data.</param>
    /// <param name="decompressedSize">The exact output size, or <see langword="null"/> to inspect format metadata.</param>
    /// <returns>The decompressed data.</returns>
    public byte[] Decompress(ReadOnlySpan<byte> source, int? decompressedSize) => Decompress(source, decompressedSize, int.MaxValue);

    /// <summary>
    /// Decompresses data while enforcing an allocation limit.
    /// </summary>
    /// <param name="source">The compressed data.</param>
    /// <param name="decompressedSize">The exact output size, or <see langword="null"/> to inspect format metadata.</param>
    /// <param name="maxDecompressedSize">The maximum number of bytes to allocate.</param>
    /// <returns>The decompressed data.</returns>
    public byte[] Decompress(ReadOnlySpan<byte> source, int? decompressedSize, int maxDecompressedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDecompressedSize);
        if (decompressedSize < 0)
            throw new ArgumentOutOfRangeException(nameof(decompressedSize));

        long size;
        if (decompressedSize.HasValue)
            size = decompressedSize.Value;
        else if (!TryGetDecompressedSize(source, out size))
            throw new NotSupportedException("Supply the exact decompressed size or use a decompression stream.");

        if (size < 0 || size > maxDecompressedSize || size > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(maxDecompressedSize), "The decompressed size exceeds the allocation limit.");

        byte[] destination = new byte[(int)size];
        int written = Decompress(source, destination);
        if (written != size)
            throw new CompressionException($"Expected {size} decompressed bytes, received {written}.");

        return destination;
    }

    /// <summary>
    /// Attempts to read the decompressed size from format metadata.
    /// </summary>
    /// <param name="source">The compressed data.</param>
    /// <param name="size">Receives the decompressed size when available.</param>
    /// <returns><see langword="true"/> when the size is available.</returns>
    public virtual bool TryGetDecompressedSize(ReadOnlySpan<byte> source, out long size)
    {
        size = 0;
        return false;
    }

    /// <summary>
    /// Creates a stream that writes compressed data to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The destination stream.</param>
    /// <returns>A compression stream that owns <paramref name="destination"/>.</returns>
    public Stream CreateCompressionStream(Stream destination) => CreateCompressionStream(destination, false, null);

    /// <summary>
    /// Creates a stream that writes compressed data to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The destination stream.</param>
    /// <param name="leaveOpen">Whether to leave the destination open when the returned stream is disposed.</param>
    /// <returns>A compression stream.</returns>
    public Stream CreateCompressionStream(Stream destination, bool leaveOpen) => CreateCompressionStream(destination, leaveOpen, null);

    /// <summary>
    /// Creates a stream that writes compressed data to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The destination stream.</param>
    /// <param name="leaveOpen">Whether to leave the destination open when the returned stream is disposed.</param>
    /// <param name="uncompressedSize">The expected input size, when known.</param>
    /// <returns>A compression stream.</returns>
    public virtual Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize) => throw new NotSupportedException("This codec does not support compression streams.");

    /// <summary>
    /// Creates a stream that reads decompressed data from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <returns>A decompression stream that owns <paramref name="source"/>.</returns>
    public Stream CreateDecompressionStream(Stream source) => CreateDecompressionStream(source, false, null);

    /// <summary>
    /// Creates a stream that reads decompressed data from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="leaveOpen">Whether to leave the source open when the returned stream is disposed.</param>
    /// <returns>A decompression stream.</returns>
    public Stream CreateDecompressionStream(Stream source, bool leaveOpen) => CreateDecompressionStream(source, leaveOpen, null);

    /// <summary>
    /// Creates a stream that reads decompressed data from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="leaveOpen">Whether to leave the source open when the returned stream is disposed.</param>
    /// <param name="compressedSize">The maximum compressed input length, when known.</param>
    /// <returns>A decompression stream.</returns>
    public virtual Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize) => throw new NotSupportedException("This codec does not support decompression streams.");

    /// <summary>
    /// Releases resources held by the codec.
    /// </summary>
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
