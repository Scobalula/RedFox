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

public sealed class ProcessFinderTests
{
    [Fact]
    public void FindProcessIdsByName_ReturnsCurrentProcess()
    {
        using Process current = Process.GetCurrentProcess();
        string processName = current.ProcessName;

        int[] ids = ProcessFinder.FindProcessIdsByName(processName);

        Assert.Contains(current.Id, ids);
    }

    [Fact]
    public void FindProcessIdsByName_AcceptsExeSuffix()
    {
        using Process current = Process.GetCurrentProcess();
        string processName = $"{current.ProcessName}.exe";

        int[] ids = ProcessFinder.FindProcessIdsByName(processName);

        Assert.Contains(current.Id, ids);
    }

    [Fact]
    public void FindProcessesByName_ReturnsProcesses()
    {
        using Process current = Process.GetCurrentProcess();
        string processName = current.ProcessName;

        Process[] processes = ProcessFinder.FindProcessesByName(processName);
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
    public void FindProcessIdsByName_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => ProcessFinder.FindProcessIdsByName(" "));
    }
}
