// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.IO.ProcessMemory
{
    /// <summary>
    /// Describes the pointer width used when reading or writing pointer values in process memory.
    /// </summary>
    public enum ProcessPointerSize
    {
        /// <summary>
        /// Uses the pointer size of the current runtime process.
        /// </summary>
        Native = 0,

        /// <summary>
        /// Uses a 32-bit pointer width.
        /// </summary>
        Bit32 = 32,

        /// <summary>
        /// Uses a 64-bit pointer width.
        /// </summary>
        Bit64 = 64,
    }
}
