// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.IO.Compression;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides methods for compressing and decompressing name files using a Brotli.
/// </summary>
public static class NameFileCompressor
{
    /// <summary>
    /// Calculates the maximum compressed size for the provided input size.
    /// </summary>
    /// <param name="inputSize">The size of the input data.</param>
    /// <returns>The maximum compressed size.</returns>
    public static int GetMaxCompressedSize(int inputSize) => BrotliEncoder.GetMaxCompressedLength(inputSize);

    /// <summary>
    /// Compresses <paramref name="source"/> into <paramref name="destination"/>, returning the byte count written.
    /// </summary>
    /// <param name="source">The source data to compress.</param>
    /// <param name="destination">The destination span to write the compressed data to.</param>
    /// <returns>The number of bytes written to the destination span.</returns>
    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (!BrotliEncoder.TryCompress(source, destination, out var written, 4, 20))
            throw new InvalidOperationException("Failed to compress name file data.");

        return written;
    }

    /// <summary>
    /// Decompresses data from <paramref name="source"/> into <paramref name="destination"/>, returning the byte count written.
    /// </summary>
    /// <param name="source">The source data to decompress.</param>
    /// <param name="destination">The destination span to write the decompressed data to.</param>
    /// <returns>The number of bytes written to the destination span.</returns>
    public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (!BrotliDecoder.TryDecompress(source, destination, out var written))
            throw new InvalidOperationException("Failed to decompress name file data.");

        return written;
    }
}
