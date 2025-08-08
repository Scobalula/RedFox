// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.LZ4
{
    internal partial class LZ4Interop
    {
        private const string Library = "Native\\liblz4";

        public const int MaxInputSize = 0x7E000000;

        [LibraryImport(Library, EntryPoint = "LZ4_compress_fast", SetLastError = true)]
        public static partial int Compress(ReadOnlySpan<byte> src, Span<byte> dst, int srcSize, int dstCapacity);

        [LibraryImport(Library, EntryPoint = "LZ4_decompress_safe", SetLastError = true)]
        public static partial int Decompress(ReadOnlySpan<byte> src, Span<byte> dst, int compressedSize, int dstCapacity);

        [LibraryImport(Library, EntryPoint = "LZ4_compressBound", SetLastError = true)]
        public static partial int GetMaxCompressedSize(int srcSize);
    }
}
