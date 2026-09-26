// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.GDeflate
{
    internal partial class GDeflateInterop
    {
        private const string Library = "Native\\libgdeflate";

        [LibraryImport(Library, EntryPoint = "libdeflate_alloc_gdeflate_decompressor", SetLastError = true)]
        public static partial nint CreateDecompressor();

        [LibraryImport(Library, EntryPoint = "libdeflate_free_gdeflate_decompressor")]
        public static partial void FreeDecompressor(nint decompressor);

        [LibraryImport(Library, EntryPoint = "libdeflate_gdeflate_decompress", SetLastError = true)]
        public static partial int Decompress(nint decompressor, ReadOnlySpan<GDeflatePage> pages, nuint pageCount, Span<byte> output, nuint availableOutputSize, out nuint returnedSize);
    }
}
