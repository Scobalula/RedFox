// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Audio.Flac;

/// <summary>
/// Audio codec for FLAC (Free Lossless Audio Codec).
/// Supports decoding and encoding of FLAC audio data using the native libFLAC library.
/// </summary>
public unsafe sealed class FlacCodec : AudioCodec, IDisposable
{
    private IntPtr _decoder = IntPtr.Zero;
    private IntPtr _encoder = IntPtr.Zero;

    private readonly List<byte> _decodeOutput = new();
    private byte[] _decodeInput = Array.Empty<byte>();
    private int _decodePosition;

    private readonly List<byte> _encodeOutput = new();

    private int _sampleRate;
    private int _channels;
    private int _bitsPerSample;

    /// <inheritdoc/>
    public override string Name => "FLAC";

    /// <inheritdoc/>
    public override AudioCodecFlags Flags => AudioCodecFlags.SupportsEncoding | AudioCodecFlags.SupportsDecoding;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".flac"];

    /// <summary>
    /// Gets or sets the compression level for encoding, ranging from 0 (fastest) to 8 (best compression).
    /// Higher values use more CPU time to achieve better compression. The default is 5.
    /// </summary>
    public int CompressionLevel { get; set; } = 5;

    /// <summary>
    /// Gets or sets the block size in samples per channel.
    /// Larger blocks may achieve better compression but increase latency.
    /// The default is 4608.
    /// </summary>
    public int BlockSize { get; set; } = 4608;

    /// <summary>
    /// Gets or sets whether to verify the encoded output by decoding it and comparing.
    /// This ensures integrity but doubles the encoding time. The default is false.
    /// </summary>
    public bool Verify { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlacCodec"/> class.
    /// </summary>
    public FlacCodec()
    {
        _decoder = FlacInterop.DecoderNew();
        _encoder = FlacInterop.EncoderNew();
    }

    /// <inheritdoc/>
    public override int Decode(ReadOnlySpan<byte> source, Span<short> destination, AudioFormat format)
    {
        if (_decoder == IntPtr.Zero)
            throw new FlacException("FLAC decoder is not available.");

        if (_decodeInput.Length < source.Length)
            _decodeInput = new byte[source.Length];

        source.CopyTo(_decodeInput);
        _decodePosition = 0;
        _decodeOutput.Clear();

        var readCallback = new FlacInterop.ReadCallback(DecoderReadCallback);
        var writeCallback = new FlacInterop.WriteCallback(DecoderWriteCallback);
        var metadataCallback = new FlacInterop.MetadataCallback(DecoderMetadataCallback);
        var errorCallback = new FlacInterop.ErrorCallback(DecoderErrorCallback);

        var status = FlacInterop.DecoderInitStream(
            _decoder,
            readCallback,
            null,
            null,
            null,
            null,
            writeCallback,
            metadataCallback,
            errorCallback,
            IntPtr.Zero);

        if (status != 0)
            throw new FlacException($"Failed to initialize FLAC decoder: {status}.");

        FlacInterop.DecoderProcessUntilEndOfStream(_decoder);
        FlacInterop.DecoderFinish(_decoder);

        var output = _decodeOutput.ToArray();
        var sampleCount = output.Length / 2;
        var samples = new short[sampleCount];

        Buffer.BlockCopy(output, 0, samples, 0, output.Length);

        new ReadOnlySpan<short>(samples).CopyTo(destination);

        return sampleCount;
    }

    /// <inheritdoc/>
    public override int Encode(ReadOnlySpan<short> source, Span<byte> destination, AudioFormat format)
    {
        if (_encoder == IntPtr.Zero)
            throw new FlacException("FLAC encoder is not available.");

        _sampleRate = format.SampleRate;
        _channels = format.Channels;
        _bitsPerSample = format.BitsPerSample > 0 ? format.BitsPerSample : 16;

        if (FlacInterop.EncoderSetVerify(_encoder, Verify ? 1 : 0) != 1)
            throw new FlacException("Failed to set encoder verify flag.");

        if (FlacInterop.EncoderSetStreamableSubset(_encoder, 1) != 1)
            throw new FlacException("Failed to set encoder streamable subset.");

        if (FlacInterop.EncoderSetChannels(_encoder, (uint)_channels) != 1)
            throw new FlacException("Failed to set encoder channels.");

        if (FlacInterop.EncoderSetSampleRate(_encoder, (uint)_sampleRate) != 1)
            throw new FlacException("Failed to set encoder sample rate.");

        if (FlacInterop.EncoderSetBitsPerSample(_encoder, (uint)_bitsPerSample) != 1)
            throw new FlacException("Failed to set encoder bits per sample.");

        if (FlacInterop.EncoderSetCompressionLevel(_encoder, (uint)CompressionLevel) != 1)
            throw new FlacException("Failed to set encoder compression level.");

        if (FlacInterop.EncoderSetBlockSize(_encoder, (uint)BlockSize) != 1)
            throw new FlacException("Failed to set encoder block size.");

        var totalSamples = (ulong)(source.Length / _channels);
        if (FlacInterop.EncoderSetTotalSamplesEstimate(_encoder, totalSamples) != 1)
            throw new FlacException("Failed to set encoder total samples estimate.");

        _encodeOutput.Clear();

        var writeCallback = new FlacInterop.EncoderWriteCallback(EncoderWriteCallback);

        var status = FlacInterop.EncoderInitStream(
            _encoder,
            writeCallback,
            null,
            null,
            null,
            IntPtr.Zero);

        if (status != 0)
            throw new FlacException($"Failed to initialize FLAC encoder: {status}.");

        fixed (short* pSource = source)
        {
            if (FlacInterop.EncoderProcessInterleaved(_encoder, (IntPtr)pSource, (uint)source.Length) != 1)
                throw new FlacException("Failed to encode FLAC data.");
        }

        FlacInterop.EncoderFinish(_encoder);

        _encodeOutput.CopyTo(destination);

        return _encodeOutput.Count;
    }

    /// <inheritdoc/>
    public override int GetMaxDecodedSize(int encodedSize, AudioFormat format)
    {
        return encodedSize * 16;
    }

    /// <inheritdoc/>
    public override int GetMaxEncodedSize(int sampleCount, AudioFormat format)
    {
        var channels = format.Channels > 0 ? format.Channels : 2;
        var bitsPerSample = format.BitsPerSample > 0 ? format.BitsPerSample : 16;
        return sampleCount * 2 * channels;
    }

    private FlacInterop.ReadCallbackStatus DecoderReadCallback(
        IntPtr decoder,
        byte* buffer,
        int* bytes,
        IntPtr clientData)
    {
        var remaining = _decodeInput.Length - _decodePosition;
        var toRead = Math.Min(*bytes, remaining);

        if (toRead > 0)
        {
            new ReadOnlySpan<byte>(_decodeInput, _decodePosition, toRead).CopyTo(new Span<byte>(buffer, toRead));
            _decodePosition += toRead;
        }

        *bytes = toRead;

        return FlacInterop.ReadCallbackStatus.ReadContinue;
    }

    private int DecoderWriteCallback(
        IntPtr decoder,
        IntPtr* frame,
        IntPtr buffer,
        IntPtr clientData)
    {
        var framePtr = frame[0];

        var blockSize = *(int*)(framePtr + 4);
        var channels = *(int*)(framePtr + 8);
        var bitsPerSample = *(int*)(framePtr + 12);

        var samplesPerChannel = blockSize;
        var totalSamples = samplesPerChannel * channels;

        var bufferData = (int**)buffer;

        for (var sample = 0; sample < samplesPerChannel; sample++)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                var value = (short)bufferData[ch][sample];

                _decodeOutput.AddRange(BitConverter.GetBytes(value));
            }
        }

        return 1;
    }

    private void DecoderMetadataCallback(IntPtr decoder, IntPtr metadata, IntPtr clientData)
    {
    }

    private void DecoderErrorCallback(IntPtr decoder, int status, IntPtr clientData)
    {
        throw new FlacException($"FLAC decoder error: {status}.");
    }

    private FlacInterop.EncoderWriteStatus EncoderWriteCallback(
        IntPtr encoder,
        byte* buffer,
        int bytes,
        int samples,
        int currentFrame,
        IntPtr clientData)
    {
        _encodeOutput.AddRange(new Span<byte>(buffer, bytes).ToArray());

        return FlacInterop.EncoderWriteStatus.WriteOk;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_decoder != IntPtr.Zero)
        {
            FlacInterop.DecoderDelete(_decoder);
            _decoder = IntPtr.Zero;
        }

        if (_encoder != IntPtr.Zero)
        {
            FlacInterop.EncoderDelete(_encoder);
            _encoder = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="FlacCodec"/> class.
    /// </summary>
    ~FlacCodec()
    {
        Dispose();
    }
}
