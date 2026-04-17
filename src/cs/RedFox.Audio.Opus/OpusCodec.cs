// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Audio.Opus;

/// <summary>
/// Audio codec for Opus that encodes and decodes audio data using a self-framing format.
/// Each encoded frame is prefixed with a 2-byte little-endian length header, allowing
/// arbitrary-length audio to be encoded and decoded through the <see cref="AudioCodec"/> interface.
/// For direct control over individual Opus packets, use <see cref="OpusEncoder"/> and
/// <see cref="OpusDecoder"/> instead.
/// </summary>
public sealed class OpusCodec : AudioCodec
{
    private const int MaxPacketSize = 1275;

    /// <inheritdoc/>
    public override string Name => "Opus";

    /// <inheritdoc/>
    public override AudioCodecFlags Flags => AudioCodecFlags.SupportsEncoding | AudioCodecFlags.SupportsDecoding;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".opus"];

    /// <summary>
    /// Gets or sets the number of samples per channel per Opus frame. This determines the
    /// granularity of encoding and decoding. The default is 960, which corresponds to 20ms
    /// at 48kHz. Valid values depend on the sample rate (e.g. at 48kHz: 120, 240, 480, 960,
    /// 1920, 2880).
    /// </summary>
    public int FrameSize { get; init; } = 960;

    /// <summary>
    /// Gets or sets the Opus application mode, which influences the encoder's internal
    /// algorithm selection and optimization targets.
    /// </summary>
    public OpusApplication Application { get; init; } = OpusApplication.Audio;

    /// <summary>
    /// Gets or sets the target bitrate in bits per second used when encoding.
    /// </summary>
    public int Bitrate { get; set; } = 64000;

    /// <summary>
    /// Gets or sets the encoder complexity (0 to 10) used when encoding.
    /// Higher values produce better quality at the cost of increased CPU usage.
    /// </summary>
    public int Complexity { get; set; } = 10;

    /// <summary>
    /// Calculates the frame size in samples per channel for the given sample rate and duration.
    /// </summary>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    /// <param name="duration">The desired frame duration.</param>
    /// <returns>The number of samples per channel for one frame.</returns>
    public static int GetFrameSize(int sampleRate, TimeSpan duration)
    {
        return (int)(sampleRate * duration.TotalMilliseconds / 1000.0);
    }

    /// <inheritdoc/>
    public override int GetMaxEncodedSize(int sampleCount, AudioFormat format)
    {
        var frameSamples = FrameSize * format.Channels;

        if (frameSamples <= 0)
            return 0;

        var frames = (sampleCount + frameSamples - 1) / frameSamples;
        return frames * (2 + MaxPacketSize);
    }

    /// <inheritdoc/>
    public override int GetMaxDecodedSize(int encodedSize, AudioFormat format)
    {
        var maxFrames = encodedSize / 3;
        var maxSamplesPerFrame = format.SampleRate * 120 / 1000;
        return maxFrames * maxSamplesPerFrame * format.Channels;
    }

    /// <inheritdoc/>
    public override int Encode(ReadOnlySpan<short> source, Span<byte> destination, AudioFormat format)
    {
        using var encoder = new OpusEncoder(format.SampleRate, format.Channels, Application)
        {
            Bitrate = Bitrate,
            Complexity = Complexity,
        };

        var frameSamples = FrameSize * format.Channels;
        Span<short> padded = stackalloc short[frameSamples];
        var written = 0;
        var pos = 0;

        while (pos < source.Length)
        {
            var remaining = source.Length - pos;
            int advance;
            int encodedSize;

            if (remaining >= frameSamples)
            {
                advance = frameSamples;
                encodedSize = encoder.Encode(source.Slice(pos, frameSamples), FrameSize, destination[(written + 2)..]);
            }
            else
            {
                source.Slice(pos, remaining).CopyTo(padded);
                padded[remaining..].Clear();
                advance = remaining;
                encodedSize = encoder.Encode(padded, FrameSize, destination[(written + 2)..]);
            }

            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(written, 2), (ushort)encodedSize);
            written += 2 + encodedSize;
            pos += advance;
        }

        return written;
    }

    /// <inheritdoc/>
    public override int Decode(ReadOnlySpan<byte> source, Span<short> destination, AudioFormat format)
    {
        using var decoder = new OpusDecoder(format.SampleRate, format.Channels);

        var maxSamplesPerFrame = format.SampleRate * 120 / 1000;
        var written = 0;
        var pos = 0;

        while (pos + 2 <= source.Length)
        {
            var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(pos, 2));
            pos += 2;

            if (pos + frameLength > source.Length)
                break;

            var remainingOutput = (destination.Length - written) / format.Channels;
            var frameSize = Math.Min(maxSamplesPerFrame, remainingOutput);

            if (frameSize <= 0)
                break;

            var decodedPerChannel = decoder.Decode(source.Slice(pos, frameLength), frameSize, destination[written..]);
            written += decodedPerChannel * format.Channels;
            pos += frameLength;
        }

        return written;
    }
}
