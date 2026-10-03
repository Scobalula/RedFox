using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RedFox.Audio;
using RedFox.Audio.Flac;
using RedFox.Audio.IO;
using RedFox.Audio.IO.Wav;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;

namespace RedFox.Tests.Audio;

public sealed class FlacAudioTranslatorTests
{
    [Theory]
    [InlineData(SampleFormat.UInt8, 1)]
    [InlineData(SampleFormat.Int16, 2)]
    [InlineData(SampleFormat.Int24, 2)]
    [InlineData(SampleFormat.Int32, 1)]
    [InlineData(SampleFormat.Int16, 6)]
    [InlineData(SampleFormat.Int16, 8)]
    public void WriteRead_Pcm_RoundTripsLosslessly(SampleFormat sampleFormat, int channels)
    {
        AudioBuffer source = CreateSignal(new AudioFormat(48000, channels), sampleFormat, 10000);
        FlacAudioTranslator translator = new();

        AudioClip clip = translator.Read(Write(translator, new AudioClip(source)));
        AudioBuffer result = clip.GetBuffer();

        Assert.IsType<FlacCodec>(clip.Encoded!.Codec);
        Assert.Equal(source.FrameCount, clip.FrameCount);
        Assert.Equal(source.Format, result.Format);
        Assert.Equal(sampleFormat, result.SampleFormat);
        Assert.Equal(source.Data.ToArray(), result.Data.ToArray());
    }

    [Fact]
    public void Write_Pcm_StoresTotalSamplesAndMd5()
    {
        AudioBuffer source = CreateSignal(new AudioFormat(44100, 2), SampleFormat.Int16, 20000);
        FlacAudioTranslator translator = new();

        AudioClip clip = translator.Read(Write(translator, new AudioClip(source)));
        FlacStreamInfo info = FlacMetadata.Parse(clip.Encoded!.Setup).StreamInfo;

        Assert.Equal(20000, info.TotalSamples);
        Assert.Equal(16, info.BitsPerSample);
        Assert.Equal(BinaryPrimitives.ReadUInt128BigEndian(MD5.HashData(source.Data.Span)), info.Md5);
        Assert.InRange(info.MinFrameSize, 1, info.MaxFrameSize);
    }

    [Fact]
    public void WriteRead_ValidBitsPerSample_IsStoredAsFlacBitDepth()
    {
        AudioBuffer source = CreateSignal(new AudioFormat(96000, 2), SampleFormat.Int24, 5000);
        Span<byte> data = source.Data.Span;

        for (int i = 0; i < data.Length; i += 3)
            data[i] &= 0xF0;

        AudioBuffer twentyBit = new(source.Format, SampleFormat.Int24, source.Data, 20);
        FlacAudioTranslator translator = new();

        AudioClip clip = translator.Read(Write(translator, new AudioClip(twentyBit)));
        AudioBuffer result = clip.GetBuffer();

        Assert.Equal(20, clip.Encoded!.BitsPerSample);
        Assert.Equal(20, result.ValidBitsPerSample);
        Assert.Equal(source.Data.ToArray(), result.Data.ToArray());
    }

    [Fact]
    public void ReadWrite_EncodedClip_PassesThroughByteIdentical()
    {
        FlacAudioTranslator translator = new();
        byte[] file = Write(translator, new AudioClip(CreateSignal(new AudioFormat(44100, 2), SampleFormat.Int16, 5000)) { LoopStart = 10, LoopEnd = 4000 });

        AudioClip clip = translator.Read(file);

        Assert.Equal(file, Write(translator, clip));
        Assert.False(clip.IsDecoded);
    }

    [Fact]
    public void WriteRead_LoopPointsAndLayout_ArePreserved()
    {
        AudioFormat format = new(44100, 2, ChannelLayout.SideLeft | ChannelLayout.SideRight);
        FlacAudioTranslator translator = new();
        AudioClip source = new(CreateSignal(format, SampleFormat.Int16, 2000))
        {
            LoopStart = 100,
            LoopEnd = 1500,
        };

        AudioClip clip = translator.Read(Write(translator, source));

        Assert.Equal(format, clip.Format);
        Assert.Equal(100, clip.LoopStart);
        Assert.Equal(1500, clip.LoopEnd);
    }

