// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Audio.Opus;

internal sealed class OpusDecoder : AudioDecoder
{
    private const int MaxPacketMilliseconds = 120;
    private const int PreRollMilliseconds = 200;

    private static readonly byte[][] VorbisToWaveOrder =
    [
        [0],
        [0, 1],
        [0, 2, 1],
        [0, 1, 2, 3],
        [0, 2, 1, 3, 4],
        [0, 2, 1, 5, 3, 4],
        [0, 2, 1, 6, 5, 3, 4],
        [0, 2, 1, 7, 5, 6, 3, 4],
    ];

    private readonly EncodedAudio _encoded;
    private readonly OpusHeader _header;
    private readonly byte[] _mapping;
    private readonly long[] _packetStarts;
    private readonly float[] _packet;
    private readonly int _maxPacketFrames;
    private readonly long _preSkip;
    private readonly float _gain;
    private IntPtr _state;
    private int _packetIndex;
    private int _bufferedOffset;
    private int _bufferedFrames;
    private long _discard;
    private long _position;

    public override AudioFormat Format => _encoded.Format;

    public override SampleFormat SampleFormat => SampleFormat.Float32;

    public override long FrameCount { get; }

    public override long Position => _position;

    public override bool CanSeek => true;

    public OpusDecoder(EncodedAudio encoded)
    {
        if (encoded.Packets.IsEmpty)
            throw new AudioException("Opus audio requires a packet table.");

        if (encoded.Format.SampleRate is not (8000 or 12000 or 16000 or 24000 or 48000))
            throw new AudioException($"Opus cannot decode at {encoded.Format.SampleRate} Hz.");

        _encoded = encoded;
        _header = OpusHeader.Parse(encoded.Setup.Span);

        if (_header.Channels != encoded.Format.Channels)
            throw new AudioException($"The OpusHead header declares {_header.Channels} channels, but the audio has {encoded.Format.Channels}.");

        _mapping = _header.MappingFamily == 1 ? [.. VorbisToWaveOrder[_header.Channels - 1].Select(channel => _header.ChannelMapping[channel])] : _header.ChannelMapping;
        _maxPacketFrames = encoded.Format.SampleRate * MaxPacketMilliseconds / 1000;
        _packet = new float[_maxPacketFrames * _header.Channels];
        _preSkip = (long)_header.PreSkip * encoded.Format.SampleRate / 48000;
        _gain = MathF.Pow(10.0f, _header.OutputGain / (20.0f * 256.0f));
        _packetStarts = CountPacketFrames();

        long available = Math.Max(0, _packetStarts[^1] - _preSkip);
        FrameCount = encoded.FrameCount >= 0 ? Math.Min(encoded.FrameCount, available) : available;

        _state = CreateState();
        _discard = _preSkip;
    }

    public override int Read(Span<byte> destination)
    {
        Span<float> output = MemoryMarshal.Cast<byte, float>(destination);
        int channels = Format.Channels;
        int frames = (int)Math.Min(output.Length / channels, FrameCount - _position);
        int written = 0;

        while (written < frames)
        {
            if (_bufferedOffset == _bufferedFrames)
            {
                if (!DecodeNextPacket())
                    break;

                continue;
            }

            int count = Math.Min(frames - written, _bufferedFrames - _bufferedOffset);

            _packet.AsSpan(_bufferedOffset * channels, count * channels).CopyTo(output[(written * channels)..]);
            _bufferedOffset += count;
            written += count;
        }

        _position += written;
        return written;
    }

    public override void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, FrameCount);

        long target = frame + _preSkip;
        long preRoll = (long)Format.SampleRate * PreRollMilliseconds / 1000;
        int packet = Array.BinarySearch(_packetStarts, 0, _packetStarts.Length - 1, Math.Max(0, target - preRoll));

        if (packet < 0)
            packet = Math.Max(0, ~packet - 1);

        OpusInterop.MultistreamDecoderDestroy(_state);
        _state = CreateState();
        _packetIndex = packet;
        _discard = target - _packetStarts[packet];
        _bufferedOffset = 0;
        _bufferedFrames = 0;
        _position = frame;
    }

    public override void Dispose()
    {
        if (_state != IntPtr.Zero)
        {
            OpusInterop.MultistreamDecoderDestroy(_state);
            _state = IntPtr.Zero;
        }

        base.Dispose();
    }

    private IntPtr CreateState()
    {
        IntPtr state = OpusInterop.MultistreamDecoderCreate(Format.SampleRate, _header.Channels, _header.StreamCount, _header.CoupledStreamCount, _mapping, out int error);

        if (error != OpusInterop.Ok)
            throw new AudioException($"Failed to create Opus decoder: {OpusInterop.StrError(error)}");

        return state;
    }

    private long[] CountPacketFrames()
    {
        long[] starts = new long[_encoded.Packets.Length + 1];

        for (int i = 0; i < _encoded.Packets.Length; i++)
        {
            ReadOnlySpan<byte> packet = _encoded.GetPacket(i);
            int frames = packet.IsEmpty ? 0 : OpusInterop.PacketGetNbSamples(packet, packet.Length, Format.SampleRate);

            if (frames < 0)
                throw new AudioException($"Opus packet {i} is invalid: {OpusInterop.StrError(frames)}");

            starts[i + 1] = starts[i] + frames;
        }

        return starts;
    }

    private bool DecodeNextPacket()
    {
        if (_packetIndex >= _encoded.Packets.Length)
            return false;

        ReadOnlySpan<byte> packet = _encoded.GetPacket(_packetIndex);
        int decoded = packet.IsEmpty ? 0 : OpusInterop.MultistreamDecodeFloat(_state, packet, packet.Length, _packet, _maxPacketFrames, 0);

        if (decoded < 0)
            throw new AudioException($"Failed to decode Opus packet {_packetIndex}: {OpusInterop.StrError(decoded)}");

        if (_gain != 1.0f)
        {
            foreach (ref float sample in _packet.AsSpan(0, decoded * Format.Channels))
                sample *= _gain;
        }

        int skipped = (int)Math.Min(_discard, decoded);

        _discard -= skipped;
        _bufferedOffset = skipped;
        _bufferedFrames = decoded;
        _packetIndex++;

        return true;
    }
}
