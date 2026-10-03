// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Audio.ADPCM;

/// <summary>
/// Codec implementation for MS-ADPCM (Microsoft Adaptive Differential Pulse Code Modulation) audio format.
/// Supports both encoding and decoding of MS-ADPCM audio with mono and stereo variants.
/// </summary>
public sealed class MsAdpcmCodec : AdpcmCodec
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

    /// <summary>
    /// The RIFF WAVE format tag for MS-ADPCM (0x02).
    /// </summary>
    public const ushort WaveFormatTag = 0x02;

    /// <inheritdoc/>
    public override string Id => "ms-adpcm";

    /// <inheritdoc/>
    public override string Name => "MS-ADPCM";

    /// <inheritdoc/>
    public override int GetFramesPerBlock(int channels, int blockAlign) => channels == 1 ? 2 + ((blockAlign - 7) * 2) : 2 + (blockAlign - 14);

    /// <inheritdoc/>
    public override byte[] CreateSetup(int framesPerBlock)
    {
        byte[] setup = new byte[4 + (CoefficientSets.Length * 4)];

        BinaryPrimitives.WriteUInt16LittleEndian(setup, (ushort)framesPerBlock);
        BinaryPrimitives.WriteUInt16LittleEndian(setup.AsSpan(2), (ushort)CoefficientSets.Length);

        for (int i = 0; i < CoefficientSets.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(setup.AsSpan(4 + (i * 4)), CoefficientSets[i].Coeff1);
            BinaryPrimitives.WriteInt16LittleEndian(setup.AsSpan(6 + (i * 4)), CoefficientSets[i].Coeff2);
        }

        return setup;
    }

    /// <inheritdoc/>
    public override void DecodeBlock(ReadOnlySpan<byte> block, Span<short> output, int channels)
    {
        if (channels == 1)
            DecodeMonoBlock(block, output);
        else
            DecodeStereoBlock(block, output);
    }

    /// <inheritdoc/>
    public override void EncodeBlock(ReadOnlySpan<short> samples, Span<byte> block, int channels, Span<int> channelState)
    {
        block.Clear();

        for (int channel = 0; channel < channels; channel++)
        {
            short initialDelta = (short)Math.Clamp(channelState[channel], 16, short.MaxValue);
            int predictor = SelectPredictor(samples, channel, channels, initialDelta);
            short delta = initialDelta;

            block[channel] = (byte)predictor;
            BinaryPrimitives.WriteInt16LittleEndian(block[(channels + (channel * 2))..], initialDelta);
            BinaryPrimitives.WriteInt16LittleEndian(block[((3 * channels) + (channel * 2))..], samples[channels + channel]);
            BinaryPrimitives.WriteInt16LittleEndian(block[((5 * channels) + (channel * 2))..], samples[channel]);

            EncodeChannel(samples, block[(7 * channels)..], channel, channels, predictor, ref delta);
            channelState[channel] = delta;
        }
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
            var nibble = (block[pos] >> 4) & 0x0F;
            DecodeSample(nibble, coeff1, coeff2, ref sample1, ref sample2, ref delta);
            if (written < output.Length)
                output[written++] = sample1;

            if (pos < block.Length && written < output.Length)
            {
                nibble = block[pos] & 0x0F;
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
        var rightPredIdx = Math.Clamp((int)block[1], 0, 6);
        var (rightCoeff1, rightCoeff2) = CoefficientSets[rightPredIdx];
        var leftDelta = (short)(block[2] | (block[3] << 8));
        var rightDelta = (short)(block[4] | (block[5] << 8));
        var leftSample1 = (short)(block[6] | (block[7] << 8));
        var rightSample1 = (short)(block[8] | (block[9] << 8));
        var leftSample2 = (short)(block[10] | (block[11] << 8));
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

            var leftNibble = (byteVal >> 4) & 0x0F;
            DecodeSample(leftNibble, leftCoeff1, leftCoeff2, ref leftSample1, ref leftSample2, ref leftDelta);
            output[written++] = leftSample1;

            var rightNibble = byteVal & 0x0F;
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

    private static int SelectPredictor(ReadOnlySpan<short> samples, int channel, int channels, short initialDelta)
    {
        int best = 0;
        long bestError = long.MaxValue;

        for (int predictor = 0; predictor < CoefficientSets.Length; predictor++)
        {
            short delta = initialDelta;
            long error = EncodeChannel(samples, Span<byte>.Empty, channel, channels, predictor, ref delta);

            if (error < bestError)
            {
                best = predictor;
                bestError = error;
            }
        }

        return best;
    }

    private static long EncodeChannel(ReadOnlySpan<short> samples, Span<byte> data, int channel, int channels, int predictor, ref short delta)
    {
        (short coeff1, short coeff2) = CoefficientSets[predictor];
        short sample2 = samples[channel];
        short sample1 = samples[channels + channel];
        int framesPerBlock = samples.Length / channels;
        long error = 0;

        for (int frame = 2; frame < framesPerBlock; frame++)
        {
            short target = samples[(frame * channels) + channel];
            int predicted = ((coeff1 * sample1) + (coeff2 * sample2)) >> 8;
            int nibble = Math.Clamp((int)Math.Round((double)(target - predicted) / delta), -8, 7) & 0x0F;

            DecodeSample(nibble, coeff1, coeff2, ref sample1, ref sample2, ref delta);
            error += (long)(target - sample1) * (target - sample1);

            if (data.IsEmpty)
                continue;

            int index = ((frame - 2) * channels) + channel;
            data[index / 2] |= (byte)(index % 2 == 0 ? nibble << 4 : nibble);
        }

        return error;
    }
}