    [Fact]
    public void WriteRead_Tags_RoundTripAndPassThroughUnchanged()
    {
        FlacAudioTranslator translator = new();
        AudioClip source = new(CreateSignal(new AudioFormat(44100, 2), SampleFormat.Int16, 2000)) { LoopStart = 1, LoopEnd = 2 };
        source.Tags["TITLE"] = "Théme";
        source.Tags["Artist"] = "RedFox";

        byte[] file = Write(translator, source);
        AudioClip clip = translator.Read(file);

        Assert.Equal(2, clip.Tags.Count);
        Assert.Equal("Théme", clip.Tags["title"]);
        Assert.Equal("RedFox", clip.Tags["ARTIST"]);
        Assert.Equal(file, Write(translator, clip));

        clip.Tags.Remove("ARTIST");
        AudioClip result = translator.Read(Write(translator, clip));

        Assert.Equal(["TITLE"], result.Tags.Keys);
        Assert.Equal(1, result.LoopStart);
    }

    [Fact]
    public void Manager_FlacToWavToFlac_KeepsTags()
    {
        AudioTranslatorManager manager = new AudioTranslatorService().Manager;
        AudioClip source = new(CreateSignal(new AudioFormat(22050, 1), SampleFormat.Int16, 500));
        source.Tags["TITLE"] = "Intro";
        source.Tags["COMMENT"] = "Looping";
        source.Tags["DISCNUMBER"] = "2";

        AudioClip flac = ReadBack(manager, source, "clip.flac");
        AudioClip wav = ReadBack(manager, flac, "clip.wav");
        AudioClip result = ReadBack(manager, wav, "clip.flac");

        Assert.Equal(3, flac.Tags.Count);
        Assert.Equal(2, wav.Tags.Count);
        Assert.Equal("Intro", result.Tags["TITLE"]);
        Assert.Equal("Looping", result.Tags["COMMENT"]);
    }

    [Fact]
    public void ExportClip_WritesEveryFormat()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        AudioTranslatorManager manager = new AudioTranslatorService().Manager;
        AudioBuffer source = CreateSignal(new AudioFormat(22050, 2), SampleFormat.Int16, 500);
        AudioClip clip = ReadBack(manager, new AudioClip(source), "clip.flac");

