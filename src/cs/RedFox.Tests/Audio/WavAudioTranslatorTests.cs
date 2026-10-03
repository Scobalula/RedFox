using System.Buffers.Binary;
using System.Runtime.InteropServices;
using RedFox.Audio;
using RedFox.Audio.ADPCM;
using RedFox.Audio.IO;
using RedFox.Audio.IO.Wav;

namespace RedFox.Tests.Audio;

public sealed class WavAudioTranslatorTests
{
    private const int WavFormatTagPcm = 1;

    private const int WavFormatTagFloat = 3;

    private const int WavFormatTagExtensible = 0xFFFE;

    [Theory]
    [InlineData(SampleFormat.UInt8, 1)]
    [InlineData(SampleFormat.Int16, 2)]
    [InlineData(SampleFormat.Int24, 2)]
    [InlineData(SampleFormat.Int32, 6)]
    [InlineData(SampleFormat.Float32, 2)]
    [InlineData(SampleFormat.Float64, 8)]
    public void WriteRead_Pcm_RoundTripsSamplesAndFormatExactly(SampleFormat sampleFormat, int channels)
    {
        AudioBuffer source = CreateNoise(new AudioFormat(48000, channels), sampleFormat, 1000);
        WavAudioTranslator translator = new();

        AudioClip clip = translator.Read(Write(translator, new AudioClip(source)));
        AudioBuffer result = clip.GetBuffer();

        Assert.Null(clip.Encoded);
        Assert.Equal(source.Format, result.Format);
        Assert.Equal(sampleFormat, result.SampleFormat);
        Assert.Equal(source.Data.ToArray(), result.Data.ToArray());
    }

