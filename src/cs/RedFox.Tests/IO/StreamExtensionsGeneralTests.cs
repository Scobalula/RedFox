// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox;
using RedFox.IO;

namespace RedFox.Tests.IO;

public sealed class StreamExtensionsGeneralTests
{
    [Fact]
    public void Scan_PreservesOriginalStreamPosition()
    {
        byte[] data = [0xAA, 0xBB, 0xAA, 0xBB];
        using MemoryStream stream = new(data, writable: false);
        stream.Position = 2;

        long[] matches = stream.Scan("AA BB", 0, data.Length, firstOccurence: false);
        Assert.Equal(2L, stream.Position);
        Assert.Equal([0L, 2L], matches);
    }

    [Fact]
    public void Scan_WithPatternFirstOnly_ReturnsSingleMatch()
    {
        byte[] data = [0x01, 0xFF, 0x10, 0x01, 0x20, 0x10];
        using MemoryStream stream = new(data, writable: false);
        Pattern<byte> pattern = BytePattern.Parse("01 ?? 10");

        long[] matches = stream.Scan(pattern, firstOccurence: true);
        Assert.Single(matches);
        Assert.Equal(0L, matches[0]);
    }

    [Fact]
    public void Scan_WithNeedleMaskOverload_ReturnsExpectedMatches()
    {
        byte[] data = [0x10, 0x20, 0x10, 0x99, 0x10, 0x20];
        using MemoryStream stream = new(data, writable: false);

        byte[] needle = [0x10, 0x00];
        byte[] mask = [0x00, 0xFF];
        long[] matches = stream.Scan(needle, mask, 0, data.Length, firstOnly: false, bufferSize: 4);

        Assert.Equal([0L, 2L, 4L], matches);
    }

    [Fact]
    public void Scan_WithInvalidRange_Throws()
    {
        using MemoryStream stream = new([1, 2, 3], writable: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Scan("01", 3, 2, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Scan("01", 99, 100, false));
    }

    [Fact]
    public void Scan_WithNonSeekableStream_Throws()
    {
        using NonSeekableStream stream = new([0xAA, 0xBB]);
        Assert.Throws<NotSupportedException>(() => stream.Scan("AA BB"));
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data, writable: false)
    {
        public override bool CanSeek => false;
    }
}