        try
        {
            AudioClipHandler.ExportClip(clip, [".wav", ".flac"], manager, Path.Combine(directory, "sub", "clip.snd"), true);

            Assert.Null(manager.Read(Path.Combine(directory, "sub", "clip.wav")).Encoded);
            Assert.Equal(source.Data.ToArray(), manager.Read(Path.Combine(directory, "sub", "clip.flac")).GetBuffer().Data.ToArray());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Write_PassthroughWithChangedLoop_UpdatesLoopWithoutReencoding()
    {
        FlacAudioTranslator translator = new();
        AudioClip clip = translator.Read(Write(translator, new AudioClip(CreateSignal(new AudioFormat(44100, 1), SampleFormat.Int16, 3000))));
        ReadOnlyMemory<byte> frames = clip.Encoded!.Data;

        clip.LoopStart = 5;
        clip.LoopEnd = 2500;

        AudioClip result = translator.Read(Write(translator, clip));

        Assert.Equal(5, result.LoopStart);
        Assert.Equal(2500, result.LoopEnd);
        Assert.Equal(frames.ToArray(), result.Encoded!.Data.ToArray());
    }

    [Fact]
    public void Write_ReencodedFlac_PreservesForeignMetadataBlocks()
    {
        FlacAudioTranslator translator = new();
        AudioBuffer source = CreateSignal(new AudioFormat(48000, 2), SampleFormat.Int24, 4000);
        byte[] application = [.. "RFOX"u8, 1, 2, 3, 4];
        AudioClip original = translator.Read(Write(translator, new AudioClip(source)));
        FlacMetadata tagged = FlacMetadata.Parse(original.Encoded!.Setup);
        int blockCount = tagged.Blocks.Count;

        tagged.Blocks.Add(new FlacMetadataBlock(2, application));

        using MemoryStream file = new();
        tagged.Write(file);
        file.Write(original.Encoded.Data.Span);

        AudioClip reencoded = translator.Read(Write(translator, translator.Read(file.ToArray()), new AudioTranslatorOptions { SampleFormat = SampleFormat.Int16 }));
        FlacMetadata result = FlacMetadata.Parse(reencoded.Encoded!.Setup);

        Assert.Equal(16, result.StreamInfo.BitsPerSample);
        Assert.Equal(4000, result.StreamInfo.TotalSamples);
        Assert.Equal(application, result.Blocks[result.IndexOf(2)].Data.ToArray());
        Assert.Equal(blockCount + 1, result.Blocks.Count);
        Assert.Equal(source.Convert(SampleFormat.Int16).Data.ToArray(), reencoded.GetBuffer().Data.ToArray());
    }

    [Fact]
    public void OpenDecoder_SeekMatchesSequentialDecode()
    {
        AudioBuffer source = CreateSignal(new AudioFormat(44100, 2), SampleFormat.Int16, 30000);
        FlacAudioTranslator translator = new();
        AudioClip clip = translator.Read(Write(translator, new AudioClip(source)));
        byte[] chunk = new byte[1000 * 4];

        using AudioDecoder decoder = clip.OpenDecoder(12345);
        int frames = decoder.Read(chunk);

        Assert.Equal(1000, frames);
        Assert.Equal(13345, decoder.Position);
        Assert.Equal(source.GetFrames(12345, 1000).ToArray(), chunk);

        decoder.Seek(30000);

        Assert.Equal(0, decoder.Read(chunk));

        decoder.Seek(0);
        decoder.Read(chunk);

        Assert.Equal(source.GetFrames(0, 1000).ToArray(), chunk);
    }

    [Fact]
    public void Codec_EncoderAndDecoder_WorkWithoutTranslator()
    {
        AudioBuffer source = CreateSignal(new AudioFormat(32000, 2), SampleFormat.Int16, 9000);
        FlacCodec codec = new();
        PacketCollector packets = new();

        using AudioEncoder encoder = codec.CreateEncoder(source.Format);

        encoder.Encode(source.Data.Span, packets);
        encoder.Complete(packets);

        EncodedAudio encoded = new()
        {
            Codec = codec,
            Format = source.Format,
            Data = packets.Data.ToArray(),
            Setup = encoder.Setup.ToArray(),
        };

        Assert.Equal(9000, packets.FrameCount);
        Assert.Equal(source.Data.ToArray(), new AudioClip(encoded).GetBuffer().Data.ToArray());
    }

    [Fact]
    public void Manager_SelectsFlacByHeaderAndExtension()
    {
        AudioTranslatorManager manager = new();
        manager.Register(new WavAudioTranslator());
        manager.Register(new FlacAudioTranslator());
        AudioBuffer source = CreateSignal(new AudioFormat(22050, 1), SampleFormat.Int16, 1000);
        using MemoryStream flac = new();
        using MemoryStream wav = new();

        manager.Write(flac, "clip.flac", new AudioClip(source));
        flac.Position = 0;
        AudioClip clip = manager.Read(flac, "clip.bin");
        manager.Write(wav, "clip.wav", clip);
        wav.Position = 0;
        AudioClip pcm = manager.Read(wav, "clip.wav");

        Assert.IsType<FlacCodec>(clip.Encoded!.Codec);
        Assert.Null(pcm.Encoded);
        Assert.Equal(source.Data.ToArray(), pcm.GetBuffer().Data.ToArray());
    }

    [Fact]
    public void Write_NonSeekableStream_MatchesSeekableOutput()
    {
        FlacAudioTranslator translator = new();
        AudioClip clip = new(CreateSignal(new AudioFormat(44100, 2), SampleFormat.Int16, 3000));
        using MemoryStream seekable = new();
        using MemoryStream inner = new();

        translator.Write(seekable, clip);
        translator.Write(new NonSeekableStream(inner), clip);

        Assert.Equal(seekable.ToArray(), inner.ToArray());
    }

    [Fact]
    public void Write_ForeignCodec_Throws()
    {
        FlacAudioTranslator translator = new();
        AudioClip clip = new(CreateSignal(new AudioFormat(44100, 1), SampleFormat.Int16, 100));

        Assert.Throws<AudioException>(() => Write(translator, clip, new AudioTranslatorOptions { Codec = new CountingAudioCodec() }));
    }

    private static byte[] Write(AudioTranslator translator, AudioClip clip) => Write(translator, clip, new AudioTranslatorOptions());

    private static byte[] Write(AudioTranslator translator, AudioClip clip, AudioTranslatorOptions options)
    {
        using MemoryStream stream = new();
        translator.Write(stream, clip, options);
        return stream.ToArray();
    }

    private static AudioClip ReadBack(AudioTranslatorManager manager, AudioClip clip, string fileName)
    {
        using MemoryStream stream = new();
        manager.Write(stream, fileName, clip);
        stream.Position = 0;
        return manager.Read(stream, fileName);
    }

    private static AudioBuffer CreateSignal(AudioFormat format, SampleFormat sampleFormat, int frameCount)
    {
        double[] samples = new double[frameCount * format.Channels];
        Random random = new(1234);

        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int channel = 0; channel < format.Channels; channel++)
                samples[(frame * format.Channels) + channel] = (Math.Sin(frame * (0.02 + (channel * 0.01))) * 0.6) + ((random.NextDouble() - 0.5) * 0.1);
        }

        AudioBuffer buffer = new(format, sampleFormat, frameCount);
        SampleConverter.Convert(MemoryMarshal.AsBytes(samples.AsSpan()), SampleFormat.Float64, buffer.Data.Span, sampleFormat);

        return buffer;
    }
}
