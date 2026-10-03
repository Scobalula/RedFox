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
/// Codec implementation for IMA ADPCM (Interactive Multimedia Association Adaptive Differential Pulse Code Modulation) audio format.
/// Supports both encoding and decoding of IMA ADPCM audio with mono and stereo variants.
/// </summary>
public sealed class ImaAdpcmCodec : AdpcmCodec
{
    private static readonly int[] StepTable =
    [
            7,     8,     9,    10,    11,    12,    13,    14,
           16,    17,    19,    21,    23,    25,    28,    31,
           34,    37,    41,    45,    50,    55,    60,    66,
           73,    80,    88,    97,   107,   118,   130,   143,
          157,   173,   190,   209,   230,   253,   279,   307,
          337,   371,   408,   449,   494,   544,   598,   658,
          724,   796,   876,   963,  1060,  1166,  1282,  1411,
         1552,  1707,  1878,  2066,  2272,  2499,  2749,  3024,
         3327,  3660,  4026,  4428,  4871,  5358,  5894,  6484,
         7132,  7845,  8630,  9493, 10442, 11487, 12635, 13899,
        15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    ];

    private static readonly int[] IndexTable = [-1, -1, -1, -1, 2, 4, 6, 8];

    /// <summary>
    /// The RIFF WAVE format tag for IMA ADPCM (0x11).
    /// </summary>
    public const ushort WaveFormatTag = 0x11;

    /// <inheritdoc/>
    public override string Id => "ima-adpcm";

    /// <inheritdoc/>
    public override string Name => "IMA ADPCM";

    /// <inheritdoc/>
    public override int GetFramesPerBlock(int channels, int blockAlign) => channels == 1 ? 1 + ((blockAlign - 4) * 2) : 1 + ((blockAlign - 8) / 8 * 8);

    /// <inheritdoc/>
    public override byte[] CreateSetup(int framesPerBlock) => [(byte)framesPerBlock, (byte)(framesPerBlock >> 8)];

    /// <inheritdoc/>
    public override void DecodeBlock(ReadOnlySpan<byte> block, Span<short> output, int channels)
    {
        if (channels == 1)
            DecodeMonoBlock(block, output, 4);
        else
            DecodeStereoBlock(block, output, 8);
    }

    /// <inheritdoc/>
    public override void EncodeBlock(ReadOnlySpan<short> samples, Span<byte> block, int channels, Span<int> channelState)
    {
        block.Clear();

        int framesPerBlock = samples.Length / channels;
        int headerSize = 4 * channels;

        for (int channel = 0; channel < channels; channel++)
        {
            short predictor = samples[channel];
            int stepIndex = Math.Clamp(channelState[channel], 0, 88);

            BinaryPrimitives.WriteInt16LittleEndian(block[(channel * 4)..], predictor);
            block[(channel * 4) + 2] = (byte)stepIndex;

            for (int frame = 1; frame < framesPerBlock; frame++)
            {
                int index = frame - 1;
                int offset = headerSize + (index / 8 * 4 * channels) + (channel * 4) + (index % 8 / 2);
                byte nibble = EncodeNibble(samples[(frame * channels) + channel], ref predictor, ref stepIndex);

                block[offset] |= (byte)(index % 2 == 0 ? nibble : nibble << 4);
            }

            channelState[channel] = stepIndex;
        }
    }

    private static void DecodeMonoBlock(ReadOnlySpan<byte> block, Span<short> output, int headerSize)
    {
        var predictor = (short)(block[0] | (block[1] << 8));
        var stepIndex = Math.Clamp((int)block[2], 0, 88);

        output[0] = predictor;

        var outPos = 1;
        for (var i = headerSize; i < block.Length && outPos < output.Length; i++)
        {
            output[outPos++] = DecodeNibble((byte)(block[i] & 0x0F), ref predictor, ref stepIndex);

            if (outPos < output.Length)
                output[outPos++] = DecodeNibble((byte)((block[i] >> 4) & 0x0F), ref predictor, ref stepIndex);
        }
    }

    private static void DecodeStereoBlock(ReadOnlySpan<byte> block, Span<short> output, int headerSize)
    {
        var leftPredictor = (short)(block[0] | (block[1] << 8));
        var leftStepIndex = Math.Clamp((int)block[2], 0, 88);

        var rightPredictor = (short)(block[4] | (block[5] << 8));
        var rightStepIndex = Math.Clamp((int)block[6], 0, 88);

        output[0] = leftPredictor;
        output[1] = rightPredictor;

        var outPos = 2;
        Span<short> leftSamples = stackalloc short[8];
        Span<short> rightSamples = stackalloc short[8];
        for (var chunk = headerSize; chunk + 7 < block.Length; chunk += 8)
        {
            for (var i = 0; i < 4; i++)
            {
                leftSamples[i * 2] = DecodeNibble((byte)(block[chunk + i] & 0x0F), ref leftPredictor, ref leftStepIndex);
                leftSamples[i * 2 + 1] = DecodeNibble((byte)(block[chunk + i] >> 4), ref leftPredictor, ref leftStepIndex);
                rightSamples[i * 2] = DecodeNibble((byte)(block[chunk + 4 + i] & 0x0F), ref rightPredictor, ref rightStepIndex);
                rightSamples[i * 2 + 1] = DecodeNibble((byte)(block[chunk + 4 + i] >> 4), ref rightPredictor, ref rightStepIndex);
            }

            for (var i = 0; i < 8 && outPos + 1 < output.Length; i++)
            {
                output[outPos++] = leftSamples[i];
                output[outPos++] = rightSamples[i];
            }
        }
    }

    private static short DecodeNibble(byte nibble, ref short predictor, ref int stepIndex)
    {
        var step = StepTable[stepIndex];
        var diff = step >> 3;

        if ((nibble & 4) != 0) diff += step;
        if ((nibble & 2) != 0) diff += step >> 1;
        if ((nibble & 1) != 0) diff += step >> 2;

        if ((nibble & 8) != 0)
            diff = -diff;

        predictor = (short)Math.Clamp(predictor + diff, -32768, 32767);
        stepIndex = Math.Clamp(stepIndex + IndexTable[nibble & 7], 0, 88);

        return predictor;
    }

    private static byte EncodeNibble(short target, ref short predictor, ref int stepIndex)
    {
        var step = StepTable[stepIndex];
        var diff = target - predictor;
        var sign = (byte)(diff < 0 ? 8 : 0);

        if (diff < 0)
            diff = -diff;

        byte encoded = 0;

        if (diff >= step)
        {
            encoded |= 4;
            diff -= step;
        }

        if (diff >= step >> 1)
        {
            encoded |= 2;
            diff -= step >> 1;
        }

        if (diff >= step >> 2)
            encoded |= 1;

        encoded |= sign;

        var delta = step >> 3;
        if ((encoded & 4) != 0) delta += step;
        if ((encoded & 2) != 0) delta += step >> 1;
        if ((encoded & 1) != 0) delta += step >> 2;
        if ((encoded & 8) != 0) delta = -delta;

        predictor = (short)Math.Clamp(predictor + delta, -32768, 32767);
        stepIndex = Math.Clamp(stepIndex + IndexTable[encoded & 7], 0, 88);

        return encoded;
    }
}
