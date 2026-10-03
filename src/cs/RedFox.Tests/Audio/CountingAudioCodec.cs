using RedFox.Audio;

namespace RedFox.Tests.Audio;

public sealed class CountingAudioCodec : AudioCodec
{
    public const ushort WaveFormatTag = 0x7777;

    public int DecoderCount { get; private set; }

    public override string Id => "counting";

    public override string Name => "Counting";

    public override bool CanDecode => true;

    public override AudioDecoder CreateDecoder(EncodedAudio audio)
    {
        DecoderCount++;
        return new AudioClip(new AudioBuffer(audio.Format, SampleFormat.Int16, audio.Data.ToArray())).OpenDecoder();
    }
}
