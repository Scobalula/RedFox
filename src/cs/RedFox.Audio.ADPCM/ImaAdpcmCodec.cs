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
/// Audio codec for IMA ADPCM (Interactive Multimedia Association Adaptive Differential Pulse-Code Modulation).
/// Supports mono and stereo encoding and decoding of raw IMA ADPCM block data.
/// </summary>
public sealed class ImaAdpcmCodec : AudioCodec
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

    /// <inheritdoc/>
    public override string Name => "IMA ADPCM";

    /// <inheritdoc/>
    public override AudioCodecFlags Flags => AudioCodecFlags.SupportsEncoding | AudioCodecFlags.SupportsDecoding;

    /// <summary>
    /// Gets the number of decoded PCM samples per block for the given format.
    /// </summary>
    /// <param name="format">The audio format describing the block structure.</param>
    /// <returns>The number of interleaved samples produced per block.</returns>
    public static int GetSamplesPerBlock(AudioFormat format)
    {
        var headerSize = 4 * format.Channels;

        return format.Channels == 1
            ? 1 + (format.BlockAlign - headerSize) * 2
            : (1 + (format.BlockAlign - headerSize)) * format.Channels;
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
        var headerSize = 4 * channels;
        var samplesPerBlock = GetSamplesPerBlock(format);
        var blockCount = source.Length / blockAlign;
        var written = 0;

        for (var b = 0; b < blockCount; b++)
        {
            var blockOffset = b * blockAlign;
            var blockEnd = written + samplesPerBlock;

            if (channels == 1)
            {
                DecodeMonoBlock(
                    source.Slice(blockOffset, blockAlign),
                    destination[written..blockEnd],
                    headerSize);
            }
            else
            {
                DecodeStereoBlock(
                    source.Slice(blockOffset, blockAlign),
                    destination[written..blockEnd],
                    headerSize);
            }

            written = blockEnd;
        }

        return written;
    }

    /// <inheritdoc/>
    public override int Encode(ReadOnlySpan<short> source, Span<byte> destination, AudioFormat format)
    {
        var blockAlign = format.BlockAlign;
        var channels = format.Channels;
        var headerSize = 4 * channels;
        var samplesPerBlock = GetSamplesPerBlock(format);
        var written = 0;
        var samplePos = 0;

        while (samplePos < source.Length)
        {
            var remaining = source.Length - samplePos;
            var blockSamples = Math.Min(remaining, samplesPerBlock);
            var blockOffset = written * blockAlign / blockAlign * blockAlign;

            if (blockOffset + blockAlign > destination.Length)
                break;

            if (channels == 1)
            {
                EncodeMonoBlock(
                    source.Slice(samplePos, blockSamples),
                    destination.Slice(blockOffset, blockAlign),
                    headerSize);
            }
            else
            {
                EncodeStereoBlock(
                    source.Slice(samplePos, blockSamples),
                    destination.Slice(blockOffset, blockAlign),
                    headerSize);
            }

            samplePos += blockSamples;
            written += blockAlign;
        }

        return written;
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
        for (var i = headerSize; i < block.Length && outPos + 1 < output.Length; i++)
        {
            output[outPos++] = DecodeNibble((byte)(block[i] & 0x0F), ref leftPredictor, ref leftStepIndex);
            output[outPos++] = DecodeNibble((byte)((block[i] >> 4) & 0x0F), ref rightPredictor, ref rightStepIndex);
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

    private static void EncodeMonoBlock(ReadOnlySpan<short> samples, Span<byte> block, int headerSize)
    {
        var predictor = samples[0];
        var stepIndex = 0;

        block[0] = (byte)(predictor & 0xFF);
        block[1] = (byte)((predictor >> 8) & 0xFF);
        block[2] = 0;
        block[3] = 0;

        var inPos = 1;
        for (var i = headerSize; i < block.Length && inPos < samples.Length; i++)
        {
            var low = EncodeNibble(samples[inPos++], ref predictor, ref stepIndex);

            var high = (byte)0;
            if (inPos < samples.Length)
                high = EncodeNibble(samples[inPos++], ref predictor, ref stepIndex);

            block[i] = (byte)(low | (high << 4));
        }
    }

    private static void EncodeStereoBlock(ReadOnlySpan<short> samples, Span<byte> block, int headerSize)
    {
        var leftPredictor = samples[0];
        var leftStepIndex = 0;

        var rightPredictor = samples[1];
        var rightStepIndex = 0;

        block[0] = (byte)(leftPredictor & 0xFF);
        block[1] = (byte)((leftPredictor >> 8) & 0xFF);
        block[2] = 0;
        block[3] = 0;

        block[4] = (byte)(rightPredictor & 0xFF);
        block[5] = (byte)((rightPredictor >> 8) & 0xFF);
        block[6] = 0;
        block[7] = 0;

        var inPos = 2;
        for (var i = headerSize; i < block.Length && inPos + 1 < samples.Length; i++)
        {
            var left = EncodeNibble(samples[inPos++], ref leftPredictor, ref leftStepIndex);
            var right = EncodeNibble(samples[inPos++], ref rightPredictor, ref rightStepIndex);

            block[i] = (byte)(left | (right << 4));
        }
    }
}
