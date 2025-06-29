// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.ZStandard
{
    internal partial class ZStandardInterop
    {
        private const string ZSTDLibrary = "Native\\libzstd";

        [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_compress", SetLastError = true)]
        public static partial nuint Compress(Span<byte> dest, nuint destCapacity, ReadOnlySpan<byte> src, nuint srcSize, int compressionLevel);

        [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_decompress", SetLastError = true)]
        public static partial nuint Decompress(Span<byte> dest, nuint destCapacity, ReadOnlySpan<byte> src, nuint compressedSize);

        [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_isError", SetLastError = true)]
        public static partial byte IsError(nuint code);

        [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_getErrorName", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial string GetErrorName(nuint code);

        [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_compressBound", SetLastError = true)]
        public static partial nuint GetMaxCompressedSize(nuint srcSize);

        [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_getFrameContentSize", SetLastError = true)]
        public static partial nuint GetDecompressedSize(ReadOnlySpan<byte> src, nuint srcSize);
    }
}
