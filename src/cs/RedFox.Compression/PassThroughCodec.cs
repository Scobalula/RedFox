using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Compression
{
    /// <summary>
    /// A basic <see cref="CompressionCodec"/> that simply passes/copies the data to the destination buffer.
    /// </summary>
    public class PassThroughCodec : CompressionCodec
    {
        /// <inheritdoc/>
        public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < source.Length)
                throw new CompressionException("Destination buffer is not large enough to store the source data.");

            source.CopyTo(destination);

            return source.Length;
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => Compress(source, destination);

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < source.Length)
                throw new CompressionException("Destination buffer is not large enough to store the source data.");

            source.CopyTo(destination);

            return source.Length;
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => Decompress(source, destination);

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => compressedBuffer.Length;

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize) => inputSize;
    }
}
