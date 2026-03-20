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

public sealed class ProcessReaderWriterValidationTests
{
    [Fact]
    public void ReaderCtor_WithInvalidPid_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ProcessReader(0));
    }

    [Fact]
    public void WriterCtor_WithInvalidPid_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ProcessWriter(0));
    }

    [Fact]
    public void ReaderCtor_WithMissingProcessName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _ = new ProcessReader("redfox_process_should_not_exist_12345"));
    }

    [Fact]
    public void WriterCtor_WithMissingProcessName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _ = new ProcessWriter("redfox_process_should_not_exist_12345"));
    }

    [Fact]
    public void ReaderRead_WithZeroAddress_Throws()
    {
        using ProcessReader reader = new(Environment.ProcessId);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadBytes(0, 1));
    }

    [Fact]
    public void WriterWrite_WithZeroAddress_Throws()
    {
        using ProcessWriter writer = new(Environment.ProcessId);

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteBytes(0, [1]));
    }

    [Fact]
    public void ReaderReadBytes_WithNegativeCount_Throws()
    {
        using ProcessReader reader = new(Environment.ProcessId);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadBytes((nint)1, -1));
    }

    [Fact]
    public void ReaderCtor_WithNullProcess_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new ProcessReader((Process)null!));
    }

    [Fact]
    public void WriterCtor_WithNullProcess_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new ProcessWriter((Process)null!));
    }

    [Fact]
    public void ReaderOpenByName_WithMissingProcessName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _ = ProcessReader.OpenByName("redfox_missing_process_12345"));
    }

    [Fact]
    public void WriterOpenByName_WithMissingProcessName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _ = ProcessWriter.OpenByName("redfox_missing_process_12345"));
    }

    [Fact]
    public void ReaderFindProcessIdsByName_ForCurrentProcessName_ReturnsCurrentProcessId()
    {
        using Process current = Process.GetCurrentProcess();
        int[] ids = ProcessReader.FindProcessIdsByName(current.ProcessName);

        Assert.Contains(current.Id, ids);
    }

    [Fact]
    public void WriterFindProcessIdsByName_ForCurrentProcessName_ReturnsCurrentProcessId()
    {
        using Process current = Process.GetCurrentProcess();
        int[] ids = ProcessWriter.FindProcessIdsByName(current.ProcessName);

        Assert.Contains(current.Id, ids);
    }

    [Fact]
    public void ReaderFindProcessesByName_ForCurrentProcessName_ReturnsCurrentProcess()
    {
        using Process current = Process.GetCurrentProcess();
        Process[] processes = ProcessReader.FindProcessesByName(current.ProcessName);

        try
        {
            Assert.Contains(processes, process => process.Id == current.Id);
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    [Fact]
    public void WriterFindProcessesByName_ForCurrentProcessName_ReturnsCurrentProcess()
    {
        using Process current = Process.GetCurrentProcess();
        Process[] processes = ProcessWriter.FindProcessesByName(current.ProcessName);

        try
        {
            Assert.Contains(processes, process => process.Id == current.Id);
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }
}
