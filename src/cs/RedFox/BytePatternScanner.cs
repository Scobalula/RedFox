using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RedFox;

/// <summary>
/// Provides high-performance byte-pattern scanning over chunked data sources.
/// </summary>
public static class BytePatternScanner
{
    /// <summary>
    /// Represents a callback that reads bytes from a source into a destination span.
    /// </summary>
    /// <param name="offset">The source offset for the current read operation.</param>
    /// <param name="destination">The destination span to fill with source bytes.</param>
    /// <returns>The number of bytes written into <paramref name="destination"/>.</returns>
    public delegate int ByteChunkReader(long offset, Span<byte> destination);

    /// <summary>
    /// Scans a source for a byte pattern and returns matching absolute offsets.
    /// </summary>
    /// <param name="pattern">The byte pattern to scan for, including wildcard mask values.</param>
    /// <param name="start">The inclusive start offset to scan from.</param>
    /// <param name="end">The exclusive end offset to scan to.</param>
    /// <param name="bufferSize">The chunk size used for each source read operation.</param>
    /// <param name="firstOnly">When true, scanning stops after the first match.</param>
    /// <param name="readChunk">The callback used to read source bytes.</param>
    /// <returns>An array containing matching offsets.</returns>
    public static long[] Scan(Pattern<byte> pattern, long start, long end, int bufferSize, bool firstOnly, ByteChunkReader readChunk)
    {
        BytePatternPlan plan = ValidateAndCreatePatternPlan(pattern, bufferSize, readChunk);
        BytePatternScanBounds bounds = new(start, end);
        if (!bounds.HasRange)
        {
            return [];
        }

        List<long> matches = [];
        using BytePatternScanBufferSet buffers = new(bufferSize, plan.PatternLength);
        long currentOffset = bounds.Start;
        while (currentOffset < bounds.End)
        {
            int requestedLength = bounds.GetReadLength(currentOffset, bufferSize);
            if (requestedLength <= 0)
            {
                break;
            }

            int bytesRead = readChunk(currentOffset, buffers.GetReadDestination(requestedLength));
            ValidateChunkReadCount(bytesRead, requestedLength);
            if (bytesRead == 0)
            {
                break;
            }

            BytePatternScanWindow window = buffers.BuildWindow(bytesRead, currentOffset);
            if (TryCollectMatches(plan, window, bounds, firstOnly, matches))
            {
                return [.. matches];
            }

            buffers.UpdateOverlap(window.Bytes);
            currentOffset += bytesRead;
        }

        return [.. matches];
    }

    private static bool TryCollectMatches(BytePatternPlan plan, BytePatternScanWindow window, BytePatternScanBounds bounds, bool first, List<long> matches)
    {
        if (!window.CanContain(plan.PatternLength))
        {
            return false;
        }

        if (first)
        {
            return TryCollectFirstMatch(plan, window, bounds, matches);
        }

        CollectAllMatches(plan, window, bounds, matches);
        return false;
    }

    private static bool TryCollectFirstMatch(BytePatternPlan plan, BytePatternScanWindow window, BytePatternScanBounds bounds, List<long> matches)
    {
        if (plan.HasOnlyWildcards)
        {
            if (!TryCollectFirstWildcardMatch(plan.PatternLength, window, bounds, out long matchOffset))
            {
                return false;
            }

            matches.Add(matchOffset);
            return true;
        }

        if (!TryCollectFirstKnownMatch(plan, window, bounds, out long knownMatchOffset))
        {
            return false;
        }

        matches.Add(knownMatchOffset);
        return true;
    }

    private static void CollectAllMatches(BytePatternPlan plan, BytePatternScanWindow window, BytePatternScanBounds bounds, List<long> matches)
    {
        if (plan.HasOnlyWildcards)
        {
            CollectAllWildcardMatches(plan.PatternLength, window, bounds, matches);
            return;
        }

        CollectAllKnownMatches(plan, window, bounds, matches);
    }

