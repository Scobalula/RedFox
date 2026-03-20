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

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates process discovery and reader creation.
/// </summary>
internal sealed class ProcessFinderSample : ISample
{
    /// <inheritdoc />
    public string Name => "process-find";

    /// <inheritdoc />
    public string Description => "Finds process IDs by name and opens a ProcessReader by PID.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        string processName = arguments.Length > 0 ? arguments[0] : Process.GetCurrentProcess().ProcessName;
        int[] processIds = ProcessFinder.FindProcessIdsByName(processName);

        Console.WriteLine($"Process name: {processName}");
        Console.WriteLine($"Found IDs   : {(processIds.Length == 0 ? "<none>" : string.Join(", ", processIds))}");

        if (processIds.Length == 0)
        {
            return 0;
        }

        using ProcessReader reader = new(processIds[0]);
        ProcessModuleInfo mainModule = reader.GetMainModule();
        Console.WriteLine($"Reader PID  : {reader.ProcessId}");
        Console.WriteLine($"Main module : {mainModule.Name}");
        Console.WriteLine($"Base address: 0x{mainModule.BaseAddress:X}");
        return 0;
    }
}
