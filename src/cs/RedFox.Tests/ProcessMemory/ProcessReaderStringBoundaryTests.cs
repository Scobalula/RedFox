// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;
using System.Text;
using RedFox.IO.ProcessMemory;

namespace RedFox.Tests.ProcessMemory;

public sealed partial class ProcessReaderStringBoundaryTests
{
    [Fact]
    public unsafe void ReadUtf8String_CrossesBoundaryWithoutFailing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        nuint pageSize = GetPageSize();
        nuint allocationSize = pageSize * 2;
        nint memory = VirtualAlloc(nint.Zero, allocationSize, MemCommit | MemReserve, PageReadWrite);
        if (memory == IntPtr.Zero)
        {
            throw new InvalidOperationException("VirtualAlloc failed.");
        }

        try
        {
            byte* firstPage = (byte*)memory;
            byte* secondPage = firstPage + pageSize;
            bool protectResult = VirtualProtect((nint)secondPage, pageSize, PageNoAccess, out uint oldProtectValue);
            if (!protectResult)
            {
                throw new InvalidOperationException("VirtualProtect failed.");
            }

            string expectedValue = "BoundaryString";
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedValue);
            int startOffset = (int)pageSize - (expectedBytes.Length + 1);
            byte* stringStartPointer = firstPage + startOffset;
            for (int index = 0; index < expectedBytes.Length; index++)
            {
                stringStartPointer[index] = expectedBytes[index];
            }

            stringStartPointer[expectedBytes.Length] = 0;

            using ProcessReader processReader = new(Environment.ProcessId);
            string result = processReader.ReadUtf8String((nint)stringStartPointer, expectedBytes.Length + 64);
            Assert.Equal(expectedValue, result);

            bool restoreResult = VirtualProtect((nint)secondPage, pageSize, oldProtectValue, out _);
            if (!restoreResult)
            {
                throw new InvalidOperationException("VirtualProtect restore failed.");
            }
        }
        finally
        {
            bool freeResult = VirtualFree(memory, 0, MemRelease);
        }
    }

    private static nuint GetPageSize()
    {
        GetSystemInfo(out SystemInfo systemInfo);
        return systemInfo.PageSize;
    }

    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;
    private const uint PageNoAccess = 0x01;

    [LibraryImport("kernel32", SetLastError = true)]
    private static partial nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protect);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualProtect(nint address, nuint size, uint newProtect, out uint oldProtect);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualFree(nint address, nuint size, uint freeType);

    [LibraryImport("kernel32")]
    private static partial void GetSystemInfo(out SystemInfo systemInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemInfo
    {
        public ushort ProcessorArchitecture;
        public ushort Reserved;
        public uint PageSize;
        public nint MinimumApplicationAddress;
        public nint MaximumApplicationAddress;
        public nint ActiveProcessorMask;
        public uint NumberOfProcessors;
        public uint ProcessorType;
        public uint AllocationGranularity;
        public ushort ProcessorLevel;
        public ushort ProcessorRevision;
    }
}
