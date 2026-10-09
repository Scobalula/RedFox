// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Compression.ZStandard;

/// <summary>
/// Compresses and decompresses Zstandard frames, optionally using a dictionary.
/// </summary>
public sealed class ZStandardCodec : CompressionCodec
{
    private readonly byte[] _dictionary;
    private ZStandardContext? _compressor;
    private ZStandardContext? _decompressor;
    private bool _disposed;

    /// <summary>
    /// Gets the compression level selected for this codec.
    /// </summary>
    public int CompressionLevel { get; }

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Compression | CompressionCodecFlags.Decompression | CompressionCodecFlags.CompressionStreams | CompressionCodecFlags.DecompressionStreams | CompressionCodecFlags.Dictionaries | CompressionCodecFlags.SizeInspection;

    /// <summary>
    /// Initializes a codec using the default compression level.
    /// </summary>
    public ZStandardCodec() : this(3, ReadOnlySpan<byte>.Empty) { }

    /// <summary>
    /// Initializes a codec using the specified compression level.
    /// </summary>
    /// <param name="level">The compression level supported by the native library.</param>
    public ZStandardCodec(int level) : this(level, ReadOnlySpan<byte>.Empty) { }

    /// <summary>
    /// Initializes a codec using the specified compression level and dictionary.
    /// </summary>
    /// <param name="level">The compression level supported by the native library.</param>
    /// <param name="dictionary">The preset Zstandard dictionary.</param>
    public ZStandardCodec(int level, ReadOnlySpan<byte> dictionary)
    {
        if (level < ZStandardInterop.MinLevel() || level > ZStandardInterop.MaxLevel())
            throw new ArgumentOutOfRangeException(nameof(level));

        CompressionLevel = level;
        _dictionary = dictionary.ToArray();
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _compressor ??= new ZStandardContext(true, CompressionLevel, _dictionary, null);
        return _compressor.Transform(source, destination);
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _decompressor ??= new ZStandardContext(false, CompressionLevel, _dictionary, null);
        return _decompressor.Transform(source, destination);
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        return checked((int)ZStandardInterop.Check(ZStandardInterop.GetMaxCompressedSize((nuint)inputSize)));
    }

    /// <inheritdoc/>
    public override bool TryGetDecompressedSize(ReadOnlySpan<byte> source, out long size)
    {
        size = 0;
        bool known = true;
        if (source.IsEmpty)
            throw new CompressionException("The Zstandard frame is empty.");

        while (!source.IsEmpty)
        {
            ulong contentSize = ZStandardInterop.GetDecompressedSize(source, (nuint)source.Length);
            if (contentSize == ulong.MaxValue - 1)
                throw new CompressionException("The Zstandard frame header is invalid.");
            if (contentSize == ulong.MaxValue)
                known = false;
            else if (contentSize > long.MaxValue || contentSize > (ulong)(long.MaxValue - size))
                throw new CompressionException("The Zstandard content size exceeds Int64.");
            else
                size += (long)contentSize;

            int frameSize = checked((int)ZStandardInterop.Check(ZStandardInterop.GetFrameCompressedSize(source, (nuint)source.Length)));
            source = source[frameSize..];
        }

        if (!known)
            size = 0;

        return known;
    }

    /// <inheritdoc/>
    public override Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ZStandardCompressionStream(destination, leaveOpen, uncompressedSize, true, CompressionLevel, _dictionary);
    }

    /// <inheritdoc/>
    public override Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ZStandardCompressionStream(source, leaveOpen, compressedSize, false, CompressionLevel, _dictionary);
    }

    /// <summary>
    /// Trains a Zstandard dictionary from concatenated samples.
    /// </summary>
    /// <param name="samples">The concatenated training sample data.</param>
    /// <param name="sampleLengths">The length of each sample in <paramref name="samples"/>.</param>
    /// <param name="destination">The buffer that receives the trained dictionary.</param>
    /// <returns>The number of dictionary bytes written.</returns>
    public static int TrainDictionary(ReadOnlySpan<byte> samples, ReadOnlySpan<int> sampleLengths, Span<byte> destination)
    {
        nuint[] lengths = new nuint[sampleLengths.Length];
        long total = 0;
        for (int i = 0; i < lengths.Length; i++)
        {
            if (sampleLengths[i] <= 0)
                throw new ArgumentException("Sample lengths must be positive.", nameof(sampleLengths));

            lengths[i] = (nuint)sampleLengths[i];
            total += sampleLengths[i];
        }
        if (total != samples.Length || lengths.Length == 0)
            throw new ArgumentException("Sample lengths must cover the concatenated samples exactly.", nameof(sampleLengths));
        if (destination.IsEmpty)
            throw new ArgumentException("The dictionary destination is empty.", nameof(destination));

        nuint result = ZStandardInterop.TrainDictionary(destination, (nuint)destination.Length, samples, lengths, (uint)lengths.Length);
        if (ZStandardInterop.DictionaryIsError(result) != 0)
            throw new CompressionException($"Zstandard dictionary training failed: {Marshal.PtrToStringUTF8(ZStandardInterop.DictionaryErrorName(result))}");

        return checked((int)result);
    }

    /// <summary>
    /// Trains a dictionary using the default dictionary size.
    /// </summary>
    /// <param name="samples">The training samples.</param>
    /// <returns>The trained dictionary.</returns>
    public static byte[] TrainDictionary(IReadOnlyList<byte[]> samples) => TrainDictionary(samples, 65536);

    /// <summary>
    /// Trains a dictionary using the specified dictionary size.
    /// </summary>
    /// <param name="samples">The training samples.</param>
    /// <param name="dictionarySize">The target dictionary size in bytes.</param>
    /// <returns>The trained dictionary.</returns>
    public static byte[] TrainDictionary(IReadOnlyList<byte[]> samples, int dictionarySize)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dictionarySize);
        int[] lengths = new int[samples.Count];
        int total = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(samples[i]);
            lengths[i] = samples[i].Length;
            total = checked(total + lengths[i]);
        }

        byte[] buffer = new byte[total];
        int offset = 0;
        foreach (byte[] sample in samples)
        {
            sample.CopyTo(buffer.AsSpan(offset));
            offset += sample.Length;
        }

        byte[] dictionary = new byte[dictionarySize];
        int written = TrainDictionary(buffer, lengths, dictionary);
        Array.Resize(ref dictionary, written);
        return dictionary;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _compressor?.Dispose();
        _decompressor?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
