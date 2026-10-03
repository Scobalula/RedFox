// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Flac;

/// <summary>
/// Codec implementation for FLAC (Free Lossless Audio Codec), backed by native libFLAC.
/// Encoded audio stores the <c>fLaC</c> marker and metadata blocks in <see cref="EncodedAudio.Setup"/>
/// and the FLAC frames in <see cref="EncodedAudio.Data"/>.
/// </summary>
public sealed class FlacCodec : AudioCodec
{
    private const int DefaultBitsPerSample = 16;

    private const int DefaultCompressionLevel = 5;

    private const int MaxCompressionLevel = 8;

    /// <inheritdoc/>
    public override string Id => "flac";

    /// <inheritdoc/>
    public override string Name => "FLAC";

    /// <inheritdoc/>
    public override bool CanDecode => true;

    /// <inheritdoc/>
    public override bool CanEncode => true;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when the supplied audio is null.</exception>
    public override AudioDecoder CreateDecoder(EncodedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return new FlacDecoder(audio);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when the supplied options are null.</exception>
    public override AudioEncoder CreateEncoder(AudioFormat format, AudioEncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        int bitsPerSample = (options as FlacEncoderOptions)?.BitsPerSample ?? DefaultBitsPerSample;
        int compressionLevel = Math.Clamp(options.Quality ?? DefaultCompressionLevel, 0, MaxCompressionLevel);

        return new FlacEncoder(format, bitsPerSample, compressionLevel);
    }

    internal static SampleFormat GetSampleFormat(int bitsPerSample) => bitsPerSample switch
    {
        <= 8 => SampleFormat.UInt8,
        <= 16 => SampleFormat.Int16,
        <= 24 => SampleFormat.Int24,
        _ => SampleFormat.Int32,
    };
}
