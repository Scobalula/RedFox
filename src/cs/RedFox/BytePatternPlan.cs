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
    public int KnownByteCount { get; }
    public int[] KnownByteOffsets { get; }
    public byte[] KnownByteValues { get; }
    public bool HasOnlyWildcards { get; }
    public int AnchorByteIndex { get; }
    public byte AnchorByteValue { get; }

    public BytePatternPlan(byte[] needle, byte[] mask)
    {
        Needle = needle;
        Mask = mask;
        PatternLength = needle.Length;
        KnownByteCount = CountKnownBytes(mask);
        HasOnlyWildcards = KnownByteCount == 0;
        if (HasOnlyWildcards)
        {
            KnownByteOffsets = [];
            KnownByteValues = [];
            AnchorByteIndex = -1;
            AnchorByteValue = 0;
            return;
        }

        KnownByteOffsets = new int[KnownByteCount];
        KnownByteValues = new byte[KnownByteCount];
        int[] valueFrequency = new int[256];
        int writeIndex = 0;
        for (int index = 0; index < mask.Length; index++)
        {
            if (mask[index] == 0xFF)
            {
                continue;
            }

            byte value = needle[index];
            KnownByteOffsets[writeIndex] = index;
            KnownByteValues[writeIndex] = value;
            writeIndex++;
            valueFrequency[value]++;
        }

        int anchorIndex = KnownByteOffsets[0];
        byte anchorValue = KnownByteValues[0];
        int anchorFrequency = valueFrequency[anchorValue];
        for (int index = 1; index < KnownByteCount; index++)
        {
            byte candidateValue = KnownByteValues[index];
            int candidateFrequency = valueFrequency[candidateValue];
            if (candidateFrequency < anchorFrequency)
            {
                anchorFrequency = candidateFrequency;
                anchorIndex = KnownByteOffsets[index];
                anchorValue = candidateValue;
            }
        }

        AnchorByteIndex = anchorIndex;
        AnchorByteValue = anchorValue;
    }


    private static int CountKnownBytes(byte[] mask)
    {
        int count = 0;
        for (int index = 0; index < mask.Length; index++)
        {
            if (mask[index] != 0xFF)
            {
                count++;
            }
        }

        return count;
    }
}
