// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Audio.IO.Wav;

internal readonly record struct WavFormatChunk(ushort Tag, AudioFormat Format, int BlockAlign, int BitsPerSample, int ValidBitsPerSample, ReadOnlyMemory<byte> Extra)
{
    public const ushort PcmTag = 1;

    public const ushort FloatTag = 3;

    public const ushort ExtensibleTag = 0xFFFE;

    private static ReadOnlySpan<byte> SubFormatSuffix => [0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    public bool IsPcm => Tag is PcmTag or FloatTag;

    public bool IsExtensible => Format.Channels > 2 || Format.Layout != AudioFormat.GetDefaultLayout(Format.Channels) || ValidBitsPerSample != BitsPerSample;

    public SampleFormat? SampleFormat => (Tag, BlockAlign / Math.Max(Format.Channels, 1)) switch
    {
        (PcmTag, 1) => Audio.SampleFormat.UInt8,
        (PcmTag, 2) => Audio.SampleFormat.Int16,
        (PcmTag, 3) => Audio.SampleFormat.Int24,
        (PcmTag, 4) => Audio.SampleFormat.Int32,
        (FloatTag, 4) => Audio.SampleFormat.Float32,
        (FloatTag, 8) => Audio.SampleFormat.Float64,
        _ => null,
    };

    public static WavFormatChunk ForSamples(AudioFormat format, SampleFormat sampleFormat, int validBitsPerSample)
    {
        ushort tag = SampleFormatInfo.IsFloat(sampleFormat) ? FloatTag : PcmTag;
        return new WavFormatChunk(tag, format, SampleFormatInfo.GetBytesPerSample(sampleFormat) * format.Channels, SampleFormatInfo.GetBitsPerSample(sampleFormat), validBitsPerSample, ReadOnlyMemory<byte>.Empty);
    }

    public static WavFormatChunk ForEncoded(ushort tag, AudioFormat format, int blockAlign, int bitsPerSample, ReadOnlyMemory<byte> setup)
    {
        return new WavFormatChunk(tag, format, blockAlign, bitsPerSample, bitsPerSample, setup);
    }

    public static WavFormatChunk Parse(ReadOnlyMemory<byte> chunk)
    {
        ReadOnlySpan<byte> span = chunk.Span;

        if (span.Length < 16)
            throw new AudioException("The WAVE format chunk is truncated.");

        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(span);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(span[2..]);
        int sampleRate = BinaryPrimitives.ReadInt32LittleEndian(span[4..]);
        int blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(span[12..]);
        int bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(span[14..]);
        ReadOnlyMemory<byte> extra = span.Length >= 18 ? chunk.Slice(18, Math.Min(BinaryPrimitives.ReadUInt16LittleEndian(span[16..]), span.Length - 18)) : ReadOnlyMemory<byte>.Empty;

        if (tag == ExtensibleTag && extra.Length >= 6)
        {
            int validBitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(extra.Span);
            ChannelLayout layout = (ChannelLayout)BinaryPrimitives.ReadUInt32LittleEndian(extra.Span[2..]);
            bool hasSubFormat = extra.Length >= 22;
            ushort subFormat = hasSubFormat ? BinaryPrimitives.ReadUInt16LittleEndian(extra.Span[6..]) : PcmTag;

            return new WavFormatChunk(subFormat, new AudioFormat(sampleRate, channels, layout), blockAlign, bitsPerSample, validBitsPerSample == 0 ? bitsPerSample : validBitsPerSample, hasSubFormat ? extra[22..] : ReadOnlyMemory<byte>.Empty);
        }

        return new WavFormatChunk(tag, new AudioFormat(sampleRate, channels), blockAlign, bitsPerSample, bitsPerSample, extra);
    }

    public int GetValidBits(SampleFormat sampleFormat) => Math.Clamp(ValidBitsPerSample, 1, SampleFormatInfo.GetBitsPerSample(sampleFormat));

    public void Write(BinaryWriter writer)
    {
        bool extensible = IsExtensible;

        writer.Write(extensible ? ExtensibleTag : Tag);
        writer.Write((ushort)Format.Channels);
        writer.Write(Format.SampleRate);
        writer.Write(Format.SampleRate * BlockAlign);
        writer.Write((ushort)BlockAlign);
        writer.Write((ushort)BitsPerSample);

        if (extensible)
        {
            writer.Write((ushort)(22 + Extra.Length));
            writer.Write((ushort)ValidBitsPerSample);
            writer.Write((uint)Format.Layout);
            writer.Write(Tag);
            writer.Write(SubFormatSuffix);
            writer.Write(Extra.Span);
        }
        else if (Tag != PcmTag)
        {
            writer.Write((ushort)Extra.Length);
            writer.Write(Extra.Span);
        }
    }
}
