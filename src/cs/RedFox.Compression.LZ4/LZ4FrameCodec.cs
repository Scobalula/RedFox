// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Compression.LZ4;

/// <summary>
/// Compresses and decompresses LZ4 frames with optional dictionaries and checksums.
/// </summary>
public sealed class LZ4FrameCodec : CompressionCodec
{
    private readonly LZ4FramePreferences _preferences;
    private readonly byte[] _dictionary;
    private readonly ThreadLocal<LZ4FrameContext> _compressors;
    private readonly ThreadLocal<LZ4FrameContext> _decompressors;
    private bool _disposed;

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Compression | CompressionCodecFlags.Decompression | CompressionCodecFlags.CompressionStreams | CompressionCodecFlags.DecompressionStreams | CompressionCodecFlags.Dictionaries | CompressionCodecFlags.SizeInspection;

    /// <summary>
    /// Initializes a frame codec with the default settings.
    /// </summary>
    public LZ4FrameCodec() : this(0, LZ4FrameBlockSize.KB64, true, false, false, []) { }

    /// <summary>
    /// Initializes a frame codec with a preset dictionary and default settings.
    /// </summary>
    /// <param name="dictionary">The history data used during compression and decompression.</param>
    public LZ4FrameCodec(ReadOnlySpan<byte> dictionary) : this(0, LZ4FrameBlockSize.KB64, true, false, false, dictionary) { }

    /// <summary>
    /// Initializes a frame codec with the specified compression and frame settings.
    /// </summary>
    /// <param name="level">The compression level.</param>
    /// <param name="blockSize">The maximum block size.</param>
    /// <param name="linkedBlocks">Whether blocks share history.</param>
    /// <param name="contentChecksum">Whether to include a content checksum.</param>
    /// <param name="blockChecksum">Whether to include block checksums.</param>
    /// <param name="dictionary">The preset history dictionary.</param>
    public LZ4FrameCodec(int level, LZ4FrameBlockSize blockSize, bool linkedBlocks, bool contentChecksum, bool blockChecksum, ReadOnlySpan<byte> dictionary)
    {
        if (!Enum.IsDefined(blockSize))
            throw new ArgumentOutOfRangeException(nameof(blockSize));

        _dictionary = dictionary.ToArray();
        _preferences = new LZ4FramePreferences
        {
            CompressionLevel = level,
            FrameInfo = new LZ4FrameInfo
            {
                BlockSize			= (int)blockSize,
                BlockMode			= linkedBlocks ? 0 : 1,
                ContentChecksum	= contentChecksum ? 1 : 0,
                BlockChecksum		= blockChecksum ? 1 : 0
            }
        };
        _compressors = new(() => new LZ4FrameContext(true, _preferences, _dictionary), true);
        _decompressors = new(() => new LZ4FrameContext(false, _preferences, _dictionary), true);
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _compressors.Value!.Transform(source, destination);
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _decompressors.Value!.Transform(source, destination);
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        return checked((int)LZ4Interop.Check(LZ4Interop.FrameBound((nuint)inputSize, _preferences)));
    }

    /// <inheritdoc/>
    public override bool TryGetDecompressedSize(ReadOnlySpan<byte> source, out long size)
    {
        if (source.IsEmpty)
            throw new CompressionException("The LZ4 frame is empty.");

        size = 0;
        bool known = true;
        LZ4Interop.Check(LZ4Interop.CreateDecompressionContext(out nint context, 100));
        try
        {
            while (!source.IsEmpty)
            {
                if (source.Length < 7)
                    throw new CompressionException("The LZ4 frame header is truncated.");

                uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source);
                if ((magic & 0xFFFFFFF0) == 0x184D2A50)
                {
                    if (source.Length < 8)
                        throw new CompressionException("The LZ4 skippable header is truncated.");

                    long skip = 8L + BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
                    if (skip > source.Length)
                        throw new CompressionException("The LZ4 skippable frame is truncated.");

                    source = source[(int)skip..];
                    continue;
                }

                LZ4Interop.ResetDecompressionContext(context);
                nuint headerSize = (nuint)source.Length;
                LZ4Interop.Check(LZ4Interop.GetFrameInfo(context, out LZ4FrameInfo info, source, ref headerSize));
                if ((source[4] & 8) == 0)
                    known = false;
                else if (info.ContentSize > long.MaxValue || info.ContentSize > (ulong)(long.MaxValue - size))
                    throw new CompressionException("The LZ4 content size exceeds Int64.");
                else
                    size += (long)info.ContentSize;

                source = source[(int)headerSize..];
                while (true)
                {
                    if (source.Length < 4)
                        throw new CompressionException("The LZ4 block header is truncated.");

                    uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(source);
                    source = source[4..];
                    if (blockSize == 0)
                        break;

                    long bytes = (blockSize & 0x7FFFFFFF) + (info.BlockChecksum == 1 ? 4L : 0L);
                    if ((blockSize & 0x7FFFFFFF) == 0 || (blockSize & 0x7FFFFFFF) > (1 << (8 + 2 * info.BlockSize)) || bytes > source.Length)
                        throw new CompressionException("The LZ4 block size is invalid.");

                    source = source[(int)bytes..];
                }
                if (info.ContentChecksum == 1)
                {
                    if (source.Length < 4)
                        throw new CompressionException("The LZ4 checksum is truncated.");

                    source = source[4..];
                }
            }
        }
        finally
        {
            LZ4Interop.FreeDecompressionContext(context);
        }

        if (!known)
            size = 0;

        return known;
    }

    /// <inheritdoc/>
    public override Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new LZ4FrameCompressionStream(destination, leaveOpen, uncompressedSize, true, _preferences, _dictionary);
    }

    /// <inheritdoc/>
    public override Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new LZ4FrameCompressionStream(source, leaveOpen, compressedSize, false, _preferences, _dictionary);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (LZ4FrameContext context in _compressors.Values)
            context.Dispose();
        foreach (LZ4FrameContext context in _decompressors.Values)
            context.Dispose();
        _compressors.Dispose();
        _decompressors.Dispose();

        GC.SuppressFinalize(this);
    }
}
