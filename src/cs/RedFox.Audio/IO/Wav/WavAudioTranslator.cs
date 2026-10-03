// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;
using System.Text;

namespace RedFox.Audio.IO.Wav;

/// <summary>
/// Translator for RIFF WAVE audio files.
/// Supports reading and writing WAVE files with various PCM and encoded audio formats.
/// Custom codec support can be added via <see cref="RegisterCodec"/>.
/// </summary>
public sealed class WavAudioTranslator : AudioTranslator
{
    private const int ByteRateOffset = 12;

    private static readonly string[] SupportedExtensions = [".wav"];

    private readonly Dictionary<ushort, AudioCodec> _codecs = [];

    /// <inheritdoc/>
    public override string Name => "WAV";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => SupportedExtensions;

    /// <summary>
    /// Registers a custom codec for a specific WAVE format tag.
    /// This allows reading and writing WAVE files with non-standard codecs.
    /// </summary>
    /// <param name="formatTag">The WAVE format tag (e.g., 0x02 for MS-ADPCM).</param>
    /// <param name="codec">The codec to use for this format tag.</param>
    /// <exception cref="ArgumentNullException">Thrown when the codec is null.</exception>
    public void RegisterCodec(ushort formatTag, AudioCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        _codecs[formatTag] = codec;
    }

