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
/// Audio codec for Microsoft ADPCM (MS-ADPCM), a block-based adaptive differential
/// pulse-code modulation format commonly found in WAV files.
/// Supports mono and stereo encoding and decoding.
/// </summary>
public sealed class MsAdpcmCodec : AudioCodec
{
    private static readonly (short Coeff1, short Coeff2)[] CoefficientSets =
    [
        (256, 0), (512, -256), (0, 0), (192, 64),
        (240, 0), (460, -208), (392, -232),
    ];

    private static readonly int[] AdaptationTable =
    [
        230, 230, 230, 230, 307, 409, 512, 614,
        768, 614, 512, 409, 307, 230, 230, 230,
    ];

    /// <inheritdoc/>
    public override string Name => "MS-ADPCM";

    /// <inheritdoc/>
    public override AudioCodecFlags Flags => AudioCodecFlags.SupportsEncoding | AudioCodecFlags.SupportsDecoding;

    /// <summary>
    /// Gets the number of decoded PCM samples per block for the given format.
    /// </summary>
    /// <param name="format">The audio format describing the block structure.</param>
    /// <returns>The number of interleaved samples produced per block.</returns>
    public static int GetSamplesPerBlock(AudioFormat format)
    {
        var headerSize = 7 * format.Channels;
        var dataBytes = format.BlockAlign - headerSize;
        var samplesPerChannel = 2 + dataBytes * 2;

        return samplesPerChannel * format.Channels;
    }

    /// <inheritdoc/>
    public override int GetMaxDecodedSize(int encodedSize, AudioFormat format)
    {
        if (format.BlockAlign <= 0)
            return 0;

        var blocks = encodedSize / format.BlockAlign;
        return blocks * GetSamplesPerBlock(format);
    }

    /// <inheritdoc/>
    public override int GetMaxEncodedSize(int sampleCount, AudioFormat format)
    {
        var samplesPerBlock = GetSamplesPerBlock(format);
        if (samplesPerBlock <= 0)
            return 0;

        var blocks = (sampleCount + samplesPerBlock - 1) / samplesPerBlock;
        return blocks * format.BlockAlign;
    }

    /// <inheritdoc/>
    public override int Decode(ReadOnlySpan<byte> source, Span<short> destination, AudioFormat format)
    {
        var blockAlign = format.BlockAlign;
        var channels = format.Channels;
        var blockCount = source.Length / blockAlign;
        var written = 0;

        for (var b = 0; b < blockCount; b++)
        {
            var blockOffset = b * blockAlign;
            var block = source.Slice(blockOffset, blockAlign);

            if (channels == 1)
            {
                written += DecodeMonoBlock(block, destination[written..]);
            }
            else
            {
                written += DecodeStereoBlock(block, destination[written..]);
            }
        }

        return written;
    }

    /// <inheritdoc/>
    public override int Encode(ReadOnlySpan<short> source, Span<byte> destination, AudioFormat format)
    {
        var blockAlign = format.BlockAlign;
        var channels = format.Channels;
        var samplesPerBlock = GetSamplesPerBlock(format);
        var written = 0;
        var samplePos = 0;

        while (samplePos < source.Length)
        {
            var remaining = source.Length - samplePos;
            var blockSamples = Math.Min(remaining, samplesPerBlock);

            if (written + blockAlign > destination.Length)
                break;

            if (channels == 1)
            {
                EncodeMonoBlock(
                    source.Slice(samplePos, blockSamples),
                    destination.Slice(written, blockAlign));
            }
            else
            {
                EncodeStereoBlock(
                    source.Slice(samplePos, blockSamples),
                    destination.Slice(written, blockAlign));
            }

            samplePos += blockSamples;
            written += blockAlign;
        }

        return written;
    }

    private static int DecodeMonoBlock(ReadOnlySpan<byte> block, Span<short> output)
    {
        var predictorIdx = Math.Clamp((int)block[0], 0, 6);
        var (coeff1, coeff2) = CoefficientSets[predictorIdx];
        var delta = (short)(block[1] | (block[2] << 8));
        var sample1 = (short)(block[3] | (block[4] << 8));
        var sample2 = (short)(block[5] | (block[6] << 8));

        var written = 0;
        output[written++] = sample2;
        output[written++] = sample1;

        var pos = 7;

        while (pos < block.Length && written < output.Length)
        {
            var nibble = block[pos] & 0x0F;
            DecodeSample(nibble, coeff1, coeff2, ref sample1, ref sample2, ref delta);
            if (written < output.Length)
                output[written++] = sample1;

            if (pos < block.Length && written < output.Length)
            {
                nibble = (block[pos] >> 4) & 0x0F;
                DecodeSample(nibble, coeff1, coeff2, ref sample1, ref sample2, ref delta);
                if (written < output.Length)
                    output[written++] = sample1;
            }

            pos++;
        }

        return written;
    }

