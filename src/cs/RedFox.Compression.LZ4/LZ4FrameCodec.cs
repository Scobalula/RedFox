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
    /// A <see cref="CompressionCodec"/> that provides a wrapper around LZ4 Frame Format.
    /// </summary>
    public class LZ4FrameCodec : CompressionCodec
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
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("LZ4FrameCodec currently does not support dictionaries.");

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            nuint decompressContext = 0;

            var versionNumber = LZ4Interop.FrameGetVersion();
            var error = LZ4Interop.FrameCreateDecompressionContext(ref decompressContext, versionNumber);

            var dst = destination;
            var src = source;
            var dstSize = (nuint)destination.Length;
            var srcSize = (nuint)src.Length;

            Span<byte> opt = stackalloc byte[256];

            var decompressError = LZ4Interop.FrameDecompress(decompressContext, dst, ref dstSize, src, ref srcSize, opt);

            if (LZ4Interop.FrameIsError(decompressError) != 0)
                throw new CompressionException($"Failed to decompress data: {LZ4Interop.FrameGetErrorName(decompressError)}");

            return (int)dstSize;
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("LZ4FrameCodec currently does not support dictionaries.");

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize) => LZ4Interop.GetMaxCompressedSize(inputSize);

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => throw new NotSupportedException("LZ4 does not store decompressed size.");
    }
}
