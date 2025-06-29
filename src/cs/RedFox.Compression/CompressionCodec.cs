// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression
{
    /// <summary>
    /// Abstract base class for compression and decompression codecs.
    /// Provides methods for compressing and decompressing data, with optional support for dictionaries and streaming.
    /// </summary>
    public abstract class CompressionCodec
    {
        /// <summary>
        /// Indicates the capabilities supported by this codec.
        /// </summary>
        public abstract CompressionCodecFlags Flags { get; }

        /// <summary>
        /// Compresses data from the specified source buffer into the destination buffer.
        /// </summary>
        /// <param name="source">The input data to be compressed.</param>
        /// <param name="destination">The buffer to store the compressed data.</param>
        /// <returns>The size of the compressed data written to the destination buffer.</returns>
        public abstract int Compress(ReadOnlySpan<byte> source, Span<byte> destination);

        /// <summary>
        /// Compresses data from the specified source buffer into the destination buffer using a provided dictionary.
        /// </summary>
        /// <param name="source">The input data to be compressed.</param>
        /// <param name="destination">The buffer to store the compressed data.</param>
        /// <param name="dictionary">The dictionary to use for compression, if supported.</param>
        /// <returns>The size of the compressed data written to the destination buffer.</returns>
        /// <exception cref="NotSupportedException">Thrown if dictionary-based compression is not supported by this codec.</exception>
        public abstract int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary);

        /// <summary>
        /// Decompresses data from the specified source buffer into the destination buffer.
        /// </summary>
        /// <param name="source">The compressed data to be decompressed.</param>
        /// <param name="destination">The buffer to store the decompressed data.</param>
        /// <returns>The size of the decompressed data written to the destination buffer.</returns>
        public abstract int Decompress(ReadOnlySpan<byte> source, Span<byte> destination);

        /// <summary>
        /// Decompresses data from the specified source buffer into the destination buffer using a provided dictionary.
        /// </summary>
        /// <param name="source">The compressed data to be decompressed.</param>
        /// <param name="destination">The buffer to store the decompressed data.</param>
        /// <param name="dictionary">The dictionary to use for decompression, if supported.</param>
        /// <returns>The size of the decompressed data written to the destination buffer.</returns>
        /// <exception cref="NotSupportedException">Thrown if dictionary-based decompression is not supported by this codec.</exception>
        public abstract int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary);

        /// <summary>
        /// Compresses data from the specified source byte array into the destination byte array, with specified offsets and lengths.
        /// </summary>
        /// <param name="source">The input data to be compressed.</param>
        /// <param name="sourceOffset">The starting offset in the source array.</param>
        /// <param name="sourceSize">The number of bytes to compress from the source array.</param>
        /// <param name="destination">The buffer to store the compressed data.</param>
        /// <param name="destinationOffset">The starting offset in the destination array.</param>
        /// <returns>The size of the compressed data written to the destination array.</returns>
        public virtual int Compress(byte[] source, int sourceOffset, int sourceSize, byte[] destination, int destinationOffset)
        {
            return Compress(source.AsSpan(sourceOffset, sourceSize), destination.AsSpan(destinationOffset));
        }

        /// <summary>
        /// Compresses data from the specified source byte array into the destination byte array using a provided dictionary.
        /// </summary>
        /// <param name="source">The input data to be compressed.</param>
        /// <param name="sourceOffset">The starting offset in the source array.</param>
        /// <param name="sourceSize">The number of bytes to compress from the source array.</param>
        /// <param name="destination">The buffer to store the compressed data.</param>
        /// <param name="destinationOffset">The starting offset in the destination array.</param>
        /// <param name="dictionary">The dictionary to use for compression, if supported.</param>
        /// <returns>The size of the compressed data written to the destination array.</returns>
        public virtual int Compress(byte[] source, int sourceOffset, int sourceSize, byte[] destination, int destinationOffset, byte[] dictionary)
        {
            return Compress(source.AsSpan(sourceOffset, sourceSize), destination.AsSpan(destinationOffset), dictionary.AsSpan());
        }

        /// <summary>
        /// Decompresses data from the specified source byte array into the destination byte array, with specified offsets and lengths.
        /// </summary>
        /// <param name="source">The compressed data to be decompressed.</param>
        /// <param name="sourceOffset">The starting offset in the source array.</param>
        /// <param name="sourceSize">The number of bytes to decompress from the source array.</param>
        /// <param name="destination">The buffer to store the decompressed data.</param>
        /// <param name="destinationOffset">The starting offset in the destination array.</param>
        /// <returns>The size of the decompressed data written to the destination array.</returns>
        public virtual int Decompress(byte[] source, int sourceOffset, int sourceSize, byte[] destination, int destinationOffset)
        {
            return Decompress(source.AsSpan(sourceOffset, sourceSize), destination.AsSpan(destinationOffset));
        }

        /// <summary>
        /// Decompresses data from the specified source byte array into the destination byte array using a provided dictionary.
        /// </summary>
        /// <param name="source">The compressed data to be decompressed.</param>
        /// <param name="sourceOffset">The starting offset in the source array.</param>
        /// <param name="sourceSize">The number of bytes to decompress from the source array.</param>
        /// <param name="destination">The buffer to store the decompressed data.</param>
        /// <param name="destinationOffset">The starting offset in the destination array.</param>
        /// <param name="dictionary">The dictionary to use for decompression, if supported.</param>
        /// <returns>The size of the decompressed data written to the destination array.</returns>
        public virtual int Decompress(byte[] source, int sourceOffset, int sourceSize, byte[] destination, int destinationOffset, byte[] dictionary)
        {
            return Decompress(source.AsSpan(sourceOffset, sourceSize), destination.AsSpan(destinationOffset), dictionary.AsSpan());
        }

        /// <summary>
        /// Gets the maximum size that a compressed buffer might be, given the size of the input data.
        /// This can be used to allocate a destination buffer for compression.
        /// </summary>
        /// <param name="inputSize">The size of the input data.</param>
        /// <returns>The maximum possible size of the compressed data.</returns>
        public abstract int GetMaxCompressedSize(int inputSize);

        /// <summary>
        /// Gets the decompressed size of the compressed data buffer, if known.
        /// This may be needed if the decompressed size is not stored in the compressed format.
        /// </summary>
        /// <param name="compressedBuffer">The buffer containing the compressed data.</param>
        /// <returns>The size of the decompressed data.</returns>
        public abstract int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer);
    }

}
