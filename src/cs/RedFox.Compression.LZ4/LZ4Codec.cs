// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression.LZ4
{
    /// <summary>
    /// A <see cref="CompressionCodec"/> that provides a wrapper around LZ4.
    /// </summary>
    public class LZ4Codec : CompressionCodec
    {
        /// <inheritdoc/>
        public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            var decompressedSize = LZ4Interop.Compress(source, destination, source.Length, destination.Length);

            if (decompressedSize <= 0)
                throw new CompressionException("Failed to compress data.", "compression", decompressedSize.ToString());

            return decompressedSize;
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("LZ4Codec currently does not support dictionaries.");

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            var decompressedSize = LZ4Interop.Decompress(source, destination, source.Length, destination.Length);

            if (decompressedSize <= 0)
                throw new CompressionException("Failed to decompress data.", "decompression", decompressedSize.ToString());

            return decompressedSize;
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("LZ4Codec currently does not support dictionaries.");

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize) => LZ4Interop.GetMaxCompressedSize(inputSize);

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => throw new NotSupportedException("LZ4 does not store decompressed size.");
    }
}
