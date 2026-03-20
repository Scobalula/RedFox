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
    internal static partial class LinuxProcessNativeMethods
    {
        [LibraryImport("libc", SetLastError = true, EntryPoint = "process_vm_readv")]
        public static unsafe partial nint ProcessVmReadv(int processId, LinuxIovec* localIov, nuint localIovCount, LinuxIovec* remoteIov, nuint remoteIovCount, nuint flags);

        [LibraryImport("libc", SetLastError = true, EntryPoint = "process_vm_writev")]
        public static unsafe partial nint ProcessVmWritev(int processId, LinuxIovec* localIov, nuint localIovCount, LinuxIovec* remoteIov, nuint remoteIovCount, nuint flags);
    }
}
