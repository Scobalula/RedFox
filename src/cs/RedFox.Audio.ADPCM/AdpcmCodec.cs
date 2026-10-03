// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.ADPCM;

/// <summary>
/// Base class for ADPCM (Adaptive Differential Pulse Code Modulation) codec implementations.
/// Provides common decoding and encoding operations for block-based ADPCM codecs.
/// </summary>
public abstract class AdpcmCodec : AudioCodec
{
    /// <inheritdoc/>
    public override bool CanDecode => true;

    /// <inheritdoc/>
    public override bool CanEncode => true;

    /// <summary>
    /// Gets a reasonable default block alignment for the specified audio format.
    /// </summary>
    /// <param name="format">The audio format.</param>
    /// <returns>The recommended block alignment in bytes.</returns>
    public static int GetDefaultBlockAlign(AudioFormat format) => 256 * format.Channels * Math.Max(1, format.SampleRate / 11025);

    /// <summary>
    /// Gets the number of audio frames stored in a block of the specified size.
    /// </summary>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="blockAlign">The block size in bytes.</param>
    /// <returns>The number of frames in the block.</returns>
    public abstract int GetFramesPerBlock(int channels, int blockAlign);

    /// <summary>
    /// Creates codec-specific setup data for the specified frames-per-block value.
    /// </summary>
    /// <param name="framesPerBlock">The number of frames per block.</param>
    /// <returns>The setup data bytes.</returns>
    public abstract byte[] CreateSetup(int framesPerBlock);

    /// <summary>
    /// Decodes a single block of ADPCM-compressed data into PCM samples.
    /// </summary>
    /// <param name="block">The compressed block data.</param>
    /// <param name="output">The buffer to fill with decoded PCM samples.</param>
    /// <param name="channels">The number of audio channels.</param>
    public abstract void DecodeBlock(ReadOnlySpan<byte> block, Span<short> output, int channels);

    /// <summary>
    /// Encodes a block of PCM samples into ADPCM-compressed data.
    /// </summary>
    /// <param name="samples">The PCM samples to encode.</param>
    /// <param name="block">The buffer to fill with compressed data.</param>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="channelState">The encoder state for each channel (updated during encoding).</param>
    public abstract void EncodeBlock(ReadOnlySpan<short> samples, Span<byte> block, int channels, Span<int> channelState);

    /// <inheritdoc/>
    public override AudioDecoder CreateDecoder(EncodedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ValidateChannels(audio.Format.Channels);

        if (audio.BlockAlign <= 0)
            throw new AudioException($"{Name} audio requires a positive block alignment.");

        return new AdpcmDecoder(this, audio);
    }

    /// <inheritdoc/>
    public override AudioEncoder CreateEncoder(AudioFormat format, AudioEncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateChannels(format.Channels);

        return new AdpcmEncoder(this, format, GetDefaultBlockAlign(format));
    }

    private void ValidateChannels(int channels)
    {
        if (channels is not (1 or 2))
            throw new AudioException($"{Name} supports mono and stereo audio only.");
    }
}
