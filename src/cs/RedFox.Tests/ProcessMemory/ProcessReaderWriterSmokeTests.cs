// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.IO.ProcessMemory;

namespace RedFox.Tests.ProcessMemory;

public sealed class ProcessReaderWriterSmokeTests
{
    [Fact]
    public unsafe void ReaderAndWriter_CanRoundTripInCurrentProcess()
    {
        int value = 0x12345678;
        int updatedValue = 0x10203040;
        nint address = (nint)(&value);

        using ProcessReader reader = new(Environment.ProcessId);
        using ProcessWriter writer = new(Environment.ProcessId);

        int readInitial = reader.Read<int>(address);
        Assert.Equal(value, readInitial);

        writer.Write(address, updatedValue);
        int readUpdated = reader.Read<int>(address);
        Assert.Equal(updatedValue, readUpdated);
    }

    [Fact]
    public unsafe void ReaderReadAndWriterWrite_WithSpans_WorkOnCurrentProcess()
    {
        byte[] data = [1, 2, 3, 4];
        byte[] replacement = [5, 6, 7, 8];

        fixed (byte* dataPtr = data)
        {
            nint address = (nint)dataPtr;
            Span<byte> readBuffer = stackalloc byte[data.Length];

            using ProcessReader reader = new(Environment.ProcessId);
            using ProcessWriter writer = new(Environment.ProcessId);

            reader.Read(address, readBuffer);
            Assert.Equal(data, readBuffer.ToArray());

            writer.Write(address, replacement);
            reader.Read(address, readBuffer);
            Assert.Equal(replacement, readBuffer.ToArray());
        }
    }
}
