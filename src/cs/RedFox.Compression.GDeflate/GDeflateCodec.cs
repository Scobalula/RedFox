// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;
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
            if (source.Length < 8)
                throw new InvalidDataException("The GDeflate header is incomplete.");
            if (source[0] != 4 || source[1] != 0xFB)
                throw new NotSupportedException("The GDeflate header is unsupported.");

            int tileCount = BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);
            if (tileCount == 0)
                throw new InvalidDataException("The GDeflate stream contains no tiles.");

            int offsetsLength = checked(tileCount * sizeof(uint));
            int headerLength = checked(8 + offsetsLength);
            if (source.Length < headerLength)
                throw new InvalidDataException("The GDeflate tile offset table is incomplete.");

            ReadOnlySpan<byte> payload = source[headerLength..];
            uint compressedLength = BinaryPrimitives.ReadUInt32LittleEndian(source[8..]);
            if (compressedLength == 0 || compressedLength > payload.Length)
                throw new InvalidDataException("The GDeflate compressed length is invalid.");

            var tiles = new GDeflatePage[tileCount];
            uint previousOffset = 0;
            for (int tileIndex = 1; tileIndex < tileCount; tileIndex++)
            {
                uint tileOffset = BinaryPrimitives.ReadUInt32LittleEndian(source[(8 + tileIndex * sizeof(uint))..]);
                if (tileOffset <= previousOffset || tileOffset >= compressedLength)
                    throw new InvalidDataException("The GDeflate tile offsets are invalid.");

                previousOffset = tileOffset;
            }

            unsafe
            {
                fixed (byte* payloadPointer = payload)
                {
                    for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
                    {
                        uint tileStart = tileIndex == 0 ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(source[(8 + tileIndex * sizeof(uint))..]);
                        uint tileEnd = tileIndex == tileCount - 1 ? compressedLength : BinaryPrimitives.ReadUInt32LittleEndian(source[(8 + (tileIndex + 1) * sizeof(uint))..]);

                        tiles[tileIndex] = new GDeflatePage
                        {
                            Data = payloadPointer + checked((int)tileStart),
                            ByteCount = (nuint)(tileEnd - tileStart)
                        };
                    }

                    nint decompressor = GDeflateInterop.CreateDecompressor();
                    if (decompressor == 0)
                        throw new InvalidOperationException("Failed to allocate a GDeflate decompressor.");

                    try
                    {
                        nuint pageCount = (nuint)tiles.Length;
                        nuint destinationCapacity = (nuint)destination.Length;
                        int result = GDeflateInterop.Decompress(decompressor, tiles, pageCount, destination, destinationCapacity, out nuint returnedValue);
                        if (result != 0)
                            throw new InvalidDataException($"GDeflate decompression failed with error code {result}.");
                        if (returnedValue > (nuint)destination.Length)
                            throw new InvalidDataException("GDeflate returned an output size larger than the destination.");

                        return checked((int)returnedValue);
                    }
                    finally
                    {
                        GDeflateInterop.FreeDecompressor(decompressor);
                    }
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
