using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Audio;

namespace RedFox.Audio.Wwise;

internal sealed unsafe class WwiseVorbisDecoder : AudioDecoder
{
    private const int FirstAudioPacketNumber = 3;

    private const int HeaderCount = 3;

    private const int WwiseModeBits = 1;

    private const int MaxModes = 1 << WwiseModeBits;

    private const int SetupOffset = 40;

    private const int ShortBlockExponentOffset = 64;

    private const int LongBlockExponentOffset = 65;

    private readonly EncodedAudio _encoded;

    private readonly bool[] _longBlocks;

    private readonly long[] _packetStarts;

    private readonly int _modeBits;

    private void* _info;

    private void* _comment;

    private void* _dspState;

    private void* _block;

    private bool _synthesisStarted;

    private int _packetIndex;

    private long _position;

    private long _discard;

    public override AudioFormat Format => _encoded.Format;

    public override SampleFormat SampleFormat => SampleFormat.Float32;

    public override long FrameCount => _encoded.FrameCount;

    public override long Position => _position;

    public override bool CanSeek => _longBlocks.Length > 1;

    public WwiseVorbisDecoder(EncodedAudio encoded)
    {
        _encoded = encoded;

        ReadOnlySpan<byte> format = encoded.Setup.Span;
        ReadOnlySpan<byte> data = encoded.Data.Span;
        int channels = encoded.Format.Channels;
        int setupOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(format[SetupOffset..]);
        int setupSize = BinaryPrimitives.ReadUInt16LittleEndian(data[setupOffset..]);

        byte[][] headers =
        [
            VorbisHeaderBuilder.BuildIdentificationHeader(channels, encoded.Format.SampleRate, format[ShortBlockExponentOffset], format[LongBlockExponentOffset]),
            VorbisHeaderBuilder.BuildCommentHeader(),
            VorbisHeaderBuilder.BuildSetupHeader(data.Slice(setupOffset + sizeof(ushort), setupSize), PackedCodebooks.Default, channels, out bool[] modeBlockFlags),
        ];

        if (modeBlockFlags.Length > MaxModes)
            throw new InvalidDataException($"A Wwise Vorbis file has {modeBlockFlags.Length} modes, but a packet can only select between {MaxModes}.");

        _modeBits = 32 - BitOperations.LeadingZeroCount((uint)(modeBlockFlags.Length - 1));
        _longBlocks = new bool[encoded.Packets.Length];

        for (int i = 0; i < _longBlocks.Length; i++)
        {
            int mode = encoded.GetPacket(i)[0] & 1;

            if (mode >= modeBlockFlags.Length)
                throw new InvalidDataException($"Packet {i} of a Wwise Vorbis file selects mode {mode}, which the file does not define.");

            _longBlocks[i] = modeBlockFlags[mode];
        }

        _packetStarts = CountPacketFrames(1 << format[ShortBlockExponentOffset], 1 << format[LongBlockExponentOffset]);

        _info = NativeMemory.AllocZeroed(VorbisInterop.InfoSize);
        _comment = NativeMemory.AllocZeroed(VorbisInterop.CommentSize);
        _dspState = NativeMemory.AllocZeroed(VorbisInterop.DspStateSize);
        _block = NativeMemory.AllocZeroed(VorbisInterop.BlockSize);

        VorbisInterop.InitializeInfo(_info);
        VorbisInterop.InitializeComment(_comment);

        try
        {
            for (int i = 0; i < HeaderCount; i++)
                ReadHeader(headers[i], i);

            ThrowIfFailed(VorbisInterop.InitializeSynthesis(_dspState, _info), "start synthesis");
            ThrowIfFailed(VorbisInterop.InitializeBlock(_dspState, _block), "start a block");

            _synthesisStarted = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public override int Read(Span<byte> destination)
    {
        Span<float> output = MemoryMarshal.Cast<byte, float>(destination);
        int channels = Format.Channels;
        long remaining = FrameCount < 0 ? long.MaxValue : FrameCount - _position;
        int frames = (int)Math.Min(output.Length / channels, remaining);
        int written = 0;

        while (written < frames)
        {
            float** pcm;
            int available = VorbisInterop.GetPcm(_dspState, &pcm);

            if (available <= 0)
            {
                if (!SynthesizeNextPacket())
                    break;

                continue;
            }

            if (_discard > 0)
            {
                int skipped = (int)Math.Min(available, _discard);

                VorbisInterop.ReadPcm(_dspState, skipped);
                _discard -= skipped;
                continue;
            }

            int count = Math.Min(available, frames - written);

            for (int frame = 0; frame < count; frame++)
            {
                for (int channel = 0; channel < channels; channel++)
                    output[((written + frame) * channels) + channel] = pcm[channel][frame];
            }

            VorbisInterop.ReadPcm(_dspState, count);
            written += count;
        }

        _position += written;
        return written;
    }

    public override void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);

        if (FrameCount >= 0)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, FrameCount);

