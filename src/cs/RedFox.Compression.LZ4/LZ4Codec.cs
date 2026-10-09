// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Compression.LZ4;

/// <summary>
/// Compresses and decompresses raw LZ4 blocks, optionally using a preset dictionary.
/// </summary>
public sealed class LZ4Codec : CompressionCodec
{
    private readonly byte[] _dictionary;
    private GCHandle _dictionaryHandle;
    private nint _dictionaryStream;
    private nint _stream;
    private bool _disposed;

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Compression | CompressionCodecFlags.Decompression | CompressionCodecFlags.Dictionaries;

    /// <summary>
    /// Initializes a codec without a preset dictionary.
    /// </summary>
    public LZ4Codec() : this(ReadOnlySpan<byte>.Empty) { }

    /// <summary>
    /// Initializes a codec with a preset history dictionary.
    /// </summary>
    /// <param name="dictionary">The history data used during compression and decompression.</param>
    public LZ4Codec(ReadOnlySpan<byte> dictionary)
    {
        _dictionary = dictionary.Length > 65536 ? dictionary[^65536..].ToArray() : dictionary.ToArray();
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stream == 0)
        {
            _stream = LZ4Interop.CreateStream();
            if (_stream == 0)
                throw new CompressionException("Failed to allocate an LZ4 stream.");
        }
        if (_dictionary.Length != 0 && _dictionaryStream == 0)
        {
            _dictionaryStream = LZ4Interop.CreateStream();
            if (_dictionaryStream == 0)
                throw new CompressionException("Failed to prepare the LZ4 dictionary.");

            _dictionaryHandle = GCHandle.Alloc(_dictionary, GCHandleType.Pinned);
            int loaded = LZ4Interop.LoadDictionary(_dictionaryStream, _dictionaryHandle.AddrOfPinnedObject(), _dictionary.Length);
            if (loaded != _dictionary.Length)
                throw new CompressionException($"LZ4 loaded {loaded} of {_dictionary.Length} dictionary bytes.");
        }

        LZ4Interop.ResetStream(_stream);
        LZ4Interop.AttachDictionary(_stream, _dictionaryStream);
        int written = LZ4Interop.Compress(_stream, source, destination, source.Length, destination.Length, 1);
        GC.KeepAlive(this);
        if (written <= 0)
            throw new CompressionException("LZ4 compression failed; check the input and destination sizes.");

        return written;
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int written = LZ4Interop.Decompress(source, destination, source.Length, destination.Length, _dictionary, _dictionary.Length);
        if (written < 0)
            throw new CompressionException($"LZ4 decompression failed ({written}).");

        return written;
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        int bound = LZ4Interop.GetMaxCompressedSize(inputSize);
        if (bound == 0)
            throw new ArgumentOutOfRangeException(nameof(inputSize));

        return bound;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        int streamStatus = 0;
        int dictionaryStreamStatus = 0;
        if (_stream != 0)
            streamStatus = LZ4Interop.FreeStream(_stream);
        if (_dictionaryStream != 0)
            dictionaryStreamStatus = LZ4Interop.FreeStream(_dictionaryStream);
        if (_dictionaryHandle.IsAllocated)
            _dictionaryHandle.Free();

        _stream = 0;
        _dictionaryStream = 0;
        _disposed = true;
        GC.SuppressFinalize(this);

        if (streamStatus != 0 || dictionaryStreamStatus != 0)
            throw new CompressionException($"LZ4 stream cleanup failed ({streamStatus}, {dictionaryStreamStatus}).");
    }

    /// <summary>
    /// Releases native codec state if the codec was not disposed.
    /// </summary>
    ~LZ4Codec() => Dispose();
}
