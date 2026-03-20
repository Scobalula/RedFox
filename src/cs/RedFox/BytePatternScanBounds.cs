// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox;

internal readonly struct BytePatternScanBounds(long start, long end)
{
    public long Start => start;
    public long End => end;
    public bool HasRange => end > start;

    public bool Contains(long offset)
    {
        return offset >= start && offset < end;
    }

    public int GetReadLength(long currentOffset, int bufferSize)
    {
        long remaining = end - currentOffset;

        if (remaining <= 0)
        {
            return 0;
        }

        int maxLength = remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        return Math.Min(bufferSize, maxLength);
    }
}
