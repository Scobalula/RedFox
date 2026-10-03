using RedFox.Audio;

namespace RedFox.Tests.Audio;

public sealed class ForwardOnlyAudioCodec : AudioCodec
{
    public override string Id => "forward-only";

    public override string Name => "Forward Only";

    public override bool CanDecode => true;

    public override AudioDecoder CreateDecoder(EncodedAudio audio) => new ForwardOnlyAudioDecoder(new AudioClip(new AudioBuffer(audio.Format, SampleFormat.Int16, audio.Data.ToArray())).OpenDecoder());
}
