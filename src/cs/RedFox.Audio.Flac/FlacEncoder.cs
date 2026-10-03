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

internal sealed unsafe class FlacEncoder : AudioEncoder
{
    private const int MaxChannels = 8;

    private const int MinBitsPerSample = 4;

    private const int MaxBitsPerSample = 32;

    private const int StreamInfoDataOffset = 16;

    private readonly MemoryStream _header = new();
    private readonly int _bitsPerSample;
    private readonly int _shift;
    private GCHandle _handle;
    private IntPtr _encoder;
    private IAudioPacketWriter? _output;
    private int[] _samples = [];
    private long _totalSamples;
    private Exception? _error;

    public override AudioFormat Format { get; }

    public override SampleFormat InputSampleFormat { get; }

    public override int BitsPerSample => _bitsPerSample;

    public override ReadOnlyMemory<byte> Setup => _header.GetBuffer().AsMemory(0, (int)_header.Length);

    public FlacEncoder(AudioFormat format, int bitsPerSample, int compressionLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(format.SampleRate, nameof(format));

        if (format.Channels is < 1 or > MaxChannels)
            throw new AudioException($"FLAC supports 1 to {MaxChannels} channels, but the audio has {format.Channels}.");

        if (bitsPerSample is < MinBitsPerSample or > MaxBitsPerSample)
            throw new AudioException($"FLAC supports {MinBitsPerSample} to {MaxBitsPerSample} bits per sample, but {bitsPerSample} was requested.");

        Format = format;
        InputSampleFormat = FlacCodec.GetSampleFormat(bitsPerSample);
        _bitsPerSample = bitsPerSample;
        _shift = SampleFormatInfo.GetBitsPerSample(InputSampleFormat) - bitsPerSample;
        _handle = GCHandle.Alloc(this);
        _encoder = FlacInterop.EncoderNew();

        if (_encoder == IntPtr.Zero)
            throw new AudioException("Failed to create the FLAC encoder.");

        FlacInterop.EncoderSetStreamableSubset(_encoder, 0);
        FlacInterop.EncoderSetChannels(_encoder, (uint)format.Channels);
        FlacInterop.EncoderSetBitsPerSample(_encoder, (uint)bitsPerSample);
        FlacInterop.EncoderSetSampleRate(_encoder, (uint)format.SampleRate);
        FlacInterop.EncoderSetCompressionLevel(_encoder, (uint)compressionLevel);

        int status = FlacInterop.EncoderInitStream(_encoder, &WriteCallback, null, null, &MetadataCallback, GCHandle.ToIntPtr(_handle));

        if (status != FlacInterop.Ok)
        {
            string state = FlacInterop.GetEncoderState(_encoder);
            Dispose();
            throw new AudioException($"Failed to initialize the FLAC encoder: {state}");
        }
    }

    public override void Encode(ReadOnlySpan<byte> frames, IAudioPacketWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        int count = frames.Length / SampleFormatInfo.GetBytesPerSample(InputSampleFormat);

        if (_samples.Length < count)
            _samples = new int[count];

        for (int i = 0; i < count; i++)
            _samples[i] = ReadSample(frames, i) >> _shift;

        int succeeded;
        _output = output;

        try
        {
            fixed (int* samples = _samples)
                succeeded = FlacInterop.EncoderProcessInterleaved(_encoder, samples, (uint)(count / Format.Channels));
        }
        finally
        {
            _output = null;
        }

        ThrowIfFailed(succeeded);
    }

    public override void Complete(IAudioPacketWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        int succeeded;
        _output = output;

        try
        {
            succeeded = FlacInterop.EncoderFinish(_encoder);
        }
        finally
        {
            _output = null;
        }

        ThrowIfFailed(succeeded);
    }

    public override void Dispose()
    {
        if (_encoder != IntPtr.Zero)
        {
            FlacInterop.EncoderDelete(_encoder);
            _encoder = IntPtr.Zero;
        }

        if (_handle.IsAllocated)
            _handle.Free();

        base.Dispose();
    }

    private void ThrowIfFailed(int succeeded)
    {
        if (_error is { } error)
        {
            _error = null;
            throw new AudioException("Failed to encode FLAC audio.", error);
        }

        if (succeeded == 0)
            throw new AudioException($"Failed to encode FLAC audio: {FlacInterop.GetEncoderState(_encoder)}");
    }

    private int ReadSample(ReadOnlySpan<byte> frames, int index) => InputSampleFormat switch
    {
        SampleFormat.UInt8 => frames[index] - 128,
        SampleFormat.Int16 => MemoryMarshal.Read<short>(frames[(index * 2)..]),
        SampleFormat.Int24 => frames[index * 3] | (frames[(index * 3) + 1] << 8) | ((sbyte)frames[(index * 3) + 2] << 16),
        _ => MemoryMarshal.Read<int>(frames[(index * 4)..]),
    };

    private void WriteOutput(ReadOnlySpan<byte> data, int samples)
    {
        if (samples == 0 && _totalSamples == 0)
        {
            _header.Write(data);
            return;
        }

        _totalSamples += samples;
        _output?.WritePacket(data, samples);
    }

    private void UpdateStreamInfo(byte* metadata)
    {
        if (*(int*)metadata != FlacInterop.StreamInfoType)
            return;

        uint* fields = (uint*)(metadata + StreamInfoDataOffset);
        FlacStreamInfo info = new((int)fields[0], (int)fields[1], (int)fields[2], (int)fields[3], (int)fields[4], (int)fields[5], (int)fields[6], _totalSamples, BinaryPrimitives.ReadUInt128BigEndian(new ReadOnlySpan<byte>(fields + 10, 16)));

        info.Write(_header.GetBuffer().AsSpan(FlacMetadata.StreamInfoOffset, FlacStreamInfo.Size));
    }

    private static FlacEncoder FromClientData(IntPtr clientData) => (FlacEncoder)GCHandle.FromIntPtr(clientData).Target!;

    [UnmanagedCallersOnly]
    private static int WriteCallback(IntPtr encoder, byte* buffer, nuint bytes, uint samples, uint currentFrame, IntPtr clientData)
    {
        FlacEncoder self = FromClientData(clientData);

        try
        {
            self.WriteOutput(new ReadOnlySpan<byte>(buffer, (int)bytes), (int)samples);
            return FlacInterop.Ok;
        }
        catch (Exception exception)
        {
            self._error ??= exception;
            return FlacInterop.WriteAbort;
        }
    }

    [UnmanagedCallersOnly]
    private static void MetadataCallback(IntPtr encoder, IntPtr metadata, IntPtr clientData)
    {
        FromClientData(clientData).UpdateStreamInfo((byte*)metadata);
    }
}