    private static bool TryCollectFirstWildcardMatch(int length, BytePatternScanWindow window, BytePatternScanBounds bounds, out long matchOffset)
    {
        int lastCandidateStart = window.GetLastCandidateStart(length);
        for (int candidateStart = 0; candidateStart <= lastCandidateStart; candidateStart++)
        {
            long offset = window.BaseOffset + candidateStart;
            if (!bounds.Contains(offset))
            {
                continue;
            }

            matchOffset = offset;
            return true;
        }

        matchOffset = 0;
        return false;
    }

    private static void CollectAllWildcardMatches(int length, BytePatternScanWindow window, BytePatternScanBounds bounds, List<long> matches)
    {
        int lastCandidateStart = window.GetLastCandidateStart(length);
        for (int candidateStart = 0; candidateStart <= lastCandidateStart; candidateStart++)
        {   
            long offset = window.BaseOffset + candidateStart;
            if (bounds.Contains(offset))
            {
                matches.Add(offset);
            }
        }
    }

    private static bool TryCollectFirstKnownMatch(BytePatternPlan plan, BytePatternScanWindow window, BytePatternScanBounds bounds, out long matchOffset)
    {
        ReadOnlySpan<byte> candidateByteSpan = window.GetCandidateByteSpan(plan.AnchorByteIndex, plan.PatternLength);
        int candidateSearchStart = 0;
        while (candidateSearchStart < candidateByteSpan.Length)
        {
            int candidateIndex = IndexOfByte(candidateByteSpan[candidateSearchStart..], plan.AnchorByteValue);
            if (candidateIndex < 0)
            {
                break;
            }

            int anchorCandidateIndex = candidateSearchStart + candidateIndex + plan.AnchorByteIndex;
            if (!window.TryTranslateAnchorCandidateToMatchStart(plan.AnchorByteIndex, anchorCandidateIndex, out int matchStart))
            {
                candidateSearchStart = anchorCandidateIndex + 1 - plan.AnchorByteIndex;
                continue;
            }

            if (IsKnownMatchAt(window.Bytes, plan, matchStart))
            {
                long offset = window.BaseOffset + matchStart;
                if (bounds.Contains(offset))
                {
                    matchOffset = offset;
                    return true;
                }
            }

            candidateSearchStart = matchStart + 1;
        }

        matchOffset = 0;
        return false;
    }

    private static void CollectAllKnownMatches(BytePatternPlan plan, BytePatternScanWindow window, BytePatternScanBounds bounds, List<long> matches)
    {
        ReadOnlySpan<byte> candidateByteSpan = window.GetCandidateByteSpan(plan.AnchorByteIndex, plan.PatternLength);
        int candidateSearchStart = 0;
        while (candidateSearchStart < candidateByteSpan.Length)
        {
            int candidateIndex = IndexOfByte(candidateByteSpan[candidateSearchStart..], plan.AnchorByteValue);
            if (candidateIndex < 0)
            {
                return;
            }

            int anchorCandidateIndex = candidateSearchStart + candidateIndex + plan.AnchorByteIndex;
            if (!window.TryTranslateAnchorCandidateToMatchStart(plan.AnchorByteIndex, anchorCandidateIndex, out int matchStart))
            {
                candidateSearchStart = anchorCandidateIndex + 1 - plan.AnchorByteIndex;
                continue;
            }

            if (IsKnownMatchAt(window.Bytes, plan, matchStart))
            {
                long offset = window.BaseOffset + matchStart;
                if (bounds.Contains(offset))
                {
                    matches.Add(offset);
                }
            }

            candidateSearchStart = matchStart + 1;
        }
    }

    private static BytePatternPlan ValidateAndCreatePatternPlan(Pattern<byte> pattern, int bufferSize, ByteChunkReader readChunk)
    {
        ArgumentNullException.ThrowIfNull(readChunk);
        ArgumentNullException.ThrowIfNull(pattern.Needle);
        ArgumentNullException.ThrowIfNull(pattern.Mask);
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Chunk size must be greater than zero.");
        }

