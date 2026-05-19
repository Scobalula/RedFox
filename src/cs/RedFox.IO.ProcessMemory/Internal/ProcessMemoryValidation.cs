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
        private const uint BaseProtectionMask = 0xFF;
        private const uint ProtectionModifierMask =
            (uint)(ProcessMemoryProtection.Guard | ProcessMemoryProtection.NoCache | ProcessMemoryProtection.WriteCombine);
        private const uint KnownProtectionMask = BaseProtectionMask | ProtectionModifierMask;

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

        public static void ThrowIfInvalidLength(int length, string paramName)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, length, "Length must be greater than zero.");
            }
        }

        public static void ThrowIfInvalidProtection(ProcessMemoryProtection protection)
        {
            uint rawProtection = (uint)protection;
            uint baseProtection = rawProtection & BaseProtectionMask;
            bool hasUnknownFlags = (rawProtection & ~KnownProtectionMask) != 0;
            bool hasValidBaseProtection = baseProtection is
                (uint)ProcessMemoryProtection.NoAccess or
                (uint)ProcessMemoryProtection.ReadOnly or
                (uint)ProcessMemoryProtection.ReadWrite or
                (uint)ProcessMemoryProtection.WriteCopy or
                (uint)ProcessMemoryProtection.Execute or
                (uint)ProcessMemoryProtection.ExecuteRead or
                (uint)ProcessMemoryProtection.ExecuteReadWrite or
                (uint)ProcessMemoryProtection.ExecuteWriteCopy;

            if (rawProtection == 0 || hasUnknownFlags || !hasValidBaseProtection)
            {
                const string message = "Protection must include exactly one base protection and only supported modifier flags.";
                throw new ArgumentOutOfRangeException(nameof(protection), protection, message);
            }
        }
    }
}
