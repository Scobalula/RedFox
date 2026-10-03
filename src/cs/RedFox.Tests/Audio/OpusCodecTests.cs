using System.Runtime.InteropServices;
using RedFox.Audio;
using RedFox.Audio.Opus;

namespace RedFox.Tests.Audio;

public sealed class OpusCodecTests
{
    private const int SampleRate = 48000;
    private const int PacketFrames = 960;
    private const int FrameCount = 48000;

    [Fact]
    public void GetBuffer_EncodedStream_DecodesWithoutPreSkipAndTrimsToFrameCount()
    {
        (EncodedAudio encoded, short[] source) = EncodeSine(2);

        AudioBuffer buffer = new AudioClip(encoded).GetBuffer();

        Assert.Equal(SampleFormat.Float32, buffer.SampleFormat);
        Assert.Equal(FrameCount, buffer.FrameCount);
        AssertSimilar(source, buffer.GetSamples<float>(), 0, FrameCount * 2, 0.1);
    }

    [Fact]
    public void Seek_MiddleOfStream_MatchesSequentialDecode()
    {
        (EncodedAudio encoded, _) = EncodeSine(2);
        float[] sequential = new AudioClip(encoded).GetBuffer().GetSamples<float>().ToArray();
        using AudioDecoder decoder = new OpusCodec().CreateDecoder(encoded);
        float[] chunk = new float[PacketFrames * 2];

        decoder.Seek(20000);
        int frames = decoder.Read(MemoryMarshal.AsBytes(chunk.AsSpan()));

        Assert.Equal(PacketFrames, frames);
        Assert.Equal(20000 + PacketFrames, decoder.Position);
        AssertSimilar(sequential.AsSpan(20000 * 2, chunk.Length), chunk, 0.02);
    }

    [Fact]
    public void OpusHeader_SurroundFamily_RoundTripsThroughBytes()
    {
        OpusHeader header = OpusHeader.Create(6, 312, 1);

        OpusHeader parsed = OpusHeader.Parse(header.ToBytes());

        Assert.Equal(6, parsed.Channels);
        Assert.Equal(312, parsed.PreSkip);
        Assert.Equal(1, parsed.MappingFamily);
        Assert.Equal(4, parsed.StreamCount);
        Assert.Equal(2, parsed.CoupledStreamCount);
        Assert.Equal([0, 4, 1, 2, 3, 5], parsed.ChannelMapping);
    }

    private static (EncodedAudio Encoded, short[] Source) EncodeSine(int channels)
    {
        short[] source = new short[FrameCount * channels];

        for (int frame = 0; frame < FrameCount; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
                source[(frame * channels) + channel] = (short)(Math.Sin(2 * Math.PI * (440 + (channel * 220)) * frame / SampleRate) * 12000);
        }

        using OpusEncoder encoder = new(SampleRate, channels, OpusApplication.Audio) { Bitrate = 128000 };
        using MemoryStream data = new();
        List<AudioPacket> packets = [];
        short[] padded = new short[(FrameCount + (2 * PacketFrames)) * channels];
        byte[] packet = new byte[4000];

        source.CopyTo(padded, 0);

        for (int frame = 0; frame < FrameCount + encoder.Lookahead; frame += PacketFrames)
        {
            int length = encoder.Encode(padded.AsSpan(frame * channels, PacketFrames * channels), PacketFrames, packet);
            packets.Add(new AudioPacket((int)data.Length, length));
            data.Write(packet, 0, length);
        }

        EncodedAudio encoded = new()
        {
            Codec = new OpusCodec(),
            Format = new AudioFormat(SampleRate, channels),
            Data = data.ToArray(),
            FrameCount = FrameCount,
            Setup = OpusHeader.Create(channels, encoder.Lookahead, 0).ToBytes(),
            Packets = packets.ToArray(),
        };

        return (encoded, source);
    }

    private static void AssertSimilar(short[] expected, ReadOnlySpan<float> actual, int start, int count, double maximumRelativeRms)
    {
        float[] reference = new float[count];

        for (int i = 0; i < count; i++)
            reference[i] = expected[start + i] / 32768.0f;

        AssertSimilar(reference, actual.Slice(start, count), maximumRelativeRms);
    }

    private static void AssertSimilar(ReadOnlySpan<float> expected, ReadOnlySpan<float> actual, double maximumRelativeRms)
    {
        double error = 0;
        double signal = 0;

        for (int i = 0; i < expected.Length; i++)
        {
            error += Math.Pow(expected[i] - actual[i], 2);
            signal += Math.Pow(expected[i], 2);
        }

        double relative = Math.Sqrt(error / signal);
        Assert.True(relative < maximumRelativeRms, $"Relative RMS error {relative:F4} exceeds {maximumRelativeRms}.");
    }
}
