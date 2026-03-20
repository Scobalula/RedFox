// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.IO.ProcessMemory;

namespace RedFox.Tests.ProcessMemory;

public sealed class ProcessReaderWriterTypedMethodsTests
{
    [Fact]
    public unsafe void ReadWriteInt32_RoundTrips()
    {
        int value = 1234;
        nint address = (nint)(&value);

        using ProcessReader processReader = new(Environment.ProcessId);
        using ProcessWriter processWriter = new(Environment.ProcessId);

        processWriter.WriteInt32(address, 987654321);
        int result = processReader.ReadInt32(address);

        Assert.Equal(987654321, result);
    }

    [Fact]
    public unsafe void ReadWritePointer_WithBit32_RoundTrips()
    {
        uint pointerStorage = 0;
        nint pointerAddress = (nint)(&pointerStorage);
        nint expectedPointer = (nint)0x10203040;

        using ProcessReader processReader = new(Environment.ProcessId);
        using ProcessWriter processWriter = new(Environment.ProcessId);

        processWriter.WritePointer(pointerAddress, expectedPointer, ProcessPointerSize.Bit32);
        nint result = processReader.ReadPointer(pointerAddress, ProcessPointerSize.Bit32);

        Assert.Equal(expectedPointer, result);
    }

    [Fact]
    public unsafe void ReadWritePointer_WithBit64_RoundTrips()
    {
        ulong pointerStorage = 0;
        nint pointerAddress = (nint)(&pointerStorage);
        nint expectedPointer = IntPtr.Size == 8 ? unchecked((nint)0x1122334455667788UL) : (nint)0x55667788;

        using ProcessReader processReader = new(Environment.ProcessId);
        using ProcessWriter processWriter = new(Environment.ProcessId);

        processWriter.WritePointer(pointerAddress, expectedPointer, ProcessPointerSize.Bit64);
        nint result = processReader.ReadPointer(pointerAddress, ProcessPointerSize.Bit64);

        Assert.Equal(expectedPointer, result);
    }

    [Fact]
    public unsafe void ReadWriteUtf8String_RoundTrips()
    {
        byte[] buffer = new byte[128];
        fixed (byte* bufferPointer = buffer)
        {
            nint address = (nint)bufferPointer;
            using ProcessReader processReader = new(Environment.ProcessId);
            using ProcessWriter processWriter = new(Environment.ProcessId);

            processWriter.WriteUtf8String(address, "RedFox UTF8");
            string result = processReader.ReadUtf8String(address, buffer.Length);
            Assert.Equal("RedFox UTF8", result);
        }
    }

    [Fact]
    public unsafe void ReadWriteAsciiString_RoundTrips()
    {
        byte[] buffer = new byte[128];
        fixed (byte* bufferPointer = buffer)
        {
            nint address = (nint)bufferPointer;
            using ProcessReader processReader = new(Environment.ProcessId);
            using ProcessWriter processWriter = new(Environment.ProcessId);

            processWriter.WriteAsciiString(address, "RedFox ASCII");
            string result = processReader.ReadAsciiString(address, buffer.Length);
            Assert.Equal("RedFox ASCII", result);
        }
    }

    [Fact]
    public unsafe void ReadWriteUtf16String_RoundTrips()
    {
        byte[] buffer = new byte[128];
        fixed (byte* bufferPointer = buffer)
        {
            nint address = (nint)bufferPointer;
            using ProcessReader processReader = new(Environment.ProcessId);
            using ProcessWriter processWriter = new(Environment.ProcessId);

            processWriter.WriteUtf16String(address, "RedFox UTF16");
            string result = processReader.ReadUtf16String(address, buffer.Length);
            Assert.Equal("RedFox UTF16", result);
        }
    }

    [Fact]
    public unsafe void ReadString_WithMissingTerminator_Throws()
    {
        byte[] buffer = Encoding.UTF8.GetBytes("unterminated");
        fixed (byte* bufferPointer = buffer)
        {
            nint address = (nint)bufferPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            Assert.Throws<InvalidOperationException>(() => processReader.ReadUtf8String(address, buffer.Length));
        }
    }
}
