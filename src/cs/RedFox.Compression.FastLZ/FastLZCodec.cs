// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
// Managed port of the FastLZ Level 1 algorithm by Ariya Hidayat (BSD/MIT licensed).
// Reference: https://github.com/ariya/FastLZ
// --------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace RedFox.Compression.FastLZ;

/// <summary>
/// Pure-managed implementation of the FastLZ Level 1 compression algorithm. The bit-stream produced is
/// compatible with reference FastLZ Level 1 (no header byte, no stored uncompressed length).
/// </summary>
/// <remarks>
/// <para>FastLZ is a small and very fast LZ77-class codec aimed at real-time compression of binary data such as
/// game assets. Level 1 is the simpler of FastLZ's two compression levels and is the most widely used in the
/// wild.</para>
/// <para>The bit-stream does not store the decompressed length; callers must know (or sideband) the
/// uncompressed size before calling <see cref="Decompress(ReadOnlySpan{byte}, Span{byte})"/>.</para>
/// </remarks>
public sealed class FastLZCodec : CompressionCodec
{
    private const int HashLog = 13;
    private const int HashSize = 1 << HashLog;
    private const int HashMask = HashSize - 1;
    private const int MaxDistance = 8192;
    private const int MaxCopy = 32;
    private const int MaxLen = 264;
    // Maximum value the match-length parameter can take in a single emitted opcode
    // (matches the reference's MAX_LEN - 2 cap). Represents (real_match_length - 2).
    private const int MaxLenParam = MaxLen - 2; // 262

    /// <inheritdoc />
    public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