    [Theory]
    [InlineData(SampleFormat.Int16, 2, WavFormatTagPcm)]
    [InlineData(SampleFormat.Float32, 2, WavFormatTagFloat)]
    [InlineData(SampleFormat.Int24, 2, WavFormatTagPcm)]
    [InlineData(SampleFormat.Int32, 1, WavFormatTagPcm)]
    [InlineData(SampleFormat.Int16, 6, WavFormatTagExtensible)]
    public void Write_Pcm_ChoosesExpectedFormatTag(SampleFormat sampleFormat, int channels, int expectedTag)
    {
        WavAudioTranslator translator = new();
        byte[] file = Write(translator, new AudioClip(CreateNoise(new AudioFormat(44100, channels), sampleFormat, 16)));

        Assert.Equal(expectedTag, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(20)));
    }

    [Theory]
    [InlineData(SampleFormat.UInt8, 1)]
    [InlineData(SampleFormat.Int24, 2)]
    [InlineData(SampleFormat.Float32, 6)]
    public void WriteReadWrite_Pcm_ProducesIdenticalFile(SampleFormat sampleFormat, int channels)
    {
        WavAudioTranslator translator = new();
        byte[] file = Write(translator, new AudioClip(CreateNoise(new AudioFormat(44100, channels), sampleFormat, 100)));

        Assert.Equal(file, Write(translator, translator.Read(file)));
    }

    [Fact]
    public void WriteRead_ValidBitsPerSample_IsPreservedInExtensibleHeader()
    {
        WavAudioTranslator translator = new();
        AudioBuffer source = CreateNoise(new AudioFormat(96000, 2), SampleFormat.Int24, 100);
        AudioBuffer twentyBit = new(source.Format, SampleFormat.Int24, source.Data, 20);

        byte[] file = Write(translator, new AudioClip(twentyBit));
        AudioBuffer result = translator.Read(file).GetBuffer();

        Assert.Equal(WavFormatTagExtensible, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(20)));
        Assert.Equal(20, result.ValidBitsPerSample);
        Assert.Equal(16, result.Convert(SampleFormat.Int16).ValidBitsPerSample);
        Assert.Equal(20, result.Convert(SampleFormat.Int32).ValidBitsPerSample);
    }

    [Fact]
    public void ReadWrite_UnknownFormatTag_PassesThroughButCannotDecode()
    {
        WavAudioTranslator writer = new();
        writer.RegisterCodec(CountingAudioCodec.WaveFormatTag, new CountingAudioCodec());
        AudioBuffer samples = CreateNoise(new AudioFormat(44100, 2), SampleFormat.Int16, 100);
        EncodedAudio encoded = new()
        {
            Codec = new CountingAudioCodec(),
            Format = samples.Format,
            Data = samples.Data,
            FrameCount = samples.FrameCount,
            BitsPerSample = 16,
            BlockAlign = 4,
        };
        byte[] file = Write(writer, new AudioClip(encoded));
        WavAudioTranslator reader = new();

        AudioClip clip = reader.Read(file);

        Assert.False(clip.Encoded!.Codec.CanDecode);
        Assert.Equal(file, Write(reader, clip));
        Assert.Throws<NotSupportedException>(() => clip.GetBuffer());
    }

    [Fact]
    public void Read_ExtensibleHeaderWithoutSubFormat_IsTreatedAsPcm()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        byte[] samples = [1, 0, 2, 0, 3, 0, 4, 0];

        writer.Write("RIFF"u8);
        writer.Write(4 + 8 + 24 + 8 + samples.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(24);
        writer.Write((ushort)0xFFFE);
        writer.Write((ushort)2);
        writer.Write(48000);
        writer.Write(48000 * 4);
        writer.Write((ushort)4);
        writer.Write((ushort)16);
        writer.Write((ushort)6);
        writer.Write((ushort)16);
        writer.Write((uint)ChannelLayout.Stereo);
        writer.Write("data"u8);
        writer.Write(samples.Length);
        writer.Write(samples);

        AudioBuffer buffer = new WavAudioTranslator().Read(stream.ToArray()).GetBuffer();

        Assert.Equal(SampleFormat.Int16, buffer.SampleFormat);
        Assert.Equal(new AudioFormat(48000, 2, ChannelLayout.Stereo), buffer.Format);
        Assert.Equal(samples, buffer.Data.ToArray());
    }

    [Fact]
    public void WriteRead_CustomChannelLayout_IsPreserved()
    {
        AudioFormat format = new(44100, 2, ChannelLayout.SideLeft | ChannelLayout.SideRight);
        WavAudioTranslator translator = new();

        AudioClip clip = translator.Read(Write(translator, new AudioClip(CreateNoise(format, SampleFormat.Int16, 16))));

        Assert.Equal(format, clip.Format);
    }

    [Fact]
    public void WriteRead_LoopPoints_ArePreserved()
    {
        WavAudioTranslator translator = new();
        AudioClip source = new(CreateNoise(new AudioFormat(22050, 1), SampleFormat.Int16, 500))
        {
            LoopStart = 100,
            LoopEnd = 400,
        };

        AudioClip clip = translator.Read(Write(translator, source));

        Assert.Equal(100, clip.LoopStart);
        Assert.Equal(400, clip.LoopEnd);
    }

    [Fact]
    public void WriteRead_Tags_AreStoredInInfoChunk()
    {
        WavAudioTranslator translator = new();
        AudioClip source = new(CreateNoise(new AudioFormat(22050, 1), SampleFormat.Int16, 100));
        source.Tags["TITLE"] = "Odd";
        source.Tags["ARTIST"] = "Even!";
        source.Tags["IKEY"] = "custom";
        source.Tags["DISCNUMBER"] = "1";

        byte[] file = Write(translator, source);
        AudioClip clip = translator.Read(file);

        Assert.Equal(3, clip.Tags.Count);
        Assert.Equal("Odd", clip.Tags["TITLE"]);
        Assert.Equal("Even!", clip.Tags["ARTIST"]);
        Assert.Equal("custom", clip.Tags["IKEY"]);
        Assert.Equal(file, Write(translator, clip));
    }

    [Fact]
    public void Read_Pcm_WrapsSourceMemoryWithoutCopying()
    {
        WavAudioTranslator translator = new();
        byte[] file = Write(translator, new AudioClip(CreateNoise(new AudioFormat(44100, 2), SampleFormat.Int16, 64)));

        AudioBuffer buffer = translator.Read(file).GetBuffer();

        Assert.True(MemoryMarshal.TryGetArray<byte>(buffer.Data, out ArraySegment<byte> segment));
        Assert.Same(file, segment.Array);
    }

    [Fact]
    public void Write_NonSeekableStream_MatchesSeekableOutput()
    {
        WavAudioTranslator translator = new();
        AudioClip clip = new(CreateNoise(new AudioFormat(44100, 2), SampleFormat.Int24, 100));
        using MemoryStream seekable = new();
        using MemoryStream inner = new();

        translator.Write(seekable, clip);
        translator.Write(new NonSeekableStream(inner), clip);

        Assert.Equal(seekable.ToArray(), inner.ToArray());
    }

    [Fact]
    public void OpenDecoder_ChunkedReadsAndSeek_MatchFullBuffer()
    {
        AudioBuffer source = CreateNoise(new AudioFormat(44100, 2), SampleFormat.Int24, 1000);
        AudioClip clip = new(source);
        using AudioDecoder decoder = clip.OpenDecoder();
        using MemoryStream streamed = new();
        byte[] chunk = new byte[7 * decoder.BytesPerFrame];
        int frames;

        while ((frames = decoder.Read(chunk)) > 0)
            streamed.Write(chunk, 0, frames * decoder.BytesPerFrame);

        decoder.Seek(250);
        decoder.Read(chunk);

        Assert.Equal(source.Data.ToArray(), streamed.ToArray());
        Assert.Equal(source.GetFrames(250, 7).ToArray(), chunk);
        Assert.Equal(257, decoder.Position);
    }

    [Fact]
    public void Write_EncodedClip_PassesThroughWithoutDecoding()
    {
        CountingAudioCodec codec = new();
        WavAudioTranslator translator = new();
        translator.RegisterCodec(CountingAudioCodec.WaveFormatTag, codec);
        AudioBuffer samples = CreateNoise(new AudioFormat(44100, 2), SampleFormat.Int16, 100);
        EncodedAudio encoded = new()
        {
            Codec = codec,
            Format = samples.Format,
            Data = samples.Data,
            FrameCount = samples.FrameCount,
            BitsPerSample = 16,
            BlockAlign = 4,
        };

        AudioClip clip = translator.Read(Write(translator, new AudioClip(encoded)));

        Assert.Equal(0, codec.DecoderCount);
        Assert.False(clip.IsDecoded);
        Assert.Equal(samples.Data.ToArray(), clip.Encoded!.Data.ToArray());
        Assert.Equal(100, clip.FrameCount);

        clip.GetBuffer();
        clip.GetBuffer();

        Assert.Equal(1, codec.DecoderCount);
    }

    [Theory]
    [InlineData(typeof(ImaAdpcmCodec), ImaAdpcmCodec.WaveFormatTag, 1)]
    [InlineData(typeof(ImaAdpcmCodec), ImaAdpcmCodec.WaveFormatTag, 2)]
    [InlineData(typeof(MsAdpcmCodec), MsAdpcmCodec.WaveFormatTag, 1)]
    [InlineData(typeof(MsAdpcmCodec), MsAdpcmCodec.WaveFormatTag, 2)]
    public void Write_AdpcmCodec_EncodesPassesThroughAndDecodes(Type codecType, ushort formatTag, int channels)
    {
        AdpcmCodec codec = (AdpcmCodec)Activator.CreateInstance(codecType)!;
        WavAudioTranslator translator = new();
        translator.RegisterCodec(formatTag, codec);
        AudioBuffer source = CreateSine(new AudioFormat(22050, channels), 3000);

        byte[] encodedFile = Write(translator, new AudioClip(source), new AudioTranslatorOptions { Codec = codec });
        AudioClip encoded = translator.Read(encodedFile);

        Assert.NotNull(encoded.Encoded);
        Assert.False(encoded.IsDecoded);
        Assert.Equal(3000, encoded.FrameCount);
        Assert.Equal(encodedFile, Write(translator, encoded));

        AudioClip pcm = translator.Read(Write(translator, encoded, new AudioTranslatorOptions { SampleFormat = SampleFormat.Int16 }));

        Assert.Null(pcm.Encoded);
        Assert.Equal(3000, pcm.FrameCount);
        AssertSimilar(source.GetSamples<short>(), pcm.GetBuffer().GetSamples<short>(), 0.01);
    }

    private static byte[] Write(AudioTranslator translator, AudioClip clip) => Write(translator, clip, new AudioTranslatorOptions());

    private static byte[] Write(AudioTranslator translator, AudioClip clip, AudioTranslatorOptions options)
    {
        using MemoryStream stream = new();
        translator.Write(stream, clip, options);
        return stream.ToArray();
    }

    private static AudioBuffer CreateNoise(AudioFormat format, SampleFormat sampleFormat, int frameCount)
    {
        AudioBuffer buffer = new(format, sampleFormat, frameCount);
        new Random(1234).NextBytes(buffer.Data.Span);
        return buffer;
    }

    private static AudioBuffer CreateSine(AudioFormat format, int frameCount)
    {
        AudioBuffer buffer = new(format, SampleFormat.Int16, frameCount);
        Span<short> samples = buffer.GetSamples<short>();

        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int channel = 0; channel < format.Channels; channel++)
                samples[(frame * format.Channels) + channel] = (short)(Math.Sin(frame * (0.03 + (channel * 0.01))) * 12000);
        }

        return buffer;
    }

    private static void AssertSimilar(ReadOnlySpan<short> expected, ReadOnlySpan<short> actual, double maximumRelativeRms)
    {
        Assert.Equal(expected.Length, actual.Length);

        double error = 0;
        double signal = 0;

        for (int i = 0; i < expected.Length; i++)
        {
            error += Math.Pow(expected[i] - actual[i], 2);
            signal += Math.Pow(expected[i], 2);
        }

        Assert.True(Math.Sqrt(error / signal) < maximumRelativeRms, $"Relative RMS error {Math.Sqrt(error / signal):F4} exceeds {maximumRelativeRms}.");
    }
}
