// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox;

internal readonly ref struct BytePatternScanWindow(ReadOnlySpan<byte> bytes, long baseOffset)
{
    public readonly ReadOnlySpan<byte> Bytes = bytes;
    public readonly long BaseOffset = baseOffset;

    public bool CanContain(int patternLength)
    {
        return Bytes.Length >= patternLength;
    }

    public int GetLastCandidateStart(int patternLength)
    {
        return Bytes.Length - patternLength;
    }

    public ReadOnlySpan<byte> GetCandidateByteSpan(int firstKnownByteIndex, int patternLength)
    {
        int lastCandidateStart = GetLastCandidateStart(patternLength);
        return Bytes.Slice(firstKnownByteIndex, lastCandidateStart + 1);
    }
}
