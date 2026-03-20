// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox;

internal readonly struct BytePatternPlan
{
    public byte[] Needle { get; }
    public byte[] Mask { get; }
    public int PatternLength { get; }
    public int FirstKnownByteIndex { get; }
    public bool HasOnlyWildcards { get; }
    public byte FirstKnownByteValue { get; }

    public BytePatternPlan(byte[] needle, byte[] mask)
    {
        Needle = needle;
        Mask = mask;
        PatternLength = needle.Length;
        FirstKnownByteIndex = BytePatternScanner.FindFirstKnownByteIndex(mask);
        HasOnlyWildcards = FirstKnownByteIndex < 0;
        FirstKnownByteValue = HasOnlyWildcards ? (byte)0 : needle[FirstKnownByteIndex];
    }
}
