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
    /// Defines page protection flags for process memory regions.
    /// </summary>
    /// <remarks>
    /// Combine exactly one base protection value with optional modifier flags such as
    /// <see cref="Guard"/>, <see cref="NoCache"/>, or <see cref="WriteCombine"/>.
    /// </remarks>
    [Flags]
    public enum ProcessMemoryProtection : uint
    {
        /// <summary>
        /// Disables all access to the region.
        /// </summary>
        NoAccess = 0x01,

        /// <summary>
        /// Allows read-only access to the region.
        /// </summary>
        ReadOnly = 0x02,

        /// <summary>
        /// Allows read and write access to the region.
        /// </summary>
        ReadWrite = 0x04,

        /// <summary>
        /// Enables copy-on-write access to the region.
        /// </summary>
        WriteCopy = 0x08,

        /// <summary>
        /// Allows execute-only access to the region.
        /// </summary>
        Execute = 0x10,

        /// <summary>
        /// Allows execute and read access to the region.
        /// </summary>
        ExecuteRead = 0x20,

        /// <summary>
        /// Allows execute, read, and write access to the region.
        /// </summary>
        ExecuteReadWrite = 0x40,

        /// <summary>
        /// Enables execute and copy-on-write access to the region.
        /// </summary>
        ExecuteWriteCopy = 0x80,

        /// <summary>
        /// Marks the region as a guard page.
        /// </summary>
        Guard = 0x100,

        /// <summary>
        /// Disables caching for the region.
        /// </summary>
        NoCache = 0x200,

        /// <summary>
        /// Enables write-combining for the region.
        /// </summary>
        WriteCombine = 0x400,
    }
}
