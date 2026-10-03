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

internal sealed unsafe class FlacDecoder : AudioDecoder
{
    private readonly EncodedAudio _encoded;
    private readonly int _bitsPerSample;
    private readonly int _shift;
    private GCHandle _handle;
    private IntPtr _decoder;
    private byte[] _frame;
    private int _frameOffset;
    private int _frameLength;
    private int _inputPosition;
    private long _position;
    private Exception? _error;

    public override AudioFormat Format => _encoded.Format;

    public override SampleFormat SampleFormat { get; }

    public override long FrameCount { get; }

    public override long Position => _position;

    public override int ValidBitsPerSample => _bitsPerSample;

    public override bool CanSeek => true;

    private int InputLength => _encoded.Setup.Length + _encoded.Data.Length;

    public FlacDecoder(EncodedAudio encoded)
    {
        FlacStreamInfo info = FlacMetadata.Parse(encoded.Setup).StreamInfo;

        if (info.Channels != encoded.Format.Channels)
            throw new AudioException($"The FLAC stream declares {info.Channels} channels, but the audio has {encoded.Format.Channels}.");

        _encoded = encoded;
        _bitsPerSample = info.BitsPerSample;
        SampleFormat = FlacCodec.GetSampleFormat(info.BitsPerSample);
        FrameCount = info.TotalSamples > 0 ? info.TotalSamples : encoded.FrameCount;
        _shift = SampleFormatInfo.GetBitsPerSample(SampleFormat) - info.BitsPerSample;
        _frame = new byte[Math.Max(info.MaxBlockSize, 1) * BytesPerFrame];
        _handle = GCHandle.Alloc(this);
        _decoder = FlacInterop.DecoderNew();

        if (_decoder == IntPtr.Zero)
            throw new AudioException("Failed to create the FLAC decoder.");

        int status = FlacInterop.DecoderInitStream(_decoder, &ReadCallback, &SeekCallback, &TellCallback, &LengthCallback, &EofCallback, &WriteCallback, null, &ErrorCallback, GCHandle.ToIntPtr(_handle));

        if (status != FlacInterop.Ok)
        {
            Dispose();
            throw new AudioException($"Failed to initialize the FLAC decoder (status {status}).");
        }
    }

    public override int Read(Span<byte> destination)
    {
        if (_position == FrameCount)
            return 0;

        int capacity = destination.Length / BytesPerFrame * BytesPerFrame;
        int written = 0;

        while (written < capacity)
        {
            if (_frameOffset == _frameLength && !DecodeNextFrame())
                break;

            int count = Math.Min(capacity - written, _frameLength - _frameOffset);

            _frame.AsSpan(_frameOffset, count).CopyTo(destination[written..]);
            _frameOffset += count;
            written += count;
        }

        int frames = written / BytesPerFrame;
        _position += frames;

        return frames;
    }

