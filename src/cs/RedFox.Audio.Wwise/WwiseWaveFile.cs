using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace RedFox.Audio.Wwise;

public sealed class WwiseWaveFile(ReadOnlyMemory<byte> format, ReadOnlyMemory<byte> seek, ReadOnlyMemory<byte> data)
{
    private const uint RiffTag = 0x46464952;

    private const uint WaveTag = 0x45564157;

    private const uint FormatTag = 0x20746D66;

    private const uint DataTag = 0x61746164;

    private const uint SeekTag = 0x6B656573;

    private const int RiffHeaderSize = 12;

    private const int ChunkHeaderSize = 8;

    public ReadOnlyMemory<byte> Format { get; } = format;

    public ReadOnlyMemory<byte> Seek { get; } = seek;

    public ReadOnlyMemory<byte> Data { get; } = data;

    public ushort Codec => BinaryPrimitives.ReadUInt16LittleEndian(Format.Span);

    public int Channels => BinaryPrimitives.ReadUInt16LittleEndian(Format.Span[2..]);

    public int SampleRate => (int)BinaryPrimitives.ReadUInt32LittleEndian(Format.Span[4..]);

    public static bool TryRead(ReadOnlyMemory<byte> media, [NotNullWhen(true)] out WwiseWaveFile? waveFile)
    {
        waveFile = null;

        var span = media.Span;

        if (span.Length < RiffHeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(span) != RiffTag || BinaryPrimitives.ReadUInt32LittleEndian(span[8..]) != WaveTag)
            return false;

        if (BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) + 8L > span.Length)
            return false;

        var format = ReadOnlyMemory<byte>.Empty;
        var seek = ReadOnlyMemory<byte>.Empty;
        var data = ReadOnlyMemory<byte>.Empty;
        var position = RiffHeaderSize;

        while (position + ChunkHeaderSize <= span.Length)
        {
            var tag = BinaryPrimitives.ReadUInt32LittleEndian(span[position..]);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(span[(position + 4)..]);

            if (chunkSize > span.Length - position - ChunkHeaderSize)
                throw new InvalidDataException("A Wwise wave chunk exceeds the media length.");

            int size = (int)chunkSize;
            var payload = media.Slice(position + ChunkHeaderSize, size);

            if (tag == FormatTag)
                format = payload;
            else if (tag == SeekTag)
                seek = payload;
            else if (tag == DataTag)
                data = payload;

            position += ChunkHeaderSize + size + (size & 1);
        }

        if (format.Length < 16 || data.IsEmpty)
            return false;

        waveFile = new WwiseWaveFile(format, seek, data);

        return true;
    }
}
