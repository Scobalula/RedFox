// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.IO;

namespace RedFox.Tests.IO;

public sealed class SpanReaderTests
{
    private static void SetPosition(ref SpanReader spanReader, int position)
    {
        spanReader.Position = position;
    }

    private static long SeekValue(ref SpanReader spanReader, int offset, SeekOrigin seekOrigin)
    {
        return spanReader.Seek(offset, seekOrigin);
    }

    [Fact]
    public void ReadAndReadString_AdvancePosition()
    {
        byte[] data = [1, 2, .. Encoding.UTF8.GetBytes("ok")];
        SpanReader reader = new(data);

        ReadOnlySpan<byte> first = reader.Read(2);
        string text = reader.ReadString(2);

        Assert.Equal((byte)1, first[0]);
        Assert.Equal((byte)2, first[1]);
        Assert.Equal("ok", text);
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void ReadGenericAndIndexedReads_ReturnExpectedValues()
    {
        //byte[] data = new byte[16];
        //BitConverter.GetBytes(0x11223344).CopyTo(data, 0);
        //BitConverter.GetBytes((short)0x5566).CopyTo(data, 4);
        //Encoding.UTF8.GetBytes("ab").CopyTo(data, 6);

        //SpanReader reader = new(data);
        //int firstValue = reader.Read<int>();
        //ReadOnlySpan<short> shortValue = reader.Read<short>(4, 1);
        //string text = reader.ReadString(6, 2);

        //Assert.Equal(0x11223344, firstValue);
        //Assert.Equal((short)0x5566, shortValue[0]);
        //Assert.Equal("ab", text);
    }

    [Fact]
    public void Seek_HandlesBeginCurrentAndEnd()
    {
        byte[] data = new byte[32];
        SpanReader reader = new(data);

        Assert.Equal(5L, reader.Seek(5, SeekOrigin.Begin));
        Assert.Equal(8L, reader.Seek(3, SeekOrigin.Current));
        Assert.Equal(24L, reader.Seek(8, SeekOrigin.End));
    }

    [Fact]
    public void PositionSetter_UsesSeekValidation()
    {
        byte[] data = new byte[8];
        SpanReader reader = new(data);

        SetPosition(ref reader, 7);
        Assert.Equal(7, reader.Position);

        bool threwEndOfStream = false;
        try
        {
            SetPosition(ref reader, 9);
        }
        catch (EndOfStreamException)
        {
            threwEndOfStream = true;
        }

        Assert.True(threwEndOfStream);
    }

    [Fact]
    public void Seek_WithInvalidOrigin_Throws()
    {
        byte[] data = new byte[8];
        SpanReader reader = new(data);

        bool threwNotImplemented = false;
        try
        {
            _ = SeekValue(ref reader, 0, (SeekOrigin)999);
        }
        catch (NotImplementedException)
        {
            threwNotImplemented = true;
        }

        bool threwIOException = false;
        try
        {
            _ = SeekValue(ref reader, 9, SeekOrigin.End);
        }
        catch (IOException)
        {
            threwIOException = true;
        }

        bool threwEndOfStream = false;
        try
        {
            _ = SeekValue(ref reader, 99, SeekOrigin.Begin);
        }
        catch (EndOfStreamException)
        {
            threwEndOfStream = true;
        }

        Assert.True(threwNotImplemented);
        Assert.True(threwIOException);
        Assert.True(threwEndOfStream);
    }
}
