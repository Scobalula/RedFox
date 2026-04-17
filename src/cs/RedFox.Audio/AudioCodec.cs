// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio;

/// <summary>
/// Abstract base class for audio codecs, providing methods for encoding and decoding
/// audio data between compressed formats and 16-bit PCM samples.
/// </summary>
public abstract class AudioCodec
{
    /// <summary>
    /// Gets the name of the codec, used for identification and registration.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the capabilities supported by this codec.
    /// </summary>
    public abstract AudioCodecFlags Flags { get; }

    /// <summary>
    /// Gets the file extensions this codec can handle, including the leading period (e.g. <c>".wav"</c>).
    /// Returns an empty list if the codec does not map to a specific file extension.
    /// </summary>
    public virtual IReadOnlyList<string> Extensions => [];

    /// <summary>
    /// Decodes compressed audio data from the specified source buffer into 16-bit PCM samples.
    /// </summary>
    /// <param name="source">The compressed audio data to decode.</param>
    /// <param name="destination">The buffer to receive decoded 16-bit PCM samples in interleaved channel order.</param>
    /// <param name="format">The format of the compressed source data.</param>
    /// <returns>The number of samples written to <paramref name="destination"/>.</returns>
    public abstract int Decode(ReadOnlySpan<byte> source, Span<short> destination, AudioFormat format);

    /// <summary>
    /// Encodes 16-bit PCM samples into compressed audio data.
    /// </summary>
    /// <param name="source">The interleaved 16-bit PCM samples to encode.</param>
    /// <param name="destination">The buffer to receive the compressed audio data.</param>
    /// <param name="format">The target format for the encoded data.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    public abstract int Encode(ReadOnlySpan<short> source, Span<byte> destination, AudioFormat format);

    /// <summary>
    /// Gets the maximum number of 16-bit PCM samples that would result from decoding
    /// the specified number of compressed bytes.
    /// </summary>
    /// <param name="encodedSize">The size of the compressed data in bytes.</param>
    /// <param name="format">The format of the compressed data.</param>
    /// <returns>The maximum number of decoded samples.</returns>
    public abstract int GetMaxDecodedSize(int encodedSize, AudioFormat format);

    /// <summary>
    /// Gets the maximum number of bytes required to encode the specified number of PCM samples.
    /// </summary>
    /// <param name="sampleCount">The number of interleaved PCM samples to encode.</param>
    /// <param name="format">The target format for the encoded data.</param>
    /// <returns>The maximum number of encoded bytes.</returns>
    public abstract int GetMaxEncodedSize(int sampleCount, AudioFormat format);

    /// <summary>
    /// Decodes compressed audio data from a byte array into 16-bit PCM samples,
    /// using specified offsets and lengths.
    /// </summary>
    /// <param name="source">The compressed audio data to decode.</param>
    /// <param name="sourceOffset">The starting offset in the source array.</param>
    /// <param name="sourceCount">The number of bytes to decode from the source array.</param>
    /// <param name="destination">The buffer to receive decoded samples.</param>
    /// <param name="destinationOffset">The starting offset in the destination array.</param>
    /// <param name="format">The format of the compressed source data.</param>
    /// <returns>The number of samples written to <paramref name="destination"/>.</returns>
    public virtual int Decode(byte[] source, int sourceOffset, int sourceCount, short[] destination, int destinationOffset, AudioFormat format)
    {
        return Decode(
            source.AsSpan(sourceOffset, sourceCount),
            destination.AsSpan(destinationOffset),
            format);
    }

    /// <summary>
    /// Encodes 16-bit PCM samples from a short array into compressed audio data,
    /// using specified offsets and lengths.
    /// </summary>
    /// <param name="source">The interleaved PCM samples to encode.</param>
    /// <param name="sourceOffset">The starting offset in the source array.</param>
    /// <param name="sourceCount">The number of samples to encode from the source array.</param>
    /// <param name="destination">The buffer to receive the compressed data.</param>
    /// <param name="destinationOffset">The starting offset in the destination array.</param>
    /// <param name="format">The target format for the encoded data.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    public virtual int Encode(short[] source, int sourceOffset, int sourceCount, byte[] destination, int destinationOffset, AudioFormat format)
    {
        return Encode(
            source.AsSpan(sourceOffset, sourceCount),
            destination.AsSpan(destinationOffset),
            format);
    }

