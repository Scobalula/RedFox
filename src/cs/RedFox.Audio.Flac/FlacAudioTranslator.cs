// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Globalization;
using System.Numerics;
using RedFox.Audio.IO;

namespace RedFox.Audio.Flac;

/// <summary>
/// Translator for native FLAC audio files.
/// Encoded FLAC is kept as-is when read and written back without decoding where possible.
/// Tags, loop points, and non-default channel layouts are stored as Vorbis comments, and all other
/// metadata blocks from a FLAC source are preserved.
/// </summary>
public sealed class FlacAudioTranslator : AudioTranslator
{
    private const string LoopStartField = "LOOPSTART";

    private const string LoopLengthField = "LOOPLENGTH";

    private const string ChannelMaskField = "WAVEFORMATEXTENSIBLE_CHANNEL_MASK";

    private const string Vendor = "RedFox";

    private const int MinBitsPerSample = 4;

    private const int FloatBitsPerSample = 24;

    private const ChannelLayout SevenChannelLayout = ChannelLayout.FrontLeft | ChannelLayout.FrontRight | ChannelLayout.FrontCenter | ChannelLayout.LowFrequency | ChannelLayout.BackCenter | ChannelLayout.SideLeft | ChannelLayout.SideRight;

    private static readonly string[] SupportedExtensions = [".flac"];

    private readonly FlacCodec _codec = new();

    /// <inheritdoc/>
    public override string Name => "FLAC";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => SupportedExtensions;

    /// <inheritdoc/>
    public override AudioClip Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        return Read(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
    }

    /// <summary>
    /// Reads a FLAC file from binary data without decoding it.
    /// </summary>
    /// <param name="data">The FLAC file data.</param>
    /// <returns>An <see cref="AudioClip"/> holding the encoded FLAC audio.</returns>
    /// <exception cref="AudioException">Thrown when the data is not a valid FLAC file.</exception>
    public AudioClip Read(ReadOnlyMemory<byte> data)
    {
        FlacMetadata metadata = FlacMetadata.Parse(data);
        FlacStreamInfo info = metadata.StreamInfo;
        int index = metadata.IndexOf(FlacMetadataBlock.VorbisComment);
        Dictionary<string, string> fields = index < 0 ? new(StringComparer.OrdinalIgnoreCase) : FlacVorbisComment.Parse(metadata.Blocks[index].Data.Span).GetFields();
        int size = metadata.Size;

        EncodedAudio encoded = new()
        {
            Codec = _codec,
            Format = new AudioFormat(info.SampleRate, info.Channels, ReadLayout(fields, info.Channels)),
            Setup = data[..size],
            Data = data[size..],
            FrameCount = info.TotalSamples > 0 ? info.TotalSamples : -1,
            BitsPerSample = info.BitsPerSample,
        };

        (long? loopStart, long? loopEnd) = ReadLoop(fields);
        AudioClip clip = new(encoded)
        {
            LoopStart = loopStart,
            LoopEnd = loopEnd,
        };

        foreach ((string field, string value) in fields.Where(field => !IsReserved(field.Key)))
            clip.Tags[field] = value;

        return clip;
    }

    /// <inheritdoc/>
    public override void Write(Stream stream, AudioClip clip, AudioTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Codec is { } codec && codec.Id != _codec.Id)
            throw new AudioException($"The {codec.Name} codec cannot be stored in a FLAC file.");

        if (!stream.CanSeek)
        {
            using MemoryStream buffer = new();
            Write(buffer, clip, options);
            buffer.Position = 0;
            buffer.CopyTo(stream);
            return;
        }