        if (pattern.Needle.Length == 0)
        {
            throw new ArgumentException("Pattern needle cannot be empty.", nameof(pattern));
        }

        if (pattern.Needle.Length != pattern.Mask.Length)
        {
            throw new ArgumentException("Pattern needle and mask lengths must match.", nameof(pattern));
        }

        return new BytePatternPlan(pattern.Needle, pattern.Mask);
    }

    private static void ValidateChunkReadCount(int bytesRead, int requestedBytes)
    {
        if (bytesRead < 0)
        {
            throw new InvalidOperationException("Chunk reader returned a negative byte count.");
        }

        if (bytesRead > requestedBytes)
        {
            throw new InvalidOperationException("Chunk reader returned more bytes than requested.");
        }
    }

    private static int IndexOfByte(ReadOnlySpan<byte> source, byte value)
    {
        if (source.IsEmpty)
        {
            return -1;
        }

        if (Avx2.IsSupported && source.Length >= Vector256<byte>.Count)
        {
            return IndexOfByteAvx2(source, value);
        }

        if (Sse2.IsSupported && source.Length >= Vector128<byte>.Count)
        {
            return IndexOfByteSse2(source, value);
        }

        return source.IndexOf(value);
    }

    private static int IndexOfByteAvx2(ReadOnlySpan<byte> source, byte value)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(source);
        int sourceLength = source.Length;
        int vectorSize = Vector256<byte>.Count;
        int currentIndex = 0;
        int lastVectorStart = sourceLength - vectorSize;
        Vector256<byte> valueVector = Vector256.Create(value);
        while (currentIndex <= lastVectorStart)
        {
            Vector256<byte> sourceVector = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref sourceReference, currentIndex));
            Vector256<byte> compareVector = Avx2.CompareEqual(sourceVector, valueVector);
            uint matchMask = (uint)Avx2.MoveMask(compareVector);
            if (matchMask != 0)
            {
                return currentIndex + BitOperations.TrailingZeroCount(matchMask);
            }

            currentIndex += vectorSize;
        }

        for (; currentIndex < sourceLength; currentIndex++)
        {
            if (Unsafe.Add(ref sourceReference, currentIndex) == value)
            {
                return currentIndex;
            }
        }

        return -1;
    }

    private static int IndexOfByteSse2(ReadOnlySpan<byte> source, byte value)
    {
        ref byte sourceReference = ref MemoryMarshal.GetReference(source);
        int sourceLength = source.Length;
        int vectorSize = Vector128<byte>.Count;
        int currentIndex = 0;
        int lastVectorStart = sourceLength - vectorSize;
        Vector128<byte> valueVector = Vector128.Create(value);
        while (currentIndex <= lastVectorStart)
        {
            Vector128<byte> sourceVector = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sourceReference, currentIndex));
            Vector128<byte> compareVector = Sse2.CompareEqual(sourceVector, valueVector);
            uint matchMask = (uint)Sse2.MoveMask(compareVector);
            if (matchMask != 0)
            {
                return currentIndex + BitOperations.TrailingZeroCount(matchMask);
            }

            currentIndex += vectorSize;
        }

        for (; currentIndex < sourceLength; currentIndex++)
        {
            if (Unsafe.Add(ref sourceReference, currentIndex) == value)
            {
                return currentIndex;
            }
        }

        return -1;
    }

    private static bool IsKnownMatchAt(ReadOnlySpan<byte> bytes, BytePatternPlan plan, int startIndex)
    {
        if (startIndex < 0)
        {
            return false;
        }

        if (startIndex > bytes.Length - plan.PatternLength)
        {
            return false;
        }

        for (int index = 0; index < plan.KnownByteCount; index++)
        {
            int checkOffset = startIndex + plan.KnownByteOffsets[index];
            if (bytes[checkOffset] != plan.KnownByteValues[index])
            {
                return false;
            }
        }

        return true;
    }

}
