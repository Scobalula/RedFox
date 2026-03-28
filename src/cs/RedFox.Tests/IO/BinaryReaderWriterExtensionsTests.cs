// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;
using System.Text;
using RedFox.IO;

namespace RedFox.Tests.IO;

public sealed class BinaryReaderWriterExtensionsTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct TestPair
    {
        public int ValueA;
        public short ValueB;
    }

    [Fact]
    public void WriteStructAndReadStruct_RoundTrip()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        TestPair expected = new() { ValueA = 0x11223344, ValueB = 0x5566 };
        writer.WriteStruct(expected);
        stream.Position = 0;

        TestPair actual = reader.ReadStruct<TestPair>();
        Assert.Equal(expected.ValueA, actual.ValueA);
        Assert.Equal(expected.ValueB, actual.ValueB);
    }

    [Fact]
    public void WriteStructArrayAndReadStructArray_RoundTrip()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        TestPair[] expected =
        [
            new TestPair { ValueA = 1, ValueB = 2 },
            new TestPair { ValueA = 3, ValueB = 4 }
        ];

        writer.WriteStructArray(expected);
        stream.Position = 0;

        Span<TestPair> actual = reader.ReadStructArray<TestPair>(2);
        Assert.Equal(expected[0].ValueA, actual[0].ValueA);
        Assert.Equal(expected[0].ValueB, actual[0].ValueB);
        Assert.Equal(expected[1].ValueA, actual[1].ValueA);
        Assert.Equal(expected[1].ValueB, actual[1].ValueB);
    }

    [Fact]
    public void ReadStructArrayByPosition_WithReturnBack_KeepsPosition()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0xCAFEBABE);
        writer.Write(0x01020304);
        writer.Write(0x05060708);
        stream.Position = 4;

        Span<int> values = reader.ReadStructArray<int>(2, 4, returnBack: true);
        Assert.Equal(4L, stream.Position);
        Assert.Equal(0x01020304, values[0]);
        Assert.Equal(0x05060708, values[1]);
    }

    [Fact]
    public void ReadStructArrayIntoExistingSpan_PopulatesValues()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((short)10);
        writer.Write((short)20);
        stream.Position = 0;

        Span<short> destination = stackalloc short[2];
        reader.ReadStructArray(ref destination);

        Assert.Equal((short)10, destination[0]);
        Assert.Equal((short)20, destination[1]);
    }

    [Fact]
    public void EnumerateStructArray_ReturnsExpectedValues()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((short)3);
        writer.Write((short)4);
        writer.Write((short)5);
        stream.Position = 0;

        short[] values = [.. reader.EnumerateStructArray<short>(3)];
        Assert.Equal([(short)3, (short)4, (short)5], values);
    }

    [Fact]
    public void ReadNumericAndByteHelpers_UseProvidedPosition()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((byte)0xAA);
        writer.Write((short)0x1122);
        writer.Write((ushort)0x3344);
        writer.Write(0x55667788);
        writer.Write(0x99AABBCCu);
        writer.Write(0x0102030405060708L);
        writer.Write(0x1112131415161718UL);
        stream.Position = 0;

        Assert.Equal((byte)0xAA, reader.PeekByte());
        Assert.Equal((byte)0xAA, reader.ReadByte(0));
        Assert.Equal((short)0x1122, reader.ReadInt16(1));
        Assert.Equal((ushort)0x3344, reader.ReadUInt16(3));
        Assert.Equal(0x55667788, reader.ReadInt32(5));
        Assert.Equal(0x99AABBCCu, reader.ReadUInt32(9));
        Assert.Equal(0x0102030405060708L, reader.ReadInt64(13));
        Assert.Equal(0x1112131415161718UL, reader.ReadUInt64(21));
        Assert.Equal([(byte)0x22, (byte)0x11], reader.ReadBytes(2, 1));
    }

    [Fact]
    public void ReadNullTerminatedStrings_AllEncodings()
    {
        byte[] utf8 = [.. Encoding.UTF8.GetBytes("fox"), 0];
        using MemoryStream utf8Stream = new(utf8);
        using BinaryReader utf8Reader = new(utf8Stream);
        Assert.Equal("fox", utf8Reader.ReadUTF8NullTerminatedString());

        byte[] utf16 = [.. Encoding.Unicode.GetBytes("fox"), 0, 0];
        using MemoryStream utf16Stream = new(utf16);
        using BinaryReader utf16Reader = new(utf16Stream);
        Assert.Equal("fox", utf16Reader.ReadUTF16NullTerminatedString());

        byte[] utf32 = [.. Encoding.UTF32.GetBytes("fox"), 0, 0, 0, 0];
        using MemoryStream utf32Stream = new(utf32);
        using BinaryReader utf32Reader = new(utf32Stream);
        Assert.Equal("fox", utf32Reader.ReadUTF32NullTerminatedString());
    }

    [Fact]
    public void ReadNullTerminatedStrings_AdvanceToByteAfterTerminator()
    {
        byte[] utf8 = [.. Encoding.UTF8.GetBytes("fox"), 0, 0x7A];
        using MemoryStream utf8Stream = new(utf8);
        using BinaryReader utf8Reader = new(utf8Stream);
        Assert.Equal("fox", utf8Reader.ReadUTF8NullTerminatedString());
        Assert.Equal(4L, utf8Stream.Position);
        Assert.Equal((byte)0x7A, utf8Reader.ReadByte());

        byte[] utf16 = [.. Encoding.Unicode.GetBytes("fox"), 0, 0, 0x7A, 0x00];
        using MemoryStream utf16Stream = new(utf16);
        using BinaryReader utf16Reader = new(utf16Stream);
        Assert.Equal("fox", utf16Reader.ReadUTF16NullTerminatedString());
        Assert.Equal(8L, utf16Stream.Position);
        Assert.Equal((short)0x007A, utf16Reader.ReadInt16());

        byte[] utf32 = [.. Encoding.UTF32.GetBytes("fox"), 0, 0, 0, 0, 0x7A, 0x00, 0x00, 0x00];
        using MemoryStream utf32Stream = new(utf32);
        using BinaryReader utf32Reader = new(utf32Stream);
        Assert.Equal("fox", utf32Reader.ReadUTF32NullTerminatedString());
        Assert.Equal(16L, utf32Stream.Position);
        Assert.Equal(0x0000007A, utf32Reader.ReadInt32());
    }

    [Fact]
    public void ReadUtf8NullTerminatedString_ByPosition_WithNoSeek_Throws()
    {
        using NonSeekableStream stream = new([0x41, 0x00]);
        using BinaryReader reader = new(stream);

        Assert.Throws<NotSupportedException>(() => reader.ReadUTF8NullTerminatedString(0));
    }

    [Fact]
    public void ReadUtf8NullTerminatedString_MissingTerminator_ThrowsEndOfStream()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("unterminated"));
        using BinaryReader reader = new(stream);

        Assert.Throws<EndOfStreamException>(() => reader.ReadUTF8NullTerminatedString());
    }

    [Fact]
    public void Align_UsesAlignmentMasking()
    {
        using MemoryStream stream = new(new byte[128], writable: true);
        using BinaryReader reader = new(stream);

        stream.Position = 5;
        long aligned = reader.Align(4);
        Assert.Equal(8L, aligned);

        stream.Position = 11;
        long unchanged = reader.Align(0);
        Assert.Equal(11L, unchanged);
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data, writable: false)
    {
        public override bool CanSeek => false;
    }
}
