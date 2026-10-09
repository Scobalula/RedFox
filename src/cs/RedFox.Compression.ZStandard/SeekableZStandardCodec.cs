using System.Buffers.Binary;
using System.IO.Hashing;

namespace RedFox.Compression.ZStandard;

/// <summary>
/// Compresses and decompresses Zstandard data as independently indexed frames.
/// </summary>
public sealed class SeekableZStandardCodec : CompressionCodec
{
    private readonly ZStandardCodec _codec;
    private readonly byte[] _dictionary;
    private bool _disposed;

    /// <summary>
    /// Gets the compression level selected for this codec.
    /// </summary>
    public int CompressionLevel { get; }

    /// <summary>
    /// Gets the maximum uncompressed size of each frame.
    /// </summary>
    public int FrameSize { get; }

    /// <summary>
    /// Gets whether seek-table checksums are written and validated.
    /// </summary>
    public bool Checksums { get; }

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => _codec.Flags;

    /// <summary>
    /// Initializes a codec with 1 MiB frames and checksums enabled.
    /// </summary>
    public SeekableZStandardCodec() : this(3, 1048576, true, ReadOnlySpan<byte>.Empty) { }

    /// <summary>
    /// Initializes a codec using the specified frame settings and optional dictionary.
    /// </summary>
    /// <param name="level">The Zstandard compression level.</param>
    /// <param name="frameSize">The maximum uncompressed frame size.</param>
    /// <param name="checksums">Whether seek-table checksums are enabled.</param>
    /// <param name="dictionary">The preset Zstandard dictionary.</param>
    public SeekableZStandardCodec(int level, int frameSize, bool checksums, ReadOnlySpan<byte> dictionary)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameSize);
        _codec = new ZStandardCodec(level, dictionary);
        _dictionary = dictionary.ToArray();
        CompressionLevel = level;
        FrameSize = frameSize;
        Checksums = checksums;
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int count = source.IsEmpty ? 0 : (source.Length - 1) / FrameSize + 1;
        int entrySize = Checksums ? 12 : 8;
        int tableSize = checked(17 + count * entrySize);
        if (destination.Length < tableSize)
            throw new CompressionException("The destination is too small for the Zstandard seek table.");

        Span<byte> table = destination[^tableSize..];
        WriteTableHeader(table, count, Checksums);
        int written = 0;
        for (int i = 0; i < count; i++)
        {
            int offset = i * FrameSize;
            ReadOnlySpan<byte> frame = source.Slice(offset, Math.Min(FrameSize, source.Length - offset));
            int compressed = _codec.Compress(frame, destination.Slice(written, destination.Length - tableSize - written));
            uint checksum = Checksums ? (uint)XxHash64.HashToUInt64(frame) : 0;
            WriteEntry(table[(8 + i * entrySize)..], compressed, frame.Length, checksum, Checksums);
            written += compressed;
        }

        WriteTableFooter(table[^9..], count, Checksums);
        table.CopyTo(destination[written..]);
        return written + tableSize;
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (source.Length < 17)
            throw new CompressionException("The seekable Zstandard input is truncated.");

        var footer = ReadFooter(source[^9..]);
        if (footer.TableSize > source.Length)
            throw new CompressionException("The Zstandard seek table is truncated.");

        int payloadSize = source.Length - (int)footer.TableSize;
        var index = ReadEntries(source[payloadSize..], payloadSize);
        int written = 0;
        for (int i = 0; i < index.Blocks.Length; i++)
        {
            CompressedBlock block = index.Blocks[i];
            if (block.DecompressedLength > destination.Length - written)
                throw new CompressionException("The seekable Zstandard destination is too small.");

            Span<byte> output = destination.Slice(written, block.DecompressedLength);
            written += DecodeFrame(_codec, index.Checksums, i, source.Slice((int)block.CompressedOffset, block.CompressedLength), output);
        }

        return written;
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        int count = inputSize == 0 ? 0 : (inputSize - 1) / FrameSize + 1;
        long bound = 17L + count * (Checksums ? 12L : 8L);
        if (count != 0)
            bound += (count - 1L) * _codec.GetMaxCompressedSize(FrameSize) + _codec.GetMaxCompressedSize(inputSize - (count - 1) * FrameSize);

        return checked((int)bound);
    }

    /// <inheritdoc/>
    public override bool TryGetDecompressedSize(ReadOnlySpan<byte> source, out long size)
    {
        if (source.Length < 17)
            throw new CompressionException("The seekable Zstandard input is truncated.");

        var footer = ReadFooter(source[^9..]);
        if (footer.TableSize > source.Length)
            throw new CompressionException("The Zstandard seek table is truncated.");

        int payloadSize = source.Length - (int)footer.TableSize;
        size = ValidateTable(source[payloadSize..], payloadSize);
        return true;
    }

    /// <inheritdoc/>
    public override Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SeekableZStandardCompressionStream(destination, leaveOpen, uncompressedSize, CompressionLevel, FrameSize, Checksums, _dictionary);
    }

    /// <inheritdoc/>
    public override Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SeekableZStandardDecompressionStream(source, leaveOpen, compressedSize, CompressionLevel, _dictionary);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _disposed = true;
        _codec.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static (int Count, bool Checksums, long TableSize) ReadFooter(ReadOnlySpan<byte> footer)
    {
        if (footer.Length < 9 || BinaryPrimitives.ReadUInt32LittleEndian(footer[5..]) != 0x8F92EAB1)
            throw new CompressionException("The Zstandard seek table footer is invalid.");
        if ((footer[4] & 0x7C) != 0)
            throw new CompressionException("The Zstandard seek table uses reserved descriptor bits.");

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(footer);
        bool checksums = (footer[4] & 0x80) != 0;
        long size = 17L + count * (checksums ? 12L : 8L);
        if (count > int.MaxValue || size - 8 > uint.MaxValue)
            throw new CompressionException("The Zstandard seek table is too large.");

        return ((int)count, checksums, size);
    }

    internal static long ValidateTable(ReadOnlySpan<byte> table, long payloadSize)
    {
        if (table.Length < 17)
            throw new CompressionException("The Zstandard seek table is truncated.");

        var footer = ReadFooter(table[^9..]);
        if (footer.TableSize != table.Length || BinaryPrimitives.ReadUInt32LittleEndian(table) != 0x184D2A5E || BinaryPrimitives.ReadUInt32LittleEndian(table[4..]) != table.Length - 8)
            throw new CompressionException("The Zstandard seek table header or length is invalid.");

        long compressed = 0;
        long decompressed = 0;
        int entrySize = footer.Checksums ? 12 : 8;
        for (int i = 0; i < footer.Count; i++)
        {
            ReadOnlySpan<byte> entry = table[(8 + i * entrySize)..];
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            if (length == 0)
                throw new CompressionException("A Zstandard seek entry has zero compressed length.");

            compressed += length;
            decompressed += BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
        }
        if (compressed != payloadSize)
            throw new CompressionException("The Zstandard seek entries do not cover the payload exactly.");

        return decompressed;
    }

    internal static (CompressedBlock[] Blocks, uint[]? Checksums) ReadEntries(ReadOnlySpan<byte> table, long payloadSize)
    {
        ValidateTable(table, payloadSize);
        var footer = ReadFooter(table[^9..]);
        CompressedBlock[] blocks = new CompressedBlock[footer.Count];
        uint[]? checksums = footer.Checksums ? new uint[footer.Count] : null;
        int entrySize = footer.Checksums ? 12 : 8;
        long offset = 0;
        for (int i = 0; i < blocks.Length; i++)
        {
            ReadOnlySpan<byte> entry = table[(8 + i * entrySize)..];
            uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            uint decompressed = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            if (compressed > Array.MaxLength || decompressed > Array.MaxLength)
                throw new NotSupportedException("A seekable Zstandard frame exceeds the .NET buffer limit.");

            blocks[i] = new CompressedBlock(offset, (int)compressed, (int)decompressed);
            if (checksums != null)
                checksums[i] = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);

            offset += compressed;
        }

        return (blocks, checksums);
    }

    internal static void WriteTableHeader(Span<byte> destination, int count, bool checksums)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x184D2A5E);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], checked((uint)(9L + count * (checksums ? 12L : 8L))));
    }

    internal static void WriteTableFooter(Span<byte> destination, int count, bool checksums)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)count);
        destination[4] = checksums ? (byte)0x80 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[5..], 0x8F92EAB1);
    }

    internal static void WriteEntry(Span<byte> destination, int compressed, int decompressed, uint checksum, bool checksums)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)compressed);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], (uint)decompressed);
        if (checksums)
            BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], checksum);
    }

    internal static int DecodeFrame(ZStandardCodec codec, uint[]? checksums, int index, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (ZStandardInterop.Check(ZStandardInterop.GetFrameCompressedSize(source, (nuint)source.Length)) != (nuint)source.Length)
            throw new CompressionException($"Seekable Zstandard entry {index} must contain exactly one frame.");

        int written = codec.Decompress(source, destination);
        if (written != destination.Length || (checksums != null && (uint)XxHash64.HashToUInt64(destination) != checksums[index]))
            throw new CompressionException($"Seekable Zstandard frame {index} has an invalid size or checksum.");

        return written;
    }

}