    /// <inheritdoc />
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        // FastLZ guarantees an upper bound of input + 5% + 66 for the worst-case incompressible stream.
        return inputSize + (inputSize / 20) + 66;
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">FastLZ does not encode the decompressed size into its bit-stream.</exception>
    public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer)
        => throw new NotSupportedException("FastLZ does not store the decompressed size in the stream.");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">FastLZ Level 1 does not support external dictionaries.</exception>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        => throw new NotSupportedException("FastLZCodec does not support dictionaries.");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">FastLZ Level 1 does not support external dictionaries.</exception>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        => throw new NotSupportedException("FastLZCodec does not support dictionaries.");

    /// <inheritdoc />
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int length = source.Length;

        // Trivial short-input case: emit a single literal opcode followed by the raw bytes.
        if (length < 4)
        {
            if (length == 0)
                return 0;

            int needed = length + 1;
            if (destination.Length < needed)
                throw new ArgumentException("Destination buffer is too small.", nameof(destination));

            destination[0] = (byte)(length - 1);
            source.CopyTo(destination[1..]);
            return needed;
        }

        if (destination.Length < GetMaxCompressedSize(length))
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));

        Span<int> hashTable = stackalloc int[HashSize];
        // Slots default to 0; for the first MaxDistance bytes this means a candidate ref of 0, which is a
        // legitimate (if often mismatching) back-reference. Beyond ip == MaxDistance any stale 0 slot will
        // naturally fail the distance check.

        int ipBound = length - 4;
        int ipLimit = length - 12 - 1;
        int anchor = 0;
        int ip = 2;
        int op = 0;
        int @ref = 0;
        int distance = 0;

        while (ip < ipLimit)
        {
            bool matched;

            // Search for a 3-byte match using the rolling hash.
            do
            {
                int hash = Hash(source, ip);
                @ref = hashTable[hash];
                hashTable[hash] = ip;
                distance = ip - @ref;

                matched = (uint)distance < (uint)MaxDistance
                          && source[@ref] == source[ip]
                          && source[@ref + 1] == source[ip + 1]
                          && source[@ref + 2] == source[ip + 2];

                if (ip >= ipLimit)
                    break;
                ip++;
            }
            while (!matched);

            if (ip >= ipLimit)
                break;

            // We advanced past the matched position inside the do/while; step back to where the match starts.
            ip--;

            if (ip > anchor)
                op = EmitLiterals(source, anchor, ip - anchor, destination, op);

            // Encode the match. 'len' carries the reference FastLZ semantics: it is the
            // FastLZ "length parameter" (real_match_length - 2), i.e. the number of bytes the
            // comparator scanned past the initial 3-byte head before a mismatch (or the bound).
            int len = Compare(source, @ref + 3, ip + 3, ipBound);
            op = EmitMatch(len, distance, destination, op);

            ip += len;

            // Re-seed the hash table at the two bytes preceding the match end so subsequent
            // matches can find these bytes. After these two ip++ advances, ip lands one past the
            // last matched byte.
            hashTable[Hash(source, ip)] = ip;
            ip++;
            hashTable[Hash(source, ip)] = ip;
            ip++;

            anchor = ip;
        }

        // Tail literal run.
        int tail = length - anchor;
        if (tail > 0)
            op = EmitLiterals(source, anchor, tail, destination, op);

        return op;
    }

    /// <inheritdoc />
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.IsEmpty)
            return 0;

        int ip = 0;
        int op = 0;
        int ipLimit = source.Length;
        int opLimit = destination.Length;

        uint ctrl = source[ip++];

        while (true)
        {
            if (ctrl >= 32)
            {
                // Back-reference (match copy).
                int len = (int)((ctrl >> 5) - 1);
                int encodedDistance = (int)((ctrl & 31) << 8);

                if (len == 6)
                {
                    if (ip >= ipLimit)
                        throw new InvalidDataException("Truncated FastLZ match length byte.");
                    len += source[ip++];
                }

                if (ip >= ipLimit)
                    throw new InvalidDataException("Truncated FastLZ match distance byte.");
                encodedDistance |= source[ip++];

                int matchLen = len + 3;
                int refIdx = op - encodedDistance - 1;

                if (refIdx < 0)
                    throw new InvalidDataException("FastLZ back-reference precedes the output buffer.");
                if (op + matchLen > opLimit)
                    throw new InvalidDataException("FastLZ destination buffer is too small.");

                // Byte-by-byte copy to correctly handle overlapping ranges (RLE-style matches).
                for (int i = 0; i < matchLen; i++)
                    destination[op + i] = destination[refIdx + i];
                op += matchLen;
            }
            else
            {
                // Literal run.
                int count = (int)(ctrl + 1);
                if (ip + count > ipLimit)
                    throw new InvalidDataException("Truncated FastLZ literal run.");
                if (op + count > opLimit)
                    throw new InvalidDataException("FastLZ destination buffer is too small.");

                source.Slice(ip, count).CopyTo(destination.Slice(op, count));
                ip += count;
                op += count;
            }

            if (ip >= ipLimit)
                break;
            ctrl = source[ip++];
        }

        return op;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(ReadOnlySpan<byte> data, int pos)
    {
        // FastLZ hash: combine two overlapping 16-bit reads with a feedback shift to produce a 13-bit slot.
        int v = data[pos] | (data[pos + 1] << 8);
        int w = data[pos + 1] | (data[pos + 2] << 8);
        return (v ^ w ^ (v >> (16 - HashLog))) & HashMask;
    }

    private static int Compare(ReadOnlySpan<byte> data, int a, int b, int bound)
    {
        int start = b;
        // Mirror reference flz_cmp semantics: increment-then-check so the returned count is
        // (matched_bytes + 1) when the loop terminates on a mismatch, or matched_bytes when the
        // bound is reached. EmitMatch / the decoder both account for this convention.
        while (b < bound)
        {
            bool eq = data[a] == data[b];
            a++;
            b++;
            if (!eq)
                break;
        }
        int matched = b - start;
        return matched > MaxLenParam ? MaxLenParam : matched;
    }

    private static int EmitLiterals(ReadOnlySpan<byte> source, int srcPos, int runs, Span<byte> dest, int op)
    {
        while (runs >= MaxCopy)
        {
            dest[op++] = MaxCopy - 1;
            source.Slice(srcPos, MaxCopy).CopyTo(dest.Slice(op, MaxCopy));
            op += MaxCopy;
            srcPos += MaxCopy;
            runs -= MaxCopy;
        }
        if (runs > 0)
        {
            dest[op++] = (byte)(runs - 1);
            source.Slice(srcPos, runs).CopyTo(dest.Slice(op, runs));
            op += runs;
        }
        return op;
    }

    private static int EmitMatch(int len, int distance, Span<byte> dest, int op)
    {
        // 'len' here is the FastLZ length parameter (real_match_length - 2), in [1, MaxLenParam].
        // 'distance' is the real distance in [1, MaxDistance]; it is decremented to fit in 13 bits.
        distance--;

        // Defensive split for over-long matches. Compare caps at MaxLenParam, so in practice
        // this loop is never entered, but it keeps the encoder safe should the cap be relaxed.
        while (len > MaxLenParam)
        {
            dest[op++] = (byte)((7 << 5) | (distance >> 8));
            dest[op++] = (byte)(MaxLenParam - 7);
            dest[op++] = (byte)(distance & 0xFF);
            len -= MaxLenParam;
        }

        if (len < 7)
        {
            dest[op++] = (byte)((len << 5) | (distance >> 8));
            dest[op++] = (byte)(distance & 0xFF);
        }
        else
        {
            dest[op++] = (byte)((7 << 5) | (distance >> 8));
            dest[op++] = (byte)(len - 7);
            dest[op++] = (byte)(distance & 0xFF);
        }

        return op;
    }
}
