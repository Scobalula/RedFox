// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.IO.ProcessMemory;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates process memory read/write helpers against the current process.
/// </summary>
internal sealed class ProcessReadWriteSample : ISample
{
    /// <inheritdoc />
    public string Name => "process-read-write";

    /// <inheritdoc />
    public string Description => "Reads and writes primitive values and pointer targets in current process memory.";

    /// <inheritdoc />
    public unsafe int Run(string[] arguments)
    {
        int value = 123;
        int pointedValue = 456;
        nint valueAddress = (nint)(&value);
        nint pointedValueAddress = (nint)(&pointedValue);
        nint pointerStorageAddress;

        if (IntPtr.Size == 8)
        {
            ulong pointerStorage = 0;
            pointerStorageAddress = (nint)(&pointerStorage);
            return RunWithPointerStorage(pointerStorageAddress, valueAddress, pointedValueAddress);
        }

        uint pointerStorage32 = 0;
        pointerStorageAddress = (nint)(&pointerStorage32);
        return RunWithPointerStorage(pointerStorageAddress, valueAddress, pointedValueAddress);
    }

    private static int RunWithPointerStorage(nint pointerStorageAddress, nint valueAddress, nint pointedValueAddress)
    {
        using ProcessReader processReader = new(Environment.ProcessId);
        using ProcessWriter processWriter = new(Environment.ProcessId);

        processWriter.WriteInt32(valueAddress, 789);
        int directReadValue = processReader.ReadInt32(valueAddress);

        processWriter.WritePointer(pointerStorageAddress, pointedValueAddress, ProcessPointerSize.Native);
        int pointerReadValue = processReader.ReadPointer<int>(pointerStorageAddress, ProcessPointerSize.Native);

        Console.WriteLine($"Direct int read/write value: {directReadValue}");
        Console.WriteLine($"Pointer target value: {pointerReadValue}");
        Console.WriteLine($"Main module base: 0x{processReader.GetMainModuleBaseAddress():X}");
        return 0;
    }
}