    private static int DecodeStereoBlock(ReadOnlySpan<byte> block, Span<short> output)
    {
        var leftPredIdx = Math.Clamp((int)block[0], 0, 6);
        var (leftCoeff1, leftCoeff2) = CoefficientSets[leftPredIdx];
        var leftDelta = (short)(block[1] | (block[2] << 8));
        var leftSample1 = (short)(block[3] | (block[4] << 8));
        var leftSample2 = (short)(block[5] | (block[6] << 8));

        var rightPredIdx = Math.Clamp((int)block[7], 0, 6);
        var (rightCoeff1, rightCoeff2) = CoefficientSets[rightPredIdx];
        var rightDelta = (short)(block[8] | (block[9] << 8));
        var rightSample1 = (short)(block[10] | (block[11] << 8));
        var rightSample2 = (short)(block[12] | (block[13] << 8));

        var written = 0;
        output[written++] = leftSample2;
        output[written++] = rightSample2;
        output[written++] = leftSample1;
        output[written++] = rightSample1;

        var pos = 14;

        while (pos < block.Length && written + 1 < output.Length)
        {
            var byteVal = block[pos];

            var leftNibble = byteVal & 0x0F;
            DecodeSample(leftNibble, leftCoeff1, leftCoeff2, ref leftSample1, ref leftSample2, ref leftDelta);
            output[written++] = leftSample1;

            var rightNibble = (byteVal >> 4) & 0x0F;
            DecodeSample(rightNibble, rightCoeff1, rightCoeff2, ref rightSample1, ref rightSample2, ref rightDelta);
            output[written++] = rightSample1;

            pos++;
        }

        return written;
    }

    private static void DecodeSample(int nibble, short coeff1, short coeff2, ref short sample1, ref short sample2, ref short delta)
    {
        var signed = nibble >= 8 ? nibble - 16 : nibble;
        var predicted = (coeff1 * sample1 + coeff2 * sample2) >> 8;
        var raw = signed * delta + predicted;
        var decoded = (short)Math.Clamp(raw, -32768, 32767);

        sample2 = sample1;
        sample1 = decoded;

        var newDelta = (delta * AdaptationTable[nibble]) >> 8;
        delta = (short)Math.Clamp(newDelta, 16, 32767);
    }

    private static byte EncodeNibble(short target, short coeff1, short coeff2, ref short sample1, ref short sample2, ref short delta)
    {
        var predicted = (coeff1 * sample1 + coeff2 * sample2) >> 8;
        var error = target - predicted;
        var nibble = delta != 0 ? Math.Clamp(error / delta, -8, 7) : 0;

        if (nibble < 0)
            nibble += 16;

        DecodeSample(nibble, coeff1, coeff2, ref sample1, ref sample2, ref delta);
        return (byte)nibble;
    }

    private static void EncodeMonoBlock(ReadOnlySpan<short> samples, Span<byte> block)
    {
        var coeff1 = CoefficientSets[0].Coeff1;
        var coeff2 = CoefficientSets[0].Coeff2;
        var delta = (short)16;
        var sample1 = samples.Length > 1 ? samples[1] : samples[0];
        var sample2 = samples[0];

        block[0] = 0;
        block[1] = (byte)(delta & 0xFF);
        block[2] = (byte)((delta >> 8) & 0xFF);
        block[3] = (byte)(sample1 & 0xFF);
        block[4] = (byte)((sample1 >> 8) & 0xFF);
        block[5] = (byte)(sample2 & 0xFF);
        block[6] = (byte)((sample2 >> 8) & 0xFF);

        var inPos = 2;
        var outPos = 7;

        while (outPos < block.Length && inPos < samples.Length)
        {
            var low = EncodeNibble(samples[inPos++], coeff1, coeff2, ref sample1, ref sample2, ref delta);

            var high = (byte)0;
            if (inPos < samples.Length)
                high = EncodeNibble(samples[inPos++], coeff1, coeff2, ref sample1, ref sample2, ref delta);

            block[outPos++] = (byte)(low | (high << 4));
        }

        while (outPos < block.Length)
            block[outPos++] = 0;
    }

    private static void EncodeStereoBlock(ReadOnlySpan<short> samples, Span<byte> block)
    {
        var (leftCoeff1, leftCoeff2) = CoefficientSets[0];
        var leftDelta = (short)16;
        var leftSample1 = samples.Length > 2 ? samples[2] : (samples.Length > 0 ? samples[0] : (short)0);
        var leftSample2 = samples.Length > 0 ? samples[0] : (short)0;

        var (rightCoeff1, rightCoeff2) = CoefficientSets[0];
        var rightDelta = (short)16;
        var rightSample1 = samples.Length > 3 ? samples[3] : (samples.Length > 1 ? samples[1] : (short)0);
        var rightSample2 = samples.Length > 1 ? samples[1] : (short)0;

        block[0] = 0;
        block[1] = (byte)(leftDelta & 0xFF);
        block[2] = (byte)((leftDelta >> 8) & 0xFF);
        block[3] = (byte)(leftSample1 & 0xFF);
        block[4] = (byte)((leftSample1 >> 8) & 0xFF);
        block[5] = (byte)(leftSample2 & 0xFF);
        block[6] = (byte)((leftSample2 >> 8) & 0xFF);

        block[7] = 0;
        block[8] = (byte)(rightDelta & 0xFF);
        block[9] = (byte)((rightDelta >> 8) & 0xFF);
        block[10] = (byte)(rightSample1 & 0xFF);
        block[11] = (byte)((rightSample1 >> 8) & 0xFF);
        block[12] = (byte)(rightSample2 & 0xFF);
        block[13] = (byte)((rightSample2 >> 8) & 0xFF);

        var inPos = 4;
        var outPos = 14;

        while (outPos < block.Length && inPos + 1 < samples.Length)
        {
            var left = EncodeNibble(samples[inPos++], leftCoeff1, leftCoeff2, ref leftSample1, ref leftSample2, ref leftDelta);
            var right = EncodeNibble(samples[inPos++], rightCoeff1, rightCoeff2, ref rightSample1, ref rightSample2, ref rightDelta);
            block[outPos++] = (byte)(left | (right << 4));
        }

        while (outPos < block.Length)
            block[outPos++] = 0;
    }
}
