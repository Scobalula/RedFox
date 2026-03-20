// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics;
using RedFox.IO.ProcessMemory;

namespace RedFox.Tests.ProcessMemory;

public sealed class ProcessMemoryMetadataTests
{
    [Fact]
    public void ProcessModuleInfo_StoresProvidedValues()
    {
        ProcessModuleInfo moduleInfo = new("mod.dll", "C:\\mod.dll", (nint)0x1000, 0x200);

        Assert.Equal("mod.dll", moduleInfo.Name);
        Assert.Equal("C:\\mod.dll", moduleInfo.FilePath);
        Assert.Equal((nint)0x1000, moduleInfo.BaseAddress);
        Assert.Equal(0x200, moduleInfo.Size);
        Assert.Equal((nint)0x1200, moduleInfo.EndAddress);
    }

    [Fact]
    public void ProcessPointerSize_EnumValues_AreStable()
    {
        Assert.Equal(0, (int)ProcessPointerSize.Native);
        Assert.Equal(32, (int)ProcessPointerSize.Bit32);
        Assert.Equal(64, (int)ProcessPointerSize.Bit64);
    }

    [Fact]
    public void OpenByName_ForCurrentProcess_WorksForReaderAndWriter()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessReader reader = ProcessReader.OpenByName(currentProcess.ProcessName);
        using ProcessWriter writer = ProcessWriter.OpenByName(currentProcess.ProcessName);

        Assert.Equal(currentProcess.Id, reader.ProcessId);
        Assert.Equal(currentProcess.Id, writer.ProcessId);
    }

    [Fact]
    public void WriterModuleAccessors_MatchReader()
    {
        using ProcessReader reader = new(Environment.ProcessId);
        using ProcessWriter writer = new(Environment.ProcessId);

        ProcessModuleInfo readerMainModule = reader.GetMainModule();
        ProcessModuleInfo writerMainModule = writer.GetMainModule();

        Assert.Equal(readerMainModule.BaseAddress, writerMainModule.BaseAddress);
        Assert.Equal(readerMainModule.Name, writerMainModule.Name);
        Assert.Equal(reader.GetMainModuleBaseAddress(), writer.GetMainModuleBaseAddress());
        Assert.Equal(reader.GetModuleBaseAddress(readerMainModule.Name), writer.GetModuleBaseAddress(writerMainModule.Name));
    }
}
