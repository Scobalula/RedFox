using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using RedFox.Audio;
using RedFox.Audio.IO;
using RedFox.Audio.IO.Wav;
using RedFox.Audio.Opus;

namespace RedFox.Audio.Wwise;

public sealed class WwiseMediaTranslator : AudioTranslator
{
    private const ushort VorbisCodecTag = 0xFFFF;

    private const ushort OpusCodecTag = 0x3041;

    private const ushort PcmCodecTag = 0xFFFE;

    private const int ChannelConfigOffset = 20;

    private const int ChannelConfigMaskShift = 12;

    private const int TotalFramesOffset = 24;

    private const int OpusPacketCountOffset = 28;

    private const int OpusPreSkipOffset = 32;

    private const int OpusMappingFamilyOffset = 35;

    private const int VorbisPacketsOffset = 44;

    private static readonly string[] SupportedExtensions = [".wem"];

    private readonly WavAudioTranslator _wav = new();

    private readonly OpusCodec _opus = new();

    private readonly WwiseVorbisCodec _vorbis = new();

    public override string Name => "Wwise";

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override IReadOnlyList<string> Extensions => SupportedExtensions;

    public override AudioClip Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        if (!TryRead(buffer.ToArray(), out AudioClip? clip))
            throw new AudioException("The data is not Wwise media with a supported codec.");

        return clip;
    }

    public bool TryRead(Memory<byte> media, [NotNullWhen(true)] out AudioClip? clip)
    {
        clip = null;

        if (!WwiseWaveFile.TryRead(media, out WwiseWaveFile? waveFile))
            return false;

        int requiredFormatSize = waveFile.Codec switch { OpusCodecTag => 36, VorbisCodecTag => 66, PcmCodecTag => 24, _ => 16 };

        if (waveFile.Format.Length < requiredFormatSize)
            throw new InvalidDataException("The Wwise codec format is truncated.");

        clip = waveFile.Codec switch
        {
            PcmCodecTag => CreatePcm(media, waveFile),
            OpusCodecTag => new AudioClip(CreateOpus(waveFile)),
            VorbisCodecTag => new AudioClip(CreateVorbis(waveFile)),
            _ => null,
        };

        return clip is not null;
    }

    public override void Write(Stream stream, AudioClip clip, AudioTranslatorOptions options) => throw new NotSupportedException("Wwise media cannot be written.");

    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) => IsValid(filePath, extension) && header.StartsWith("RIFF"u8);

    private static AudioFormat GetFormat(WwiseWaveFile waveFile)
    {
        uint config = BinaryPrimitives.ReadUInt32LittleEndian(waveFile.Format.Span[ChannelConfigOffset..]);
        uint mask = (config & 0xFF) == waveFile.Channels ? config >> ChannelConfigMaskShift : config;

        return BitOperations.PopCount(mask) == waveFile.Channels ? new AudioFormat(waveFile.SampleRate, waveFile.Channels, (ChannelLayout)mask) : new AudioFormat(waveFile.SampleRate, waveFile.Channels);
    }

    private AudioClip CreatePcm(Memory<byte> media, WwiseWaveFile waveFile)
    {
        AudioBuffer buffer = _wav.Read(media).GetBuffer();
        return new AudioClip(new AudioBuffer(GetFormat(waveFile), buffer.SampleFormat, buffer.Data, buffer.ValidBitsPerSample));
    }

    private EncodedAudio CreateOpus(WwiseWaveFile waveFile)
    {
        ReadOnlySpan<byte> format = waveFile.Format.Span;
        ReadOnlySpan<byte> seek = waveFile.Seek.Span;
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(format[OpusPacketCountOffset..]);

        if (count > seek.Length / sizeof(ushort))
            throw new InvalidDataException("The seek table of a Wwise Opus file is shorter than its packet count.");

        int packetCount = (int)count;
        AudioPacket[] packets = new AudioPacket[packetCount];
        int offset = 0;

        for (int i = 0; i < packetCount; i++)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(seek[(i * sizeof(ushort))..]);

            if (length == 0 || length > waveFile.Data.Length - offset)
                throw new InvalidDataException("A Wwise Opus packet extends past the end of the data chunk.");

            packets[i] = new AudioPacket(offset, length);
            offset += length;
        }

        return new EncodedAudio
        {
            Codec = _opus,
            Format = GetFormat(waveFile),
            Data = waveFile.Data,
            FrameCount = BinaryPrimitives.ReadUInt32LittleEndian(format[TotalFramesOffset..]),
            Setup = OpusHeader.Create(waveFile.Channels, BinaryPrimitives.ReadUInt16LittleEndian(format[OpusPreSkipOffset..]), format[OpusMappingFamilyOffset]).ToBytes(),
            Packets = packets,
        };
    }

    private EncodedAudio CreateVorbis(WwiseWaveFile waveFile)
    {
        ReadOnlySpan<byte> data = waveFile.Data.Span;
        List<AudioPacket> packets = [];
        uint packetOffset = BinaryPrimitives.ReadUInt32LittleEndian(waveFile.Format.Span[VorbisPacketsOffset..]);

        if (packetOffset > data.Length)
            throw new InvalidDataException("The Wwise Vorbis packet offset exceeds the data chunk.");

        int position = (int)packetOffset;

        while (position + sizeof(ushort) <= data.Length)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(data[position..]);
            position += sizeof(ushort);

            if (length == 0)
                break;

            if (position + length > data.Length)
                throw new InvalidDataException("A Wwise Vorbis packet extends past the end of the data chunk.");

            packets.Add(new AudioPacket(position, length));
            position += length;
        }

        return new EncodedAudio
        {
            Codec = _vorbis,
            Format = GetFormat(waveFile),
            Data = waveFile.Data,
            FrameCount = BinaryPrimitives.ReadUInt32LittleEndian(waveFile.Format.Span[TotalFramesOffset..]),
            Setup = waveFile.Format,
            Packets = packets.ToArray(),
        };
    }
}
