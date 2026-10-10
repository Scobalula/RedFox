// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Compression.GDeflate;

/// <summary>
/// Compresses and decompresses data in the DirectStorage GDeflate tile format.
/// </summary>
public sealed class GDeflateCodec : CompressionCodec
{
    internal const int TileSize = 65536;
    internal const long MaxInputSize = 65535L * TileSize;
    private readonly object _sync = new();
    private nint _compressor;
    private nint _decompressor;
    private bool _disposed;

    /// <summary>
    /// Gets the GDeflate compression level selected for this codec.
    /// </summary>
    public int CompressionLevel { get; }

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Compression | CompressionCodecFlags.Decompression | CompressionCodecFlags.CompressionStreams | CompressionCodecFlags.DecompressionStreams | CompressionCodecFlags.SizeInspection;

    /// <summary>
    /// Initializes a codec using the default GDeflate compression level.
    /// </summary>
    public GDeflateCodec() : this(1) { }

    /// <summary>
    /// Initializes a codec using the specified compression level.
    /// </summary>
    /// <param name="level">The compression level from 0 through 12.</param>
    public GDeflateCodec(int level)
    {
        if (level < 0 || level > 12)
            throw new ArgumentOutOfRangeException(nameof(level));

        CompressionLevel = level;
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            int count = WriteHeader(destination, source.Length);
            int headerSize = 8 + count * 4;
            if (destination.Length < headerSize)
                throw new CompressionException("The GDeflate destination is too small.");

            int offset = headerSize;
            for (int i = 0; i < count; i++)
            {
                int sourceOffset = i * TileSize;
                int written = CompressTile(source.Slice(sourceOffset, Math.Min(TileSize, source.Length - sourceOffset)), destination[offset..]);
                BinaryPrimitives.WriteUInt32LittleEndian(destination[(8 + i * 4)..], (uint)(i == 0 ? 0 : offset - headerSize));
                if (i == count - 1)
                    BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], (uint)written);