    public override void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);

        if (FrameCount >= 0)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, FrameCount);

        _frameOffset = 0;
        _frameLength = 0;
        _position = frame;

        if (frame == FrameCount)
            return;

        int succeeded = FlacInterop.DecoderSeekAbsolute(_decoder, (ulong)frame);

        if (succeeded == 0)
        {
            string state = FlacInterop.GetDecoderState(_decoder);
            FlacInterop.DecoderFlush(_decoder);
            throw new AudioException($"Failed to seek the FLAC stream to frame {frame}: {state}");
        }

        ThrowIfFailed(succeeded);
    }

    public override void Dispose()
    {
        if (_decoder != IntPtr.Zero)
        {
            FlacInterop.DecoderDelete(_decoder);
            _decoder = IntPtr.Zero;
        }

        if (_handle.IsAllocated)
            _handle.Free();

        base.Dispose();
    }

    private bool DecodeNextFrame()
    {
        _frameOffset = 0;
        _frameLength = 0;

        while (_frameLength == 0)
        {
            if (FlacInterop.DecoderGetState(_decoder) == FlacInterop.DecoderEndOfStream)
                return false;

            ThrowIfFailed(FlacInterop.DecoderProcessSingle(_decoder));
        }

        return true;
    }

    private void ThrowIfFailed(int succeeded)
    {
        if (_error is { } error)
        {
            _error = null;
            throw error as AudioException ?? new AudioException("Failed to decode FLAC audio.", error);
        }

        if (succeeded == 0)
            throw new AudioException($"Failed to decode FLAC audio: {FlacInterop.GetDecoderState(_decoder)}");
    }

    private void WriteFrame(int blockSize, int** channels)
    {
        int channelCount = Format.Channels;
        int length = blockSize * BytesPerFrame;

        if (_frame.Length < length)
            _frame = new byte[length];

        Span<byte> destination = _frame.AsSpan(0, length);

        for (int frame = 0; frame < blockSize; frame++)
        {
            for (int channel = 0; channel < channelCount; channel++)
                WriteSample(destination, (frame * channelCount) + channel, channels[channel][frame] << _shift);
        }

        _frameOffset = 0;
        _frameLength = length;
    }

    private void WriteSample(Span<byte> destination, int index, int value)
    {
        switch (SampleFormat)
        {
            case SampleFormat.UInt8:
                destination[index] = (byte)(value + 128);
                break;
            case SampleFormat.Int16:
                MemoryMarshal.Write(destination[(index * 2)..], (short)value);
                break;
            case SampleFormat.Int24:
                destination[index * 3] = (byte)value;
                destination[(index * 3) + 1] = (byte)(value >> 8);
                destination[(index * 3) + 2] = (byte)(value >> 16);
                break;
            default:
                MemoryMarshal.Write(destination[(index * 4)..], value);
                break;
        }
    }

    private static FlacDecoder FromClientData(IntPtr clientData) => (FlacDecoder)GCHandle.FromIntPtr(clientData).Target!;

    [UnmanagedCallersOnly]
    private static int ReadCallback(IntPtr decoder, byte* buffer, nuint* bytes, IntPtr clientData)
    {
        FlacDecoder self = FromClientData(clientData);
        Span<byte> destination = new(buffer, (int)Math.Min(*bytes, int.MaxValue));
        int setupLength = self._encoded.Setup.Length;
        int read = 0;

        while (read < destination.Length && self._inputPosition < self.InputLength)
        {
            ReadOnlySpan<byte> source = self._inputPosition < setupLength ? self._encoded.Setup.Span[self._inputPosition..] : self._encoded.Data.Span[(self._inputPosition - setupLength)..];
            int count = Math.Min(source.Length, destination.Length - read);

            source[..count].CopyTo(destination[read..]);
            self._inputPosition += count;
            read += count;
        }

        *bytes = (nuint)read;
        return read == 0 ? FlacInterop.ReadEndOfStream : FlacInterop.Ok;
    }

    [UnmanagedCallersOnly]
    private static int SeekCallback(IntPtr decoder, ulong offset, IntPtr clientData)
    {
        FlacDecoder self = FromClientData(clientData);

        if (offset > (ulong)self.InputLength)
            return 1;

        self._inputPosition = (int)offset;
        return FlacInterop.Ok;
    }

    [UnmanagedCallersOnly]
    private static int TellCallback(IntPtr decoder, ulong* offset, IntPtr clientData)
    {
        *offset = (ulong)FromClientData(clientData)._inputPosition;
        return FlacInterop.Ok;
    }

    [UnmanagedCallersOnly]
    private static int LengthCallback(IntPtr decoder, ulong* length, IntPtr clientData)
    {
        *length = (ulong)FromClientData(clientData).InputLength;
        return FlacInterop.Ok;
    }

    [UnmanagedCallersOnly]
    private static int EofCallback(IntPtr decoder, IntPtr clientData)
    {
        FlacDecoder self = FromClientData(clientData);
        return self._inputPosition >= self.InputLength ? 1 : 0;
    }

    [UnmanagedCallersOnly]
    private static int WriteCallback(IntPtr decoder, IntPtr frame, int** buffer, IntPtr clientData)
    {
        FlacDecoder self = FromClientData(clientData);

        try
        {
            self.WriteFrame(*(int*)frame, buffer);
            return FlacInterop.Ok;
        }
        catch (Exception exception)
        {
            self._error ??= exception;
            return FlacInterop.WriteAbort;
        }
    }

    [UnmanagedCallersOnly]
    private static void ErrorCallback(IntPtr decoder, int status, IntPtr clientData)
    {
        FromClientData(clientData)._error ??= new AudioException($"The FLAC stream is corrupt (libFLAC error status {status}).");
    }
}
