// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression.Deflate
{
    /// <summary>
    /// A <see cref="CompressionCodec"/> that provides a wrapper around ZLIB (MiniZ).
    /// </summary>
    public unsafe class DeflateCodec : CompressionCodec
    {
        /// <inheritdoc/>
        public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

        /// <summary>
        /// Gets or Sets the compression level indicating to MiniZ the level of compression to use.
        /// </summary>
        public DeflateLevel CompressionLevel { get; set; } = DeflateLevel.DefaultCompression;

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            DeflateInterop.MZStream stream = new();

            fixed (byte* pSource = &source[0])
            fixed (byte* pDest = &destination[0])
            {
                int status = 0;

                stream.NextIn = pSource;
                stream.AvailIn = (uint)source.Length;
                stream.NextOut = pDest;
                stream.AvailOut = (uint)destination.Length;

                status = DeflateInterop.DeflateInit(ref stream, CompressionLevel, DeflateInterop.MZDeflated, -DeflateInterop.MZDefaultWindowBits, 9, DeflateInterop.MZDefaultStrategy);

                if (status != 0)
                    throw new CompressionException($"DeflateInit failed: {status}");

                status = DeflateInterop.Deflate(ref stream, 4);
                DeflateInterop.DeflateEnd(ref stream);

                if (status != 1)
                    throw new CompressionException($"Deflate failed: {status}");

                return (int)stream.TotalOut;
            }
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            return Compress(source, destination);
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            DeflateInterop.MZStream stream = new();

            fixed(byte* pSource = &source[0])
            fixed(byte* pDest = &destination[0])
            {
                int status = 0;

                stream.NextIn = pSource;
                stream.AvailIn = (uint)source.Length;
                stream.NextOut = pDest;
                stream.AvailOut = (uint)destination.Length;

                status = DeflateInterop.InflateInit(ref stream, -15);

                if (status != 0)
                    throw new CompressionException($"InflateInit failed: {status}");

                status = DeflateInterop.Inflate(ref stream, 4);
                DeflateInterop.InflateEnd(ref stream);

                if (status != 1)
                    throw new CompressionException($"Inflate failed: {status}");

                return (int)stream.TotalOut;
            }
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("");

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize) => DeflateInterop.CompressBound(inputSize);

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => throw new NotSupportedException("Deflate does not store the decompressed size");
    }
}
