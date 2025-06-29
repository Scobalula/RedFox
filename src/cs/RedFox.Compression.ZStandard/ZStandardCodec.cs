// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression.ZStandard
{
    /// <summary>
    /// A <see cref="CompressionCodec"/> that provides a wrapper around ZStandard.
    /// </summary>
    public class ZStandardCodec : CompressionCodec
    {
        /// <inheritdoc/>
        public override CompressionCodecFlags Flags => CompressionCodecFlags.SupportsKnownSize;

        /// <summary>
        /// Gets or Sets the compression level indicating to ZStandard the level of compression to use.
        /// Higher values result in better compression but longer compression times.
        /// </summary>
        public int CompressionLevel { get; set; } = 3;

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            nuint compressedSize = ZStandardInterop.Compress(destination, (nuint)destination.Length, source, (nuint)source.Length, CompressionLevel);

            if (ZStandardInterop.IsError(compressedSize) == 1)
                throw new CompressionException("Failed to compress data.", "compression", ZStandardInterop.GetErrorName(compressedSize));

            return (int)compressedSize;
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            return Compress(source, destination);
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            nuint decompressedSize = ZStandardInterop.Decompress(destination, (nuint)destination.Length, source, (nuint)source.Length);

            if (ZStandardInterop.IsError(decompressedSize) == 1)
                throw new CompressionException("Failed to decompress data.", "decompression", ZStandardInterop.GetErrorName(decompressedSize));

            return (int)decompressedSize;
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("");

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize) => (int)ZStandardInterop.GetMaxCompressedSize((nuint)inputSize);

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => (int)ZStandardInterop.GetDecompressedSize(compressedBuffer, (nuint)compressedBuffer.Length);
    }
}
