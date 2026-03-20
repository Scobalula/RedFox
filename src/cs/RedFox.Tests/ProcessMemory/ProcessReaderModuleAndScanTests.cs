// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics;
using RedFox;
using RedFox.IO.ProcessMemory;

namespace RedFox.Tests.ProcessMemory;

public sealed class ProcessReaderModuleAndScanTests
{
    [Fact]
    public void GetMainModule_ReturnsCurrentProcessMainModule()
    {
        using Process process = Process.GetCurrentProcess();
        using ProcessReader processReader = new(Environment.ProcessId);

        ProcessModuleInfo mainModule = processReader.GetMainModule();

        Assert.False(string.IsNullOrWhiteSpace(mainModule.Name));
        Assert.NotEqual(IntPtr.Zero, mainModule.BaseAddress);
        Assert.True(mainModule.Size > 0);
        Assert.True(mainModule.EndAddress > mainModule.BaseAddress);
        Assert.Equal(process.MainModule?.BaseAddress ?? IntPtr.Zero, mainModule.BaseAddress);
    }

    [Fact]
    public void GetModules_ContainsMainModule()
    {
        using Process process = Process.GetCurrentProcess();
        using ProcessReader processReader = new(Environment.ProcessId);

        ProcessModuleInfo mainModule = processReader.GetMainModule();
        ProcessModuleInfo[] modules = processReader.GetModules();

        Assert.NotEmpty(modules);
        Assert.Contains(modules, module => module.BaseAddress == mainModule.BaseAddress && module.Size == mainModule.Size);
    }

    [Fact]
    public void GetModuleBaseAddress_ByMainModuleName_ReturnsMainAddress()
    {
        using ProcessReader processReader = new(Environment.ProcessId);
        ProcessModuleInfo mainModule = processReader.GetMainModule();

        nint moduleBaseAddress = processReader.GetModuleBaseAddress(mainModule.Name);

        Assert.Equal(mainModule.BaseAddress, moduleBaseAddress);
    }

    [Fact]
    public void GetMainModuleBaseAddress_ReturnsMainAddress()
    {
        using ProcessReader processReader = new(Environment.ProcessId);
        ProcessModuleInfo mainModule = processReader.GetMainModule();

        nint moduleBaseAddress = processReader.GetMainModuleBaseAddress();

        Assert.Equal(mainModule.BaseAddress, moduleBaseAddress);
    }

    [Fact]
    public unsafe void Scan_WithHexPattern_ReturnsFirstExpectedAddress()
    {
        byte[] data = [0x00, 0xAB, 0xCD, 0xEF, 0x10, 0xAB, 0xCD, 0xEF];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            nint match = processReader.Scan("AB CD EF", address, address + data.Length);

            Assert.Equal(address + 1, match);
        }
    }

    [Fact]
    public unsafe void Scan_WithWildcardPattern_ReturnsFirstExpectedAddress()
    {
        byte[] data = [0x11, 0xAA, 0xBB, 0xCC, 0x22, 0xAA, 0x00, 0xCC];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            nint match = processReader.Scan("AA ?? CC", address, address + data.Length);

            Assert.Equal(address + 1, match);
        }
    }

    [Fact]
    public unsafe void Scan_WithLeadingWildcardPattern_ReturnsFirstExpectedAddress()
    {
        byte[] data = [0x10, 0x40, 0x50, 0x77, 0x40, 0x50, 0x88];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            nint match = processReader.Scan("?? 40 50", address, address + data.Length);

            Assert.Equal(address, match);
        }
    }

    [Fact]
    public unsafe void ScanAll_WithAllWildcardPattern_ReturnsEveryStartOffset()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);

            nint[] matches = processReader.ScanAll("?? ??", address, address + data.Length);

            Assert.Equal(4, matches.Length);
            Assert.Equal(address, matches[0]);
            Assert.Equal(address + 1, matches[1]);
            Assert.Equal(address + 2, matches[2]);
            Assert.Equal(address + 3, matches[3]);
        }
    }

    [Fact]
    public unsafe void ScanFirst_WithPattern_ReturnsFirstMatch()
    {
        byte[] data = [0x10, 0xAB, 0xCD, 0x20, 0xAB, 0xCD];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);
            Pattern<byte> pattern = BytePattern.Parse("AB CD");

            nint firstMatch = processReader.ScanFirst(pattern, address, address + data.Length);

            Assert.Equal(address + 1, firstMatch);
        }
    }

    [Fact]
    public unsafe void ScanAll_WithPattern_ReturnsAllMatches()
    {
        byte[] data = [0xAB, 0xCD, 0x00, 0xAB, 0xCD, 0x00, 0xAB, 0xCD];
        fixed (byte* dataPointer = data)
        {
            nint address = (nint)dataPointer;
            using ProcessReader processReader = new(Environment.ProcessId);
            Pattern<byte> pattern = BytePattern.Parse("AB CD");

            nint[] matches = processReader.ScanAll(pattern, address, address + data.Length);

            Assert.Equal(3, matches.Length);
            Assert.Equal(address, matches[0]);
            Assert.Equal(address + 3, matches[1]);
            Assert.Equal(address + 6, matches[2]);
        }
    }

    [Fact]
    public unsafe void ScanMainModule_WithMissingPattern_ReturnsNoMatches()
    {
        using ProcessReader processReader = new(Environment.ProcessId);

        nint match = processReader.ScanMainModule("DE AD BE EF 44 33 22 11");

        Assert.Equal(IntPtr.Zero, match);
    }

    [Fact]
    public unsafe void ScanModule_WithMainModuleName_ReturnsEquivalentToMainModuleScan()
    {
        using ProcessReader processReader = new(Environment.ProcessId);
        ProcessModuleInfo mainModule = processReader.GetMainModule();

        nint moduleMatch = processReader.ScanModule("DE AD BE EF 44 33 22 11", mainModule.Name);
        nint mainModuleMatch = processReader.ScanMainModule("DE AD BE EF 44 33 22 11");

        Assert.Equal(mainModuleMatch, moduleMatch);
    }

    [Fact]
    public unsafe void ScanAllModule_WithMainModuleName_ReturnsEquivalentToMainModuleScanAll()
    {
        using ProcessReader processReader = new(Environment.ProcessId);
        ProcessModuleInfo mainModule = processReader.GetMainModule();

        nint[] moduleMatches = processReader.ScanAllModule("DE AD BE EF 44 33 22 11", mainModule.Name);
        nint[] mainModuleMatches = processReader.ScanAllMainModule("DE AD BE EF 44 33 22 11");

        Assert.Equal(mainModuleMatches, moduleMatches);
    }
}
