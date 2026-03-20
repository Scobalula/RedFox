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

public sealed class ProcessReaderChunkScanTests
{
    [Fact]
    public unsafe void ProcessReaderScan_PatternAcrossChunkBoundary_ReturnsExpectedMatch()
    {
        byte[] data = [0x01, 0x02, 0xAA, 0xBB, 0xCC, 0x03];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            nint match = processReader.Scan("AA BB CC", address, address + data.Length, chunkSize: 4);
            Assert.Equal(address + 2, match);
        }
    }

    [Fact]
    public unsafe void ProcessReaderScan_OverlapFallbackCase_ReturnsExpectedMatch()
    {
        byte[] data = [0xAB, 0xAB, 0xAC, 0x00];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            nint match = processReader.Scan("AB AC", address, address + data.Length, chunkSize: 2);
            Assert.Equal(address + 1, match);
        }
    }
}