        int packet = Array.BinarySearch(_packetStarts, 1, _longBlocks.Length - 1, frame);

        if (packet < 0)
            packet = ~packet - 1;

        packet = Math.Clamp(packet, 1, _longBlocks.Length - 1);

        ThrowIfFailed(VorbisInterop.Restart(_dspState), "restart synthesis");

        _packetIndex = packet - 1;
        _discard = frame - _packetStarts[packet];
        _position = frame;
    }

    public override void Dispose()
    {
        if (_info is null)
            return;

        if (_synthesisStarted)
        {
            VorbisInterop.ClearBlock(_block);
            VorbisInterop.ClearDspState(_dspState);
        }

        VorbisInterop.ClearComment(_comment);
        VorbisInterop.ClearInfo(_info);

        NativeMemory.Free(_info);
        NativeMemory.Free(_comment);
        NativeMemory.Free(_dspState);
        NativeMemory.Free(_block);

        _info = null;
        _comment = null;
        _dspState = null;
        _block = null;

        base.Dispose();
    }

    private long[] CountPacketFrames(int shortBlockSize, int longBlockSize)
    {
        long[] starts = new long[_longBlocks.Length + 1];

        for (int i = 0; i < _longBlocks.Length; i++)
        {
            int frames = i == 0 ? 0 : ((_longBlocks[i - 1] ? longBlockSize : shortBlockSize) + (_longBlocks[i] ? longBlockSize : shortBlockSize)) / 4;
            starts[i + 1] = starts[i] + frames;
        }

        return starts;
    }

    private bool SynthesizeNextPacket()
    {
        if (_packetIndex >= _longBlocks.Length)
            return false;

        byte[] restored = RestorePacket(_encoded.GetPacket(_packetIndex));

        fixed (byte* pointer = restored)
        {
            OggPacket packet = new() { Data = (nint)pointer, Length = restored.Length, GranulePosition = -1, PacketNumber = FirstAudioPacketNumber + _packetIndex };

            ThrowIfFailed(VorbisInterop.Synthesize(_block, &packet), $"synthesize packet {_packetIndex}");
            ThrowIfFailed(VorbisInterop.BlockIn(_dspState, _block), $"process packet {_packetIndex}");
        }

        _packetIndex++;
        return true;
    }

    private byte[] RestorePacket(ReadOnlySpan<byte> packet)
    {
        OggBitWriter writer = new(packet.Length + 4);

        writer.WriteBits(0, 1);
        writer.WriteBits((uint)(packet[0] & 1), _modeBits);

        if (_longBlocks[_packetIndex])
        {
            writer.WriteBits(_packetIndex > 0 && _longBlocks[_packetIndex - 1] ? 1u : 0u, 1);
            writer.WriteBits(_packetIndex < _longBlocks.Length - 1 && _longBlocks[_packetIndex + 1] ? 1u : 0u, 1);
        }

        writer.CopyBits(packet, WwiseModeBits, (packet.Length * 8) - WwiseModeBits);

        return writer.ToArray();
    }

    private void ReadHeader(byte[] header, int number)
    {
        fixed (byte* pointer = header)
        {
            OggPacket packet = new() { Data = (nint)pointer, Length = header.Length, BeginningOfStream = number == 0 ? 1 : 0, PacketNumber = number };

            ThrowIfFailed(VorbisInterop.ReadHeader(_info, _comment, &packet), $"read header {number}");
        }
    }

    private static void ThrowIfFailed(int result, string operation)
    {
        if (result != 0)
            throw new InvalidDataException($"libvorbis failed to {operation}: {result}.");
    }
}