    /// <summary>
    /// Decodes an entire compressed audio buffer into an <see cref="AudioBuffer"/>.
    /// </summary>
    /// <param name="source">The compressed audio data to decode.</param>
    /// <param name="format">The format of the compressed source data.</param>
    /// <returns>An <see cref="AudioBuffer"/> containing the decoded 16-bit PCM samples.</returns>
    public virtual AudioBuffer Decode(ReadOnlySpan<byte> source, AudioFormat format)
    {
        var maxSamples = GetMaxDecodedSize(source.Length, format);
        var samples = GC.AllocateUninitializedArray<short>(maxSamples);
        var count = Decode(source, samples, format);

        return new AudioBuffer
        {
            SampleRate = format.SampleRate,
            Channels = format.Channels,
            Samples = new Memory<short>(samples, 0, count),
        };
    }

    /// <summary>
    /// Encodes 16-bit PCM samples into a new byte array.
    /// </summary>
    /// <param name="source">The interleaved 16-bit PCM samples to encode.</param>
    /// <param name="format">The target format for the encoded data.</param>
    /// <returns>A byte array containing the compressed audio data.</returns>
    public virtual byte[] Encode(ReadOnlySpan<short> source, AudioFormat format)
    {
        var maxSize = GetMaxEncodedSize(source.Length, format);
        var encoded = GC.AllocateUninitializedArray<byte>(maxSize);
        var count = Encode(source, encoded, format);

        if (count < maxSize)
            Array.Resize(ref encoded, count);

        return encoded;
    }

    /// <summary>
    /// Reads compressed audio data from a file and decodes it into an <see cref="AudioBuffer"/>.
    /// </summary>
    /// <param name="filePath">The path to the file containing compressed audio data.</param>
    /// <param name="format">The format of the compressed data in the file.</param>
    /// <returns>An <see cref="AudioBuffer"/> containing the decoded 16-bit PCM samples.</returns>
    public virtual AudioBuffer Read(string filePath, AudioFormat format)
    {
        return Decode(File.ReadAllBytes(filePath), format);
    }

    /// <summary>
    /// Encodes 16-bit PCM samples and writes the compressed data to a file.
    /// </summary>
    /// <param name="filePath">The path of the file to write.</param>
    /// <param name="samples">The interleaved 16-bit PCM samples to encode.</param>
    /// <param name="format">The target format for the encoded data.</param>
    public virtual void Write(string filePath, ReadOnlySpan<short> samples, AudioFormat format)
    {
        var encoded = Encode(samples, format);
        File.WriteAllBytes(filePath, encoded);
    }

    /// <summary>
    /// Reads compressed audio data from a stream and decodes it into an <see cref="AudioBuffer"/>.
    /// </summary>
    /// <param name="stream">A readable stream containing compressed audio data.</param>
    /// <param name="format">The format of the compressed data in the stream.</param>
    /// <returns>An <see cref="AudioBuffer"/> containing the decoded 16-bit PCM samples.</returns>
    public virtual AudioBuffer Read(Stream stream, AudioFormat format)
    {
        var length = (int)(stream.Length - stream.Position);
        var bytes = GC.AllocateUninitializedArray<byte>(length);
        stream.ReadExactly(bytes);
        return Decode(bytes, format);
    }

    /// <summary>
    /// Encodes 16-bit PCM samples and writes the compressed data to a stream.
    /// </summary>
    /// <param name="stream">A writable stream to receive the compressed data.</param>
    /// <param name="samples">The interleaved 16-bit PCM samples to encode.</param>
    /// <param name="format">The target format for the encoded data.</param>
    public virtual void Write(Stream stream, ReadOnlySpan<short> samples, AudioFormat format)
    {
        var encoded = Encode(samples, format);
        stream.Write(encoded);
    }
}
