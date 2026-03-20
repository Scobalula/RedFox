// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;

namespace RedFox.Tests.Core;

public sealed class PublicUtilitiesTests
{
    [Fact]
    public void ReadNullTerminatedString_UsesChunkReaderAndDecodesUtf8()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("redfox"), 0];
        int offset = 0;
        string value = NullTerminatedStringReader.ReadNullTerminatedString(
            Encoding.UTF8,
            maxBytes: bytes.Length,
            chunkSize: 3,
            readChunk: destination =>
            {
                int toRead = Math.Min(destination.Length, bytes.Length - offset);
                if (toRead <= 0)
                {
                    return 0;
                }

                bytes.AsSpan(offset, toRead).CopyTo(destination);
                offset += toRead;
                return toRead;
            },
            onMissing: () => new InvalidOperationException("No terminator."));

        Assert.Equal("redfox", value);
    }

    [Fact]
    public void ReadNullTerminatedBytes_WithoutTerminator_ThrowsFactoryException()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("abc");
        int offset = 0;
        Assert.Throws<InvalidOperationException>(
            () => NullTerminatedStringReader.ReadNullTerminatedBytes(
                Encoding.ASCII,
                maxBytes: bytes.Length,
                chunkSize: 2,
                readChunk: destination =>
                {
                    int toRead = Math.Min(destination.Length, bytes.Length - offset);
                    if (toRead <= 0)
                    {
                        return 0;
                    }

                    bytes.AsSpan(offset, toRead).CopyTo(destination);
                    offset += toRead;
                    return toRead;
                },
                onMissing: () => new InvalidOperationException("Missing terminator.")));
    }

    [Fact]
    public void ReadNullTerminatedBytes_NegativeChunkCount_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => NullTerminatedStringReader.ReadNullTerminatedBytes(
                Encoding.UTF8,
                maxBytes: 16,
                chunkSize: 8,
                readChunk: _ => -1,
                onMissing: () => new InvalidOperationException("Missing terminator.")));
    }

    [Fact]
    public void BytePatternScanner_NegativeChunkCount_ThrowsInvalidOperationException()
    {
        Pattern<byte> pattern = BytePattern.Parse("AA");
        Assert.Throws<InvalidOperationException>(
            () => BytePatternScanner.Scan(pattern, start: 0, end: 10, bufferSize: 8, firstOnly: false, readChunk: (_, _) => -1));
    }

    [Fact]
    public void BytePatternScanner_FindFirstAndAllMatches()
    {
        byte[] data = [0x10, 0xAB, 0xCD, 0x20, 0xAB, 0xCD, 0x30];
        Pattern<byte> pattern = BytePattern.Parse("AB CD");
        int reads = 0;

        long[] all = BytePatternScanner.Scan(
            pattern,
            start: 0,
            end: data.Length,
            bufferSize: 4,
            firstOnly: false,
            readChunk: (offset, destination) =>
            {
                reads++;
                int offsetInt = (int)offset;
                int toRead = Math.Min(destination.Length, data.Length - offsetInt);
                if (toRead <= 0)
                {
                    return 0;
                }

                data.AsSpan(offsetInt, toRead).CopyTo(destination);
                return toRead;
            });

        long[] first = BytePatternScanner.Scan(
            pattern,
            start: 0,
            end: data.Length,
            bufferSize: 4,
            firstOnly: true,
            readChunk: (offset, destination) =>
            {
                int offsetInt = (int)offset;
                int toRead = Math.Min(destination.Length, data.Length - offsetInt);
                if (toRead <= 0)
                {
                    return 0;
                }

                data.AsSpan(offsetInt, toRead).CopyTo(destination);
                return toRead;
            });

        Assert.Equal([1L, 4L], all);
        Assert.Equal([1L], first);
        Assert.True(reads > 0);
    }
}