                offset += written;
            }

            return offset;
        }
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long size = ReadHeader(source, out int count);
            int headerSize = 8 + count * 4;
            if (source.Length < headerSize || size > destination.Length)
                throw new CompressionException("The GDeflate input is truncated or the destination is too small.");

            CompressedBlock[] tiles = ReadTiles(source.Slice(8, count * 4), size, source.Length - headerSize);
            int written = 0;
            foreach (CompressedBlock tile in tiles)
                written += DecompressTile(source.Slice(headerSize + (int)tile.CompressedOffset, tile.CompressedLength), destination.Slice(written, tile.DecompressedLength));

            return written;
        }
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputSize);
        int count = (inputSize - 1) / TileSize + 1;
        long tileBound = (long)GDeflateInterop.CompressBound(0, TileSize, out _);
        return checked((int)(8 + count * 4L + count * tileBound));
    }

    /// <inheritdoc/>
    public override bool TryGetDecompressedSize(ReadOnlySpan<byte> source, out long size)
    {
        size = ReadHeader(source, out int count);
        int headerSize = 8 + count * 4;
        if (source.Length < headerSize)
            throw new CompressionException("The GDeflate tile table is truncated.");

        ReadTiles(source.Slice(8, count * 4), size, source.Length - headerSize);
        return true;
    }

    /// <inheritdoc/>
    public override Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!uncompressedSize.HasValue)
            throw new ArgumentException("GDeflate writing requires the input length.", nameof(uncompressedSize));

        return new GDeflateCompressionStream(destination, leaveOpen, uncompressedSize.Value, CompressionLevel);
    }

    /// <inheritdoc/>
    public override Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new GDeflateDecompressionStream(source, leaveOpen, compressedSize);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        lock (_sync)
        {
            if (_compressor != 0)
                GDeflateInterop.FreeCompressor(_compressor);
            if (_decompressor != 0)
                GDeflateInterop.FreeDecompressor(_decompressor);

            _compressor = 0;
            _decompressor = 0;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    internal static int WriteHeader(Span<byte> destination, long size)
    {
        if (size <= 0 || size > MaxInputSize)
            throw new ArgumentOutOfRangeException(nameof(size));

        int count = (int)((size + TileSize - 1) / TileSize);
        if (destination.Length < 8)
            throw new CompressionException("The GDeflate destination is too small.");

        destination[0] = 4;
        destination[1] = 0xFB;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], (ushort)count);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], 1 | ((uint)(size % TileSize) << 2));
        return count;
    }

    internal static long ReadHeader(ReadOnlySpan<byte> source, out int count)
    {
        if (source.Length < 8)
            throw new CompressionException("The GDeflate header is truncated.");
        if (source[0] != 4 || source[1] != 0xFB)
            throw new CompressionException("The GDeflate header is invalid.");

        count = BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        int lastSize = (int)((flags >> 2) & 0x3FFFF);
        if (count == 0 || (flags & 3) != 1 || (flags >> 20) != 0 || lastSize > TileSize)
            throw new CompressionException("The GDeflate tile header is invalid.");

        return (long)count * TileSize - (lastSize == 0 ? 0 : TileSize - lastSize);
    }

    internal static CompressedBlock[] ReadTiles(ReadOnlySpan<byte> table, long size, long? availablePayload)
    {
        int count = (int)((size + TileSize - 1) / TileSize);
        if (table.Length != count * 4)
            throw new CompressionException("The GDeflate tile table is truncated.");

        long lastOffset = count == 1 ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(table[((count - 1) * 4)..]);
        long payloadSize = lastOffset + BinaryPrimitives.ReadUInt32LittleEndian(table);
        if (payloadSize <= lastOffset || (availablePayload.HasValue && payloadSize != availablePayload.Value))
            throw new CompressionException("The GDeflate payload length is invalid.");

        CompressedBlock[] blocks = new CompressedBlock[count];
        long offset = 0;
        for (int i = 0; i < count; i++)
        {
            long end = i == count - 1 ? payloadSize : BinaryPrimitives.ReadUInt32LittleEndian(table[((i + 1) * 4)..]);
            if (end <= offset || end - offset > int.MaxValue)
                throw new CompressionException("The GDeflate tile offsets are invalid.");

            blocks[i] = new CompressedBlock(offset, (int)(end - offset), (int)Math.Min(TileSize, size - (long)i * TileSize));
            offset = end;
        }

        return blocks;
    }

    internal unsafe int CompressTile(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_compressor == 0)
        {
            _compressor = GDeflateInterop.CreateCompressor(CompressionLevel);
            if (_compressor == 0)
                throw new CompressionException("Failed to allocate the GDeflate compressor.");
        }

        fixed (byte* output = destination)
        {
            Span<GDeflatePage> page = stackalloc GDeflatePage[1];
            page[0] = new GDeflatePage { Data = output, ByteCount = (nuint)destination.Length };
            nuint result = GDeflateInterop.Compress(_compressor, source, (nuint)source.Length, page, 1);
            GC.KeepAlive(this);
            if (result == 0)
                throw new CompressionException("GDeflate compression failed; the destination may be too small.");

            return checked((int)page[0].ByteCount);
        }
    }

    internal unsafe int DecompressTile(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_decompressor == 0)
        {
            _decompressor = GDeflateInterop.CreateDecompressor();
            if (_decompressor == 0)
                throw new CompressionException("Failed to allocate the GDeflate decompressor.");
        }

        fixed (byte* input = source)
        {
            Span<GDeflatePage> page = stackalloc GDeflatePage[1];
            page[0] = new GDeflatePage { Data = input, ByteCount = (nuint)source.Length };
            int result = GDeflateInterop.Decompress(_decompressor, page, 1, destination, (nuint)destination.Length, out nuint written);
            GC.KeepAlive(this);
            if (result != 0 || written != (nuint)destination.Length)
                throw new CompressionException($"GDeflate decompression failed ({result}); expected {destination.Length} bytes, received {written}.");

            return (int)written;
        }
    }

    /// <summary>
    /// Releases native codec state if the codec was not disposed.
    /// </summary>
    ~GDeflateCodec() => Dispose();
}
