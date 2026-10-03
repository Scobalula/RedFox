using RedFox.Audio;

namespace RedFox.Tests.Audio;

public sealed class AudioClipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenDecoder_AtFrame_StartsAtThatFrame(bool forwardOnly)
    {
        AudioBuffer samples = new(new AudioFormat(44100, 2), SampleFormat.Int16, 20000);
        new Random(42).NextBytes(samples.Data.Span);
        EncodedAudio encoded = new()
        {
            Codec = new ForwardOnlyAudioCodec(),
            Format = samples.Format,
            Data = samples.Data,
            FrameCount = samples.FrameCount,
        };
        AudioClip clip = forwardOnly ? new AudioClip(encoded) : new AudioClip(samples);
        byte[] chunk = new byte[100 * samples.BytesPerFrame];

        using AudioDecoder decoder = clip.OpenDecoder(12345);
        int frames = decoder.Read(chunk);

        Assert.Equal(forwardOnly, !decoder.CanSeek);
        Assert.Equal(100, frames);
        Assert.Equal(12445, decoder.Position);
        Assert.Equal(samples.GetFrames(12345, 100).ToArray(), chunk);
    }
}
