using RedFox.Audio.ADPCM;
using RedFox.Audio.Flac;
using RedFox.Audio.IO;
using RedFox.Audio.IO.Wav;

namespace RedFox.GameExtraction;

/// <summary>
/// Owns the audio translator manager used for audio import and export, pre-populated with the built-in formats.
/// </summary>
public class AudioTranslatorService
{
    /// <summary>
    /// Gets the manager responsible for audio translation operations.
    /// </summary>
    public AudioTranslatorManager Manager { get; } = new AudioTranslatorManager();

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioTranslatorService"/> class and registers the WAV (with MS and IMA ADPCM) and FLAC formats.
    /// </summary>
    public AudioTranslatorService()
    {
        WavAudioTranslator wav = new();
        wav.RegisterCodec(MsAdpcmCodec.WaveFormatTag, new MsAdpcmCodec());
        wav.RegisterCodec(ImaAdpcmCodec.WaveFormatTag, new ImaAdpcmCodec());

        Manager.Register(wav);
        Manager.Register(new FlacAudioTranslator());
    }
}
