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
/// Encapsulates a native Opus encoder state, providing frame-by-frame encoding of
/// 16-bit PCM audio into compressed Opus packets. This class is not thread-safe;
/// use separate instances per thread.
/// </summary>
public sealed class OpusEncoder : IDisposable
{
    private IntPtr _state;

    /// <summary>
    /// Gets the sample rate in Hz used by the encoder (8000, 12000, 16000, 24000, or 48000).
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2 for stereo).
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets the application mode the encoder was created with.
    /// </summary>
    public OpusApplication Application { get; }

    /// <summary>
    /// Gets the maximum number of samples per channel the encoder can produce per frame,
    /// based on the configured sample rate (120ms duration).
    /// </summary>
    public int MaxFrameSize => SampleRate * 120 / 1000;

    /// <summary>
    /// Gets or sets the target bitrate in bits per second. A value of <c>-1</c> indicates
    /// the default bitrate, and <c>0</c> or a positive value sets a specific target.
    /// Valid range: 500 to 512000 bits per second.
    /// </summary>
    /// <exception cref="AudioException">Thrown when the bitrate cannot be set on the native encoder.</exception>
    public int Bitrate
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.EncoderCtlGetInt(_state, OpusInterop.GetBitrateRequest, out var value);
            return value;
        }
        set
        {
            ThrowIfDisposed();
            var result = OpusInterop.EncoderCtlSet(_state, OpusInterop.SetBitrateRequest, value);

            if (result < 0)
                throw new AudioException($"Failed to set Opus bitrate: {OpusInterop.StrError(result)}");
        }
    }

    /// <summary>
    /// Gets or sets the encoder complexity, ranging from 0 (lowest complexity, fastest)
    /// to 10 (highest complexity, best quality). Higher values increase CPU usage.
    /// </summary>
    /// <exception cref="AudioException">Thrown when the complexity cannot be set on the native encoder.</exception>
    public int Complexity
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.EncoderCtlGetInt(_state, OpusInterop.GetComplexityRequest, out var value);
            return value;
        }
        set
        {
            ThrowIfDisposed();
            var result = OpusInterop.EncoderCtlSet(_state, OpusInterop.SetComplexityRequest, value);

            if (result < 0)
                throw new AudioException($"Failed to set Opus complexity: {OpusInterop.StrError(result)}");
        }
    }

    /// <summary>
    /// Gets or sets whether variable bitrate encoding is enabled. When enabled, the encoder
    /// dynamically adjusts the bitrate based on the complexity of the audio content.
    /// </summary>
    /// <exception cref="AudioException">Thrown when VBR mode cannot be set on the native encoder.</exception>
    public bool Vbr
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.EncoderCtlGetInt(_state, OpusInterop.GetVbrRequest, out var value);
            return value != 0;
        }
        set
        {
            ThrowIfDisposed();
            var result = OpusInterop.EncoderCtlSet(_state, OpusInterop.SetVbrRequest, value ? 1 : 0);

            if (result < 0)
                throw new AudioException($"Failed to set Opus VBR: {OpusInterop.StrError(result)}");
        }
    }

    /// <summary>
    /// Gets or sets whether Discontinuous Transmission (DTX) is enabled. When enabled,
    /// the encoder produces significantly smaller packets during periods of silence.
    /// </summary>
    /// <exception cref="AudioException">Thrown when DTX mode cannot be set on the native encoder.</exception>
    public bool Dtx
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.EncoderCtlGetInt(_state, OpusInterop.GetDtxRequest, out var value);
            return value != 0;
        }
        set
        {
            ThrowIfDisposed();
            var result = OpusInterop.EncoderCtlSet(_state, OpusInterop.SetDtxRequest, value ? 1 : 0);

            if (result < 0)
                throw new AudioException($"Failed to set Opus DTX: {OpusInterop.StrError(result)}");
        }
    }

    /// <summary>
    /// Gets or sets the estimated packet loss percentage the encoder should compensate for.
    /// Higher values cause the encoder to add more forward error correction data. Range: 0 to 100.
    /// </summary>
    /// <exception cref="AudioException">Thrown when packet loss percentage cannot be set on the native encoder.</exception>
    public int PacketLossPercentage
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.EncoderCtlGetInt(_state, OpusInterop.GetPacketLossPercRequest, out var value);
            return value;
        }
        set
        {
            ThrowIfDisposed();
            var result = OpusInterop.EncoderCtlSet(_state, OpusInterop.SetPacketLossPercRequest, value);

            if (result < 0)
                throw new AudioException($"Failed to set Opus packet loss percentage: {OpusInterop.StrError(result)}");
        }
    }

    /// <summary>
    /// Gets the encoder lookahead in samples per channel. This is the number of samples
    /// the encoder needs to see ahead of the current frame for optimal quality.
    /// </summary>
    public int Lookahead
    {
        get
        {
            ThrowIfDisposed();
            OpusInterop.EncoderCtlGetInt(_state, OpusInterop.GetLookaheadRequest, out var value);
            return value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpusEncoder"/> class with the specified parameters.
    /// </summary>
    /// <param name="sampleRate">The sample rate in Hz. Must be 8000, 12000, 16000, 24000, or 48000.</param>
    /// <param name="channels">The number of audio channels (1 for mono, 2 for stereo).</param>
    /// <param name="application">The intended application mode for the encoder.</param>
    /// <exception cref="AudioException">Thrown when the native encoder cannot be created.</exception>
    public OpusEncoder(int sampleRate, int channels, OpusApplication application)
    {
        _state = OpusInterop.EncoderCreate(sampleRate, channels, (int)application, out var error);

        if (error != OpusInterop.Ok)
            throw new AudioException($"Failed to create Opus encoder: {OpusInterop.StrError(error)}");

        SampleRate = sampleRate;
        Channels = channels;
        Application = application;
    }

    /// <summary>
    /// Encodes a single frame of interleaved 16-bit PCM samples into an Opus packet.
    /// </summary>
    /// <param name="pcm">
    /// The interleaved 16-bit PCM samples to encode. Must contain exactly
    /// <paramref name="frameSize"/> * <see cref="Channels"/> samples.
    /// </param>
    /// <param name="frameSize">The number of samples per channel in the input frame.</param>
    /// <param name="output">The buffer to receive the encoded Opus packet bytes.</param>
    /// <returns>The number of bytes written to <paramref name="output"/>.</returns>
    /// <exception cref="AudioException">Thrown when encoding fails.</exception>
    public int Encode(ReadOnlySpan<short> pcm, int frameSize, Span<byte> output)
    {
        ThrowIfDisposed();
        var result = OpusInterop.Encode(_state, pcm, frameSize, output, output.Length);

        if (result < 0)
            throw new AudioException($"Failed to encode Opus frame: {OpusInterop.StrError(result)}");

        return result;
    }

    /// <summary>
    /// Releases the native Opus encoder resources.
    /// </summary>
    public void Dispose()
    {
        if (_state == IntPtr.Zero)
            return;

        OpusInterop.EncoderDestroy(_state);
        _state = IntPtr.Zero;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_state == IntPtr.Zero, this);
    }
}