    /// <inheritdoc/>
    public override AudioClip Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            byte[] data = new byte[stream.Length - stream.Position];
            stream.ReadExactly(data);
            return Read(data);
        }

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return Read(buffer.ToArray());
    }

    /// <summary>
    /// Reads a WAVE file from binary data.
    /// </summary>
    /// <param name="data">The WAVE file data.</param>
    /// <returns>An <see cref="AudioClip"/> containing the audio.</returns>
    /// <exception cref="AudioException">Thrown when the data is not a valid WAVE file.</exception>
    public AudioClip Read(Memory<byte> data)
    {
        ReadOnlySpan<byte> span = data.Span;

        if (!IsWave(span))
            throw new AudioException("The data is not a RIFF WAVE file.");

        WavFormatChunk? format = null;
        Memory<byte>? samples = null;
        long frameCount = -1;
        (long Start, long End)? loop = null;
        Dictionary<string, string> tags = [];
        int offset = 12;

        while (offset + 8 <= span.Length)
        {
            ReadOnlySpan<byte> id = span.Slice(offset, 4);
            int size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]), (uint)(span.Length - offset - 8));
            Memory<byte> chunk = data.Slice(offset + 8, size);

            if (id.SequenceEqual("fmt "u8))
                format = WavFormatChunk.Parse(chunk);
            else if (id.SequenceEqual("data"u8))
                samples = chunk;
            else if (id.SequenceEqual("fact"u8) && size >= 4)
                frameCount = BinaryPrimitives.ReadUInt32LittleEndian(chunk.Span);
            else if (id.SequenceEqual("smpl"u8))
                loop = ReadLoop(chunk.Span);
            else if (id.SequenceEqual("LIST"u8) && WavInfoChunk.IsInfo(chunk.Span))
                WavInfoChunk.Read(chunk.Span, tags);

            offset += 8 + size + (size & 1);
        }

        if (format is not { } fmt || samples is not { } sampleData)
            throw new AudioException("The WAVE file has no format or data chunk.");

        AudioClip clip = CreateClip(fmt, sampleData, frameCount);
        clip.LoopStart = loop?.Start;
        clip.LoopEnd = loop?.End;

        foreach ((string field, string value) in tags)
            clip.Tags[field] = value;

        return clip;
    }

    /// <inheritdoc/>
    public override void Write(Stream stream, AudioClip clip, AudioTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(options);

        if (!stream.CanSeek)
        {
            using MemoryStream buffer = new();
            Write(buffer, clip, options);
            buffer.Position = 0;
            buffer.CopyTo(stream);
            return;
        }

        using BinaryWriter writer = new(stream, Encoding.ASCII, true);
        long riff = BeginChunk(writer, "RIFF"u8);
        writer.Write("WAVE"u8);

        if (CanPassthrough(clip, options) && TryGetFormatTag(clip.Encoded!.Codec, out ushort tag))
            WritePassthrough(writer, clip.Encoded, tag);
        else
            WriteDecoded(writer, clip, options);

        if (clip.LoopStart is { } loopStart && clip.LoopEnd is { } loopEnd)
            WriteLoop(writer, clip.Format.SampleRate, loopStart, loopEnd);

        byte[] info = WavInfoChunk.Create(clip.Tags);

        if (info.Length > 0)
        {
            long list = BeginChunk(writer, "LIST"u8);
            writer.Write(info);
            EndChunk(writer, list);
        }

        EndChunk(writer, riff);
    }

    /// <inheritdoc/>
    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) => IsWave(header);

    private static bool IsWave(ReadOnlySpan<byte> header) => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WAVE"u8);

    private static (long Start, long End)? ReadLoop(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length < 60 || BinaryPrimitives.ReadUInt32LittleEndian(chunk[28..]) == 0)
            return null;

        return (BinaryPrimitives.ReadUInt32LittleEndian(chunk[44..]), BinaryPrimitives.ReadUInt32LittleEndian(chunk[48..]) + 1L);
    }

    private static void WriteLoop(BinaryWriter writer, int sampleRate, long start, long end)
    {
        long chunk = BeginChunk(writer, "smpl"u8);

        writer.Write(0);
        writer.Write(0);
        writer.Write(1_000_000_000 / sampleRate);
        writer.Write(60);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1);
        writer.Write(0);

        writer.Write(0);
        writer.Write(0);
        writer.Write((uint)start);
        writer.Write((uint)(end - 1));
        writer.Write(0);
        writer.Write(0);

        EndChunk(writer, chunk);
    }

    private static void WritePassthrough(BinaryWriter writer, EncodedAudio encoded, ushort tag)
    {
        WavFormatChunk format = WavFormatChunk.ForEncoded(tag, encoded.Format, encoded.BlockAlign, encoded.BitsPerSample, encoded.Setup);
        (long Format, long Data) chunks = BeginData(writer, format);

        writer.Write(encoded.Data.Span);
        EndData(writer, format, chunks, encoded.FrameCount);
    }

    private static (long Format, long Data) BeginData(BinaryWriter writer, WavFormatChunk format)
    {
        long fmt = BeginChunk(writer, "fmt "u8);
        format.Write(writer);
        EndChunk(writer, fmt);

        if (!format.IsPcm)
        {
            long fact = BeginChunk(writer, "fact"u8);
            writer.Write(0u);
            EndChunk(writer, fact);
        }

        return (fmt, BeginChunk(writer, "data"u8));
    }

    private static void EndData(BinaryWriter writer, WavFormatChunk format, (long Format, long Data) chunks, long frameCount)
    {
        long dataSize = writer.BaseStream.Position - chunks.Data - 4;
        EndChunk(writer, chunks.Data);

        if (format.IsPcm)
            return;

        uint byteRate = frameCount > 0 ? (uint)(dataSize * format.Format.SampleRate / frameCount) : 0;

        Patch(writer, chunks.Format + ByteRateOffset, byteRate);
        Patch(writer, chunks.Data - 8, (uint)Math.Max(frameCount, 0));
    }

    private static long BeginChunk(BinaryWriter writer, ReadOnlySpan<byte> id)
    {
        writer.Write(id);
        long sizePosition = writer.BaseStream.Position;
        writer.Write(0u);
        return sizePosition;
    }

    private static void EndChunk(BinaryWriter writer, long sizePosition)
    {
        long size = writer.BaseStream.Position - sizePosition - 4;
        Patch(writer, sizePosition, (uint)size);

        if ((size & 1) != 0)
            writer.Write((byte)0);
    }

    private static void Patch(BinaryWriter writer, long position, uint value)
    {
        long end = writer.BaseStream.Position;

        writer.BaseStream.Position = position;
        writer.Write(value);
        writer.BaseStream.Position = end;
    }

    private void WriteDecoded(BinaryWriter writer, AudioClip clip, AudioTranslatorOptions options)
    {
        using AudioDecoder decoder = clip.OpenDecoder();
        WavDataWriter output = new(writer.BaseStream);

        if (options.Codec is { } codec)
        {
            if (!TryGetFormatTag(codec, out ushort tag))
                throw new AudioException($"The {codec.Name} codec cannot be stored in a WAVE file.");

            using AudioEncoder encoder = codec.CreateEncoder(decoder.Format, options.Encoder ?? new AudioEncoderOptions());
            WavFormatChunk format = WavFormatChunk.ForEncoded(tag, decoder.Format, encoder.BlockAlign, encoder.BitsPerSample, encoder.Setup);
            (long Format, long Data) chunks = BeginData(writer, format);

            EncodeSamples(decoder, encoder, output);
            EndData(writer, format, chunks, output.FrameCount);
        }
        else
        {
            SampleFormat sampleFormat = options.SampleFormat ?? decoder.SampleFormat;
            int validBitsPerSample = SampleFormatInfo.GetConvertedValidBits(decoder.ValidBitsPerSample, sampleFormat);
            WavFormatChunk format = WavFormatChunk.ForSamples(decoder.Format, sampleFormat, validBitsPerSample);
            (long Format, long Data) chunks = BeginData(writer, format);

            CopySamples(decoder, sampleFormat, output);
            EndData(writer, format, chunks, output.FrameCount);
        }
    }

    private AudioClip CreateClip(WavFormatChunk format, Memory<byte> samples, long frameCount)
    {
        if (format.SampleFormat is { } sampleFormat)
            return new AudioClip(new AudioBuffer(format.Format, sampleFormat, samples, format.GetValidBits(sampleFormat)));

        EncodedAudio encoded = new()
        {
            Codec = _codecs.GetValueOrDefault(format.Tag) ?? new WavPassthroughCodec(format.Tag),
            Format = format.Format,
            Data = samples,
            FrameCount = frameCount,
            BitsPerSample = format.BitsPerSample,
            BlockAlign = format.BlockAlign,
            Setup = format.Extra,
        };

        return new AudioClip(encoded);
    }

    private bool TryGetFormatTag(AudioCodec codec, out ushort tag)
    {
        if (codec is WavPassthroughCodec passthrough)
        {
            tag = passthrough.FormatTag;
            return true;
        }

        foreach ((ushort formatTag, AudioCodec candidate) in _codecs)
        {
            if (candidate.Id == codec.Id)
            {
                tag = formatTag;
                return true;
            }
        }

        tag = 0;
        return false;
    }
}
