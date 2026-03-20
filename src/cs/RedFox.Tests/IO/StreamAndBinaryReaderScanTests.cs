// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.IO;

namespace RedFox.Tests.IO;

public sealed class StreamAndBinaryReaderScanTests
{
    [Fact]
    public void StreamScan_PatternAcrossBufferBoundary_ReturnsExpectedMatch()
    {
        byte[] data = [0x10, 0x20, 0xA1, 0xB2, 0xC3, 0x30, 0x40];
        using MemoryStream stream = new(data, writable: false);

        long[] matches = stream.Scan("A1 B2 C3", 0, data.Length, false);
        Assert.Single(matches);
        Assert.Equal(2L, matches[0]);
    }

    [Fact]
    public void StreamScan_OverlapFallbackCase_ReturnsExpectedMatch()
    {
        byte[] data = [0xAB, 0xAB, 0xAC];
        using MemoryStream stream = new(data, writable: false);

        long[] matches = stream.Scan("AB AC", 0, data.Length, false);
        Assert.Single(matches);
        Assert.Equal(1L, matches[0]);
    }

    [Fact]
    public void BinaryReaderScan_PatternAcrossBufferBoundary_ReturnsExpectedMatch()
    {
        byte[] data = [0x00, 0xAA, 0xBB, 0xCC, 0xDD];
        using MemoryStream stream = new(data, writable: false);
        using BinaryReader reader = new(stream);

        long[] matches = reader.Scan("AA BB CC", 0, data.Length, false);
        Assert.Single(matches);
        Assert.Equal(1L, matches[0]);
    }
}