        if (CanPassthrough(clip, options) && clip.Encoded!.Codec is FlacCodec)
            WritePassthrough(stream, clip);
        else
            WriteEncoded(stream, clip, options);
    }

    /// <inheritdoc/>
    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) => FlacMetadata.IsFlac(header);

    private static void WritePassthrough(Stream stream, AudioClip clip)
    {
        FlacMetadata metadata = FlacMetadata.Parse(clip.Encoded!.Setup);

        ApplyComments(metadata, clip, clip.Format);
        metadata.Write(stream);
        stream.Write(clip.Encoded.Data.Span);
    }

    private void WriteEncoded(Stream stream, AudioClip clip, AudioTranslatorOptions options)
    {
        using AudioDecoder decoder = clip.OpenDecoder();
        FlacEncoderOptions encoderOptions = new()
        {
            Quality = options.Encoder?.Quality,
            BitsPerSample = GetBitsPerSample(decoder, options),
        };

        using AudioEncoder encoder = _codec.CreateEncoder(decoder.Format, encoderOptions);
        FlacMetadata metadata = FlacMetadata.Parse(encoder.Setup.ToArray());

        if (clip.Encoded is { Codec: FlacCodec } source)
        {
            FlacMetadataBlock streamInfo = metadata.Blocks[0];

            metadata = FlacMetadata.Parse(source.Setup);
            metadata.Blocks[0] = streamInfo;
            metadata.Blocks.RemoveAll(block => block.Type == FlacMetadataBlock.SeekTable);
        }

        long start = stream.Position;

        ApplyComments(metadata, clip, decoder.Format);
        metadata.Write(stream);
        EncodeSamples(decoder, encoder, new FlacFrameWriter(stream));

        long end = stream.Position;

        stream.Position = start + FlacMetadata.StreamInfoOffset;
        stream.Write(encoder.Setup.Span.Slice(FlacMetadata.StreamInfoOffset, FlacStreamInfo.Size));
        stream.Position = end;
    }

    private static int GetBitsPerSample(AudioDecoder decoder, AudioTranslatorOptions options)
    {
        if (options.Encoder is FlacEncoderOptions { BitsPerSample: { } bitsPerSample })
            return bitsPerSample;

        if (options.SampleFormat is { } sampleFormat)
            return SampleFormatInfo.IsFloat(sampleFormat) ? throw new AudioException("FLAC cannot store floating-point samples.") : SampleFormatInfo.GetBitsPerSample(sampleFormat);

        return SampleFormatInfo.IsFloat(decoder.SampleFormat) ? FloatBitsPerSample : Math.Max(decoder.ValidBitsPerSample, MinBitsPerSample);
    }

    private static void ApplyComments(FlacMetadata metadata, AudioClip clip, AudioFormat format)
    {
        int index = metadata.IndexOf(FlacMetadataBlock.VorbisComment);
        FlacVorbisComment? existing = index < 0 ? null : FlacVorbisComment.Parse(metadata.Blocks[index].Data.Span);
        Dictionary<string, string> current = existing?.GetFields() ?? new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> fields = CreateFields(clip, format);

        if (current.Count == fields.Count && fields.All(field => current.TryGetValue(field.Key, out string? value) && value == field.Value))
            return;

        FlacVorbisComment comments = new(existing?.Vendor ?? Vendor, [.. fields.Select(field => $"{field.Key}={field.Value}")]);
        FlacMetadataBlock block = new(FlacMetadataBlock.VorbisComment, comments.ToBytes());

        if (index < 0)
            metadata.Blocks.Insert(1, block);
        else
            metadata.Blocks[index] = block;
    }

    private static Dictionary<string, string> CreateFields(AudioClip clip, AudioFormat format)
    {
        Dictionary<string, string> fields = new(clip.Tags.Where(tag => !IsReserved(tag.Key)), StringComparer.OrdinalIgnoreCase);
        ChannelLayout defaultLayout = GetDefaultLayout(format.Channels);

        if (format.Layout != ChannelLayout.None && format.Layout != defaultLayout)
            fields[ChannelMaskField] = $"0x{(uint)format.Layout:X4}";

        if (clip.LoopStart is { } loopStart && clip.LoopEnd is { } loopEnd)
        {
            fields[LoopStartField] = loopStart.ToString(CultureInfo.InvariantCulture);
            fields[LoopLengthField] = (loopEnd - loopStart).ToString(CultureInfo.InvariantCulture);
        }

        return fields;
    }

    private static bool IsReserved(string field) => field.Equals(LoopStartField, StringComparison.OrdinalIgnoreCase) || field.Equals(LoopLengthField, StringComparison.OrdinalIgnoreCase) || field.Equals(ChannelMaskField, StringComparison.OrdinalIgnoreCase);

    private static ChannelLayout ReadLayout(Dictionary<string, string> fields, int channels)
    {
        if (fields.TryGetValue(ChannelMaskField, out string? value) && uint.TryParse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint mask) && BitOperations.PopCount(mask) == channels)
            return (ChannelLayout)mask;

        return GetDefaultLayout(channels);
    }

    private static (long? Start, long? End) ReadLoop(Dictionary<string, string> fields)
    {
        if (fields.TryGetValue(LoopStartField, out string? start) && fields.TryGetValue(LoopLengthField, out string? length) && long.TryParse(start, CultureInfo.InvariantCulture, out long loopStart) && long.TryParse(length, CultureInfo.InvariantCulture, out long loopLength))
            return (loopStart, loopStart + loopLength);

        return (null, null);
    }

    private static ChannelLayout GetDefaultLayout(int channels) => channels == 7 ? SevenChannelLayout : AudioFormat.GetDefaultLayout(channels);
}
