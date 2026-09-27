// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;
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

    private readonly FlacInterop.ReadCallback _readCallback;
    private readonly FlacInterop.WriteCallback _writeCallback;
    private readonly FlacInterop.MetadataCallback _metadataCallback;
    private readonly FlacInterop.ErrorCallback _errorCallback;
    private readonly FlacInterop.EncoderWriteCallback _encoderWriteCallback;

    private byte[] _decodeInput = Array.Empty<byte>();
    private int _decodeLength;
    private int _decodePosition;
    private short* _decodeDestination;
    private int _decodeCapacity;
    private int _decodeWritePosition;
    private int _decodeExpectedChannels;
    private Exception? _callbackException;
    private int _decoderError;

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
        _readCallback = DecoderReadCallback;
        _writeCallback = DecoderWriteCallback;
        _metadataCallback = DecoderMetadataCallback;
        _errorCallback = DecoderErrorCallback;
        _encoderWriteCallback = EncoderWriteCallback;
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
        _decodeLength = source.Length;
        _decodePosition = 0;
        _decodeCapacity = destination.Length;
        _decodeWritePosition = 0;
        _decodeExpectedChannels = format.Channels;
        _callbackException = null;
        _decoderError = 0;

        var status = FlacInterop.DecoderInitStream(_decoder, _readCallback, null, null, null, null, _writeCallback, _metadataCallback, _errorCallback, IntPtr.Zero);

        if (status != 0)
            throw new FlacException($"Failed to initialize FLAC decoder: {status}.");

        int processResult;
        int finishResult;
        fixed (short* destinationPointer = destination)
        {
            _decodeDestination = destinationPointer;
            try
            {
                processResult = FlacInterop.DecoderProcessUntilEndOfStream(_decoder);
            }
            finally
            {
                _decodeDestination = null;
            }

            finishResult = FlacInterop.DecoderFinish(_decoder);
        }

        if (_callbackException is not null)
            throw new FlacException("A FLAC callback failed.", _callbackException);
        if (_decoderError != 0 || processResult != 1 || finishResult != 1)
            throw new FlacException("Failed to decode the FLAC stream.");

        return _decodeWritePosition;
    }

    /// <inheritdoc/>
    public override int Encode(ReadOnlySpan<short> source, Span<byte> destination, AudioFormat format)
    {
        if (_encoder == IntPtr.Zero)
            throw new FlacException("FLAC encoder is not available.");

        _sampleRate = format.SampleRate;
        _channels = format.Channels;
        _bitsPerSample = format.BitsPerSample > 0 ? format.BitsPerSample : 16;

        if (_channels is < 1 or > 8 || _sampleRate <= 0 || _bitsPerSample is < 4 or > 32)
            throw new FlacException("The FLAC audio format is invalid.");
        if (CompressionLevel is < 0 or > 8 || BlockSize is < 16 or > 65535)
            throw new FlacException("The FLAC encoder settings are invalid.");
        if (source.Length % _channels != 0)
            throw new FlacException("The interleaved FLAC sample count is invalid.");

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

        _callbackException = null;

        var status = FlacInterop.EncoderInitStream(_encoder, _encoderWriteCallback, null, null, null, IntPtr.Zero);

        if (status != 0)
            throw new FlacException($"Failed to initialize FLAC encoder: {status}.");

        int[] encoderSamples = new int[source.Length];
        int shift = _bitsPerSample - 16;
        for (int sampleIndex = 0; sampleIndex < source.Length; sampleIndex++)
            encoderSamples[sampleIndex] = shift > 0 ? source[sampleIndex] << shift : shift < 0 ? source[sampleIndex] >> -shift : source[sampleIndex];

        int processResult;
        int finishResult;
        try
        {
            fixed (int* pSource = encoderSamples)
                processResult = FlacInterop.EncoderProcessInterleaved(_encoder, (IntPtr)pSource, (uint)(source.Length / _channels));
        }
        finally
        {
            finishResult = FlacInterop.EncoderFinish(_encoder);
        }

        if (_callbackException is not null)
            throw new FlacException("The FLAC encoder write callback failed.", _callbackException);
        if (processResult != 1 || finishResult != 1)
            throw new FlacException("Failed to finish FLAC encoding.");

        _encodeOutput.CopyTo(destination);

        return _encodeOutput.Count;
    }

    /// <inheritdoc/>
    public override int GetMaxDecodedSize(int encodedSize, AudioFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(encodedSize);
        int channels = format.Channels > 0 ? format.Channels : 2;
        return checked(encodedSize * 16 * channels);
    }

    /// <inheritdoc/>
    public override int GetMaxDecodedSize(ReadOnlySpan<byte> encodedData, AudioFormat format)
    {
        if (encodedData.Length < 42 || !encodedData[..4].SequenceEqual("fLaC"u8))
            return GetMaxDecodedSize(encodedData.Length, format);

        int offset = 4;
        while (offset <= encodedData.Length - 4)
        {
            byte blockHeader = encodedData[offset];
            int blockType = blockHeader & 0x7F;
            int blockLength = (encodedData[offset + 1] << 16) | (encodedData[offset + 2] << 8) | encodedData[offset + 3];
            offset += 4;
            if (blockLength > encodedData.Length - offset)
                throw new InvalidDataException("The FLAC metadata block is truncated.");

            if (blockType == 0)
            {
                if (blockLength != 34)
                    throw new InvalidDataException("The FLAC STREAMINFO block has an invalid size.");

                ReadOnlySpan<byte> streamInfo = encodedData.Slice(offset, blockLength);
                int channels = ((streamInfo[12] >> 1) & 0x07) + 1;
                ulong sampleCount = BinaryPrimitives.ReadUInt64BigEndian(streamInfo[10..]) & 0xFFFFFFFFFUL;
                if (format.Channels > 0 && channels != format.Channels)
                    throw new InvalidDataException("The FLAC stream channel count does not match the requested audio format.");
                if (sampleCount == 0)
                    return GetMaxDecodedSize(encodedData.Length, format);
                if (sampleCount > (ulong)(int.MaxValue / channels))
                    throw new InvalidDataException("The FLAC decoded sample count exceeds the supported buffer size.");

                return checked((int)sampleCount * channels);
            }

            offset = checked(offset + blockLength);
            if ((blockHeader & 0x80) != 0)
                break;
        }

        throw new InvalidDataException("The FLAC stream does not contain a STREAMINFO block.");
    }

    /// <inheritdoc/>
    public override int GetMaxEncodedSize(int sampleCount, AudioFormat format)
    {
        var channels = format.Channels > 0 ? format.Channels : 2;
        var bitsPerSample = format.BitsPerSample > 0 ? format.BitsPerSample : 16;
        return sampleCount * 2 * channels;
    }

    private FlacInterop.ReadCallbackStatus DecoderReadCallback(IntPtr decoder, byte* buffer, int* bytes, IntPtr clientData)
    {
        if (bytes is null || *bytes <= 0)
        {
            _callbackException = new FlacException("The FLAC decoder requested an invalid input size.");
            return FlacInterop.ReadCallbackStatus.ReadAbort;
        }

        var remaining = _decodeLength - _decodePosition;
        var toRead = Math.Min(*bytes, remaining);

        if (toRead > 0)
        {
            new ReadOnlySpan<byte>(_decodeInput, _decodePosition, toRead).CopyTo(new Span<byte>(buffer, toRead));
            _decodePosition += toRead;
        }

        *bytes = toRead;
        return toRead == 0 ? FlacInterop.ReadCallbackStatus.ReadEndOfStream : FlacInterop.ReadCallbackStatus.ReadContinue;
    }

    private int DecoderWriteCallback(IntPtr decoder, IntPtr frame, IntPtr buffer, IntPtr clientData)
    {
        try
        {
            if (frame == IntPtr.Zero || buffer == IntPtr.Zero)
                throw new FlacException("The FLAC decoder returned an invalid frame.");

            byte* framePointer = (byte*)frame;
            int blockSize = checked((int)*(uint*)framePointer);
            int channels = checked((int)*(uint*)(framePointer + 8));
            int bitsPerSample = checked((int)*(uint*)(framePointer + 16));
            if (blockSize <= 0 || channels is < 1 or > 8 || (_decodeExpectedChannels > 0 && channels != _decodeExpectedChannels) || bitsPerSample is < 1 or > 32)
                throw new FlacException("The FLAC frame contains invalid metadata.");

            int totalSamples = checked(blockSize * channels);
            if (_decodeDestination is null || totalSamples > _decodeCapacity - _decodeWritePosition)
                throw new FlacException("The FLAC frame exceeds the output buffer or contains invalid metadata.");

            int** channelData = (int**)buffer;
            int sampleShift = bitsPerSample - 16;
            short* output = _decodeDestination + _decodeWritePosition;
            for (int sample = 0; sample < blockSize; sample++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    int value = channelData[channel][sample];
                    value = sampleShift > 0 ? value >> sampleShift : sampleShift < 0 ? value << -sampleShift : value;
                    output[sample * channels + channel] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
                }
            }

            _decodeWritePosition += totalSamples;
            return 0;
        }
        catch (Exception exception)
        {
            _callbackException = exception;
            return 1;
        }
    }

    private void DecoderMetadataCallback(IntPtr decoder, IntPtr metadata, IntPtr clientData)
    {
    }

    private void DecoderErrorCallback(IntPtr decoder, int status, IntPtr clientData)
    {
        _decoderError = status;
    }

    private FlacInterop.EncoderWriteStatus EncoderWriteCallback(IntPtr encoder, byte* buffer, int bytes, int samples, int currentFrame, IntPtr clientData)
    {
        try
        {
            _encodeOutput.EnsureCapacity(checked(_encodeOutput.Count + bytes));
            for (int i = 0; i < bytes; i++)
                _encodeOutput.Add(buffer[i]);
            return FlacInterop.EncoderWriteStatus.WriteOk;
        }
        catch (Exception exception)
        {
            _callbackException = exception;
            return FlacInterop.EncoderWriteStatus.WriteFatalError;
        }
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
