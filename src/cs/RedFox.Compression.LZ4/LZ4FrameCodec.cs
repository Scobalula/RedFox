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
            nuint bound = LZ4Interop.FrameGetMaxCompressedSize((nuint)source.Length, ReadOnlySpan<byte>.Empty);
            if (LZ4Interop.FrameIsError(bound) != 0)
                throw new CompressionException("Failed to get the LZ4 frame size bound.", "compression", LZ4Interop.FrameGetErrorName(bound));

            nuint compressedSize = LZ4Interop.FrameCompress(destination, (nuint)destination.Length, source, (nuint)source.Length, ReadOnlySpan<byte>.Empty);
            if (LZ4Interop.FrameIsError(compressedSize) != 0)
                throw new CompressionException("Failed to compress data.", "compression", LZ4Interop.FrameGetErrorName(compressedSize));

            return checked((int)compressedSize);
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("LZ4FrameCodec currently does not support dictionaries.");

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (source.IsEmpty)
                throw new InvalidDataException("The LZ4 frame is empty.");

            var versionNumber = LZ4Interop.FrameGetVersion();
            nuint createResult = LZ4Interop.FrameCreateDecompressionContext(out nint decompressionContext, versionNumber);
            if (LZ4Interop.FrameIsError(createResult) != 0)
                throw new CompressionException("Failed to create the LZ4 decompression context.", "decompression", LZ4Interop.FrameGetErrorName(createResult));
            if (decompressionContext == 0)
                throw new CompressionException("Failed to create the LZ4 decompression context.", "decompression", "The native context is null.");

            try
            {
                int bytesRead = 0;
                int bytesWritten = 0;

                while (bytesRead < source.Length)
                {
                    nuint destinationSize = (nuint)(destination.Length - bytesWritten);
                    nuint sourceSize = (nuint)(source.Length - bytesRead);
                    nuint hint = LZ4Interop.FrameDecompress(decompressionContext, destination[bytesWritten..], ref destinationSize, source[bytesRead..], ref sourceSize, ReadOnlySpan<byte>.Empty);
                    if (LZ4Interop.FrameIsError(hint) != 0)
                        throw new CompressionException("Failed to decompress data.", "decompression", LZ4Interop.FrameGetErrorName(hint));
                    if (destinationSize > (nuint)(destination.Length - bytesWritten) || sourceSize > (nuint)(source.Length - bytesRead))
                        throw new InvalidDataException("The LZ4 frame decoder returned invalid buffer sizes.");

                    bytesWritten += checked((int)destinationSize);
                    bytesRead += checked((int)sourceSize);
                    if (sourceSize == 0 && destinationSize == 0)
                        throw new InvalidDataException("The LZ4 frame decoder made no progress.");
                    if (hint != 0 && bytesRead == source.Length)
                        throw new InvalidDataException("The LZ4 frame is incomplete.");
                }

                return bytesWritten;
            }
            finally
            {
                LZ4Interop.FrameFreeDecompressionContext(decompressionContext);
            }
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("LZ4FrameCodec currently does not support dictionaries.");

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize)
        {
            if (inputSize < 0)
                throw new ArgumentOutOfRangeException(nameof(inputSize));

            nuint bound = LZ4Interop.FrameGetMaxCompressedSize((nuint)inputSize, ReadOnlySpan<byte>.Empty);
            if (LZ4Interop.FrameIsError(bound) != 0 || bound > int.MaxValue)
                throw new CompressionException("Failed to get the LZ4 frame size bound.", "compression", LZ4Interop.FrameGetErrorName(bound));

            return (int)bound;
        }

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => throw new NotSupportedException("LZ4 does not store decompressed size.");
    }
}
