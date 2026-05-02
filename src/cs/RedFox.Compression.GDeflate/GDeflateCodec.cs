// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace RedFox.Compression.GDeflate
{
    /// <summary>
    /// A <see cref="CompressionCodec"/> that provides a wrapper around GDeflate.
    /// </summary>
    public class GDeflateCodec : CompressionCodec
    {
        /// <inheritdoc/>
        public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

        /// <summary>
        /// Gets or Sets the compression level indicating to GDeflate the level of compression to use.
        /// Higher values result in better compression but longer compression times.
        /// </summary>
        public int CompressionLevel { get; set; } = 1;

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            return Compress(source, destination);
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            var decompressor = GDeflateInterop.CreateDecompressor();

            if (source[0] != 4)
                throw new NotSupportedException();
            if (source[1] != 0xFB)
                throw new NotSupportedException();

            var fuck = source[2..];

            var numTiles = MemoryMarshal.Read<ushort>(source[2..]);
            var packedInfo = MemoryMarshal.Read<uint>(source[4..]);

            var tileSizeIdx = packedInfo & 0x3;
            var lastTileSize = (packedInfo >> 2) & 0x3FFFF;
            var reserved1 = (packedInfo >> 20) & 0xFFF;

            var tileOffsets = MemoryMarshal.Cast<byte, int>(source[8..]);
            var inDataPointer = source[(8 + numTiles * 4)..];

            unsafe
            {
                fixed (byte* buffer = &inDataPointer[0])
                {
                    var tiles = new GDeflatePage[numTiles];

                    for (int tileIndex = 0; tileIndex < numTiles; tileIndex++)
                    {
                        int tileOffset = tileIndex > 0 ? tileOffsets[tileIndex] : 0;

                        tiles[tileIndex] = new()
                        {
                            Data = buffer + tileOffset,
                            Bytes = tileIndex < numTiles - 1 ? tileOffsets[tileIndex + 1] - tileOffset : tileOffsets[0]
                        };
                    }

                    var result = GDeflateInterop.Decompress(decompressor, tiles, tiles.Length, destination, destination.Length, out var returnedValue);
                    return returnedValue;
                }
            }
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary) => throw new NotSupportedException("GDeflate does not support dictionary-based decompression.");

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize) => throw new NotImplementedException();

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => throw new NotSupportedException("GDeflate does not store the decompressed size.");
    }
}
