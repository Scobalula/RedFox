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
    /// Represents metadata for a module loaded into a target process.
    /// </summary>
    /// <param name="name">The module file name.</param>
    /// <param name="filePath">The full module path when available.</param>
    /// <param name="baseAddress">The base address where the module is mapped.</param>
    /// <param name="size">The module image size in bytes.</param>
    public readonly record struct ProcessModuleInfo(string name, string? filePath, nint baseAddress, int size)
    {
        /// <summary>
        /// Gets the module file name.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the full module path when available.
        /// </summary>
        public string? FilePath { get; } = filePath;

        /// <summary>
        /// Gets the base address where the module is mapped.
        /// </summary>
        public nint BaseAddress { get; } = baseAddress;

        /// <summary>
        /// Gets the module image size in bytes.
        /// </summary>
        public int Size { get; } = size;

        /// <summary>
        /// Gets the exclusive end address of the mapped module.
        /// </summary>
        public nint EndAddress => BaseAddress + Size;

    }
}
