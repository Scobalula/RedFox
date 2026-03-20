// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.IO.ProcessMemory.Internal
{
    internal static class ProcessMemoryBackendFactory
    {
        public static IProcessMemoryBackend Create(int processId)
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsProcessMemoryBackend(processId);
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxProcessMemoryBackend(processId);
            }

            throw ProcessMemoryErrors.CreateUnsupportedPlatformException("access");
        }
    }
}
