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
    internal static class ProcessMemoryValidation
    {
        public static void ThrowIfInvalidProcessId(int processId)
        {
            if (processId <= 0)
            {
                string message = "Process ID must be greater than zero.";
                throw new ArgumentOutOfRangeException(nameof(processId), processId, message);
            }
        }

        public static void ThrowIfInvalidAddress(nint address)
        {
            if (address == IntPtr.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(address), "Memory address cannot be zero.");
            }
        }
    }
}
