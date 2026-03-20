// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.IO.ProcessMemory.Internal
{
    internal static partial class WindowsProcessNativeMethods
    {
        private const uint ProcessVmOperation = 0x0008;
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessVmWrite = 0x0020;
        private const uint ProcessQueryInformation = 0x0400;
        private const uint ProcessAccess = ProcessVmRead | ProcessVmWrite | ProcessVmOperation | ProcessQueryInformation;

        public static SafeProcessHandle OpenProcessHandle(int processId)
        {
            SafeProcessHandle handle = OpenProcess(ProcessAccess, 0, processId);

            if (handle.IsInvalid)
            {
                int errorCode = Marshal.GetLastPInvokeError();
                throw ProcessMemoryErrors.CreateOpenProcessException(processId, errorCode);
            }

            return handle;
        }

        [LibraryImport("kernel32", SetLastError = true)]
        private static partial SafeProcessHandle OpenProcess(uint desiredAccess, int inheritHandle, int processId);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReadProcessMemory(SafeProcessHandle processHandle, nint baseAddress, Span<byte> buffer, nuint size, out nint numberOfBytesRead);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool WriteProcessMemory(SafeProcessHandle processHandle, nint baseAddress, ReadOnlySpan<byte> buffer, nuint size, out nint numberOfBytesWritten);
    }
}
