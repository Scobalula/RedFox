// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides methods for compressing and decompressing name files using a simple LZ-based algorithm.
/// </summary>
public static class NameFileCompressor
{
    private const int WindowSize = 4096;
    private const int WindowMask = WindowSize - 1;
    private const int MinMatch = 3;
    private const int MaxMatch = MinMatch + 15;
    private const int HashSize = 1 << 12;
    private const int MaxChain = 32;

    /// <summary>
    /// Calculates the maximum compressed size for the provided input size.
    /// </summary>
    /// <param name="inputSize">The size of the input data.</param>
    /// <returns>The maximum compressed size.</returns>
    public static int GetMaxCompressedSize(int inputSize) => inputSize + (inputSize >> 3) + 1;

    /// <summary>
    /// Compresses <paramref name="source"/> into <paramref name="destination"/>, returning the byte count written.
    /// </summary>
    /// <param name="source">The source data to compress.</param>
    /// <param name="destination">The destination span to write the compressed data to.</param>
    /// <returns>The number of bytes written to the destination span.</returns>
    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        // NOTE: This is separate to any compression implementation in main RedFox.Compression 
        // as may spin this GameExtraction stuff out into a standalone library later.
        int length = source.Length;
        int input = 0, output = 0;
        int flagAt = 0, flagBit = 8;

        Span<int> head = stackalloc int[HashSize];
        Span<int> prev = stackalloc int[WindowSize];
        head.Fill(-1);

        while (input < length)
        {
            if (flagBit == 8)
            {
                flagAt = output;
                destination[output++] = 0;
                flagBit = 0;
            }

            int remaining = length - input;
            int bestLength = MinMatch - 1;
            int bestDistance = 0;

            if (remaining >= MinMatch)
            {
                int hash = (int)(((uint)(source[input] | (source[input + 1] << 8) | (source[input + 2] << 16)) * 2654435761u) >> (32 - 12));
                int candidate = head[hash];

                prev[input & WindowMask] = candidate;
                head[hash] = input;

                int floor = input - WindowSize;
                int maxLength = Math.Min(MaxMatch, remaining);

                for (int chain = MaxChain; candidate >= 0 && candidate > floor && chain > 0; chain--)
                {
                    if (source[candidate + bestLength] == source[input + bestLength])
                    {
                        int len = 0;
                        while (len < maxLength && source[candidate + len] == source[input + len])
                            len++;

                        if (len > bestLength)
                        {
                            bestLength = len;
                            bestDistance = input - candidate;
                            if (len == maxLength)
                                break;
                        }
                    }

                    candidate = prev[candidate & WindowMask];
                }
            }

            if (bestLength >= MinMatch)
            {
                destination[flagAt] |= (byte)(1 << flagBit);
                int token = ((bestDistance - 1) << 4) | (bestLength - MinMatch);
                destination[output++] = (byte)token;
                destination[output++] = (byte)(token >> 8);

                int end = input + bestLength;
                while (++input < end)
                {
                    if (length - input >= MinMatch)
                    {
                        int hash = (int)(((uint)(source[input] | (source[input + 1] << 8) | (source[input + 2] << 16)) * 2654435761u) >> (32 - 12));
                        prev[input & WindowMask] = head[hash];
                        head[hash] = input;
                    }
                }
            }
            else
            {
                destination[output++] = source[input++];
            }

            flagBit++;
        }

        return output;
    }

    /// <summary>
    /// Decompresses data from <paramref name="source"/> into <paramref name="destination"/>, returning the byte count written.
    /// </summary>
    /// <param name="source">The source data to decompress.</param>
    /// <param name="destination">The destination span to write the decompressed data to.</param>
    /// <returns>The number of bytes written to the destination span.</returns>
    public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int length = source.Length;
        int input = 0, output = 0;
        int flagBit = 8;
        byte flags = 0;

        while (input < length)
        {
            if (flagBit == 8)
            {
                flags = source[input++];
                flagBit = 0;
            }

            if ((flags & (1 << flagBit++)) == 0)
            {
                destination[output++] = source[input++];
            }
            else
            {
                int token = source[input] | (source[input + 1] << 8);
                input += 2;

                int from = output - ((token >> 4) + 1);
                int count = (token & 0xF) + MinMatch;

                while (count-- > 0)
                    destination[output++] = destination[from++];
            }
        }

        return output;
    }
}
