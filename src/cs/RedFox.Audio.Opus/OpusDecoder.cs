// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Opus;

/// <summary>
/// Encapsulates a native Opus decoder state, providing frame-by-frame decoding of
/// compressed Opus packets into 16-bit PCM audio. This class is not thread-safe;
/// use separate instances per thread.
/// </summary>
public sealed class OpusDecoder : IDisposable
{
    private IntPtr _state;

    /// <summary>
    /// Gets the sample rate in Hz used by the decoder (8000, 12000, 16000, 24000, or 48000).
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2 for stereo).
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets the maximum number of samples per channel the decoder can produce per frame,
    /// based on the configured sample rate (120ms duration).
    /// </summary>
    public int MaxFrameSize => SampleRate * 120 / 1000;

    /// <summary>
    /// Gets the duration of the last successfully decoded packet in samples per channel.
    /// </summary>
    public int LastPacketDuration
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.DecoderCtlGetInt(_state, OpusInterop.GetLastPacketDurationRequest, out var value);
            return value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpusDecoder"/> class with the specified parameters.
    /// </summary>
    /// <param name="sampleRate">The sample rate in Hz. Must be 8000, 12000, 16000, 24000, or 48000.</param>
    /// <param name="channels">The number of audio channels (1 for mono, 2 for stereo).</param>
    /// <exception cref="AudioException">Thrown when the native decoder cannot be created.</exception>
    public OpusDecoder(int sampleRate, int channels)
    {
        _state = OpusInterop.DecoderCreate(sampleRate, channels, out var error);

        if (error != OpusInterop.Ok)
            throw new AudioException($"Failed to create Opus decoder: {OpusInterop.StrError(error)}");

        SampleRate = sampleRate;
        Channels = channels;
    }

    /// <summary>
    /// Decodes a single Opus packet into interleaved 16-bit PCM samples.
    /// </summary>
    /// <param name="data">The compressed Opus packet data to decode.</param>
    /// <param name="frameSize">
    /// The maximum number of samples per channel that can be written to <paramref name="output"/>.
    /// Must be at least as large as the actual decoded frame size.
    /// </param>
    /// <param name="output">The buffer to receive the decoded interleaved 16-bit PCM samples.</param>
    /// <returns>The number of decoded samples per channel written to <paramref name="output"/>.</returns>
    /// <exception cref="AudioException">Thrown when decoding fails.</exception>
    public int Decode(ReadOnlySpan<byte> data, int frameSize, Span<short> output)
    {
        ThrowIfDisposed();
        var result = OpusInterop.Decode(_state, data, data.Length, output, frameSize, 0);

        if (result < 0)
            throw new AudioException($"Failed to decode Opus packet: {OpusInterop.StrError(result)}");

        return result;
    }

    /// <summary>
    /// Generates packet loss concealment (PLC) data for a missing frame by synthesizing
    /// audio from the decoder's internal state. This should be called when a packet is
    /// lost or missing in the stream.
    /// </summary>
    /// <param name="frameSize">The number of samples per channel to generate.</param>
    /// <param name="output">The buffer to receive the synthesized interleaved 16-bit PCM samples.</param>
    /// <returns>The number of samples per channel written to <paramref name="output"/>.</returns>
    /// <exception cref="AudioException">Thrown when PLC generation fails.</exception>
    public int DecodePacketLoss(int frameSize, Span<short> output)
    {
        ThrowIfDisposed();
        var result = OpusInterop.Decode(_state, ReadOnlySpan<byte>.Empty, 0, output, frameSize, 0);

        if (result < 0)
            throw new AudioException($"Failed to generate PLC data: {OpusInterop.StrError(result)}");

        return result;
    }

    /// <summary>
    /// Releases the native Opus decoder resources.
    /// </summary>
    public void Dispose()
    {
        if (_state == IntPtr.Zero)
            return;

        OpusInterop.DecoderDestroy(_state);
        _state = IntPtr.Zero;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_state == IntPtr.Zero, this);
    }
}
