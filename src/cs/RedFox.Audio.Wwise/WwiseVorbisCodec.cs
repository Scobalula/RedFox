using RedFox.Audio;

namespace RedFox.Audio.Wwise;

internal sealed class WwiseVorbisCodec : AudioCodec
{
    public override string Id => "wwise-vorbis";

    public override string Name => "Wwise Vorbis";

    public override bool CanDecode => true;

    public override AudioDecoder CreateDecoder(EncodedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return new WwiseVorbisDecoder(audio);
    }
}
