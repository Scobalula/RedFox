// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression.Deflate;

/// <summary>
/// Compresses and decompresses raw Deflate data, optionally using a preset dictionary.
/// </summary>
public class DeflateCodec : CompressionCodec
{
    private readonly int _windowBits;
    private readonly byte[] _dictionary;
    private readonly ThreadLocal<ZLibContext> _compressors;
    private readonly ThreadLocal<ZLibContext> _decompressors;
    private bool _disposed;

    /// <summary>
    /// Gets the compression level selected when this codec was created.
    /// </summary>
    public DeflateLevel CompressionLevel { get; }

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Compression | CompressionCodecFlags.Decompression | CompressionCodecFlags.CompressionStreams | CompressionCodecFlags.DecompressionStreams | (_windowBits == 31 ? CompressionCodecFlags.None : CompressionCodecFlags.Dictionaries);

    /// <summary>
    /// Initializes a codec using the default compression level.
    /// </summary>
    public DeflateCodec() : this(DeflateLevel.DefaultCompression, []) { }

    /// <summary>
    /// Initializes a codec using the specified compression level.
    /// </summary>
    /// <param name="level">The compression level.</param>
    public DeflateCodec(DeflateLevel level) : this(level, []) { }

    /// <summary>
    /// Initializes a codec using the specified compression level and preset dictionary.
    /// </summary>
    /// <param name="level">The compression level.</param>
    /// <param name="dictionary">The preset history dictionary.</param>
    public DeflateCodec(DeflateLevel level, ReadOnlySpan<byte> dictionary) : this(-15, level, dictionary) { }

    /// <summary>
    /// Initializes a Deflate codec with a format window, level, and optional dictionary.
    /// </summary>
    /// <param name="windowBits">The zlib window setting for the selected wrapper format.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="dictionary">The preset history dictionary.</param>
    protected DeflateCodec(int windowBits, DeflateLevel level, ReadOnlySpan<byte> dictionary)
    {
        if ((int)level < -1 || (int)level > 9)
            throw new ArgumentOutOfRangeException(nameof(level));
        if (windowBits == 31 && !dictionary.IsEmpty)
            throw new NotSupportedException("Gzip does not support preset dictionaries.");

        _windowBits = windowBits;
        _dictionary = dictionary.ToArray();
        CompressionLevel = level;
        _compressors = new(() => new ZLibContext(true, _windowBits, CompressionLevel, _dictionary), true);
        _decompressors = new(() => new ZLibContext(false, _windowBits, CompressionLevel, _dictionary), true);
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
        return checked((int)DeflateInterop.CompressBound((uint)inputSize) + 32);
    }

    /// <inheritdoc/>
    public override Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ZLibCompressionStream(destination, leaveOpen, uncompressedSize, true, _windowBits, CompressionLevel, _dictionary);
    }

    /// <inheritdoc/>
    public override Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ZLibCompressionStream(source, leaveOpen, compressedSize, false, _windowBits, CompressionLevel, _dictionary);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (ZLibContext context in _compressors.Values)
            context.Dispose();
        foreach (ZLibContext context in _decompressors.Values)
            context.Dispose();
        _compressors.Dispose();
        _decompressors.Dispose();

        GC.SuppressFinalize(this);
    }
}
