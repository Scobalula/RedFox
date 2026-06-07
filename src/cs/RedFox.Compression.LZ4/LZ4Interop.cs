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

        [LibraryImport(Library, EntryPoint = "LZ4F_createDecompressionContext", SetLastError = true)]
        public static partial int FrameCreateDecompressionContext(ref nuint decompressionContextPointer, uint versionNumber);

        [LibraryImport(Library, EntryPoint = "LZ4F_decompress", SetLastError = true)]
        public static partial int FrameDecompress(nuint decompressionContextPointer, Span<byte> dst, ref nuint dstSize, ReadOnlySpan<byte> src, ref nuint srcSize, ReadOnlySpan<byte> opt);

        [LibraryImport(Library, EntryPoint = "LZ4F_freeDecompressionContext", SetLastError = true)]
        public static partial int FrameFreeDecompressionContext(nuint decompressionContextPointer);

        [LibraryImport(Library, EntryPoint = "LZ4F_getVersion", SetLastError = true)]
        public static partial uint FrameGetVersion();

        [LibraryImport(Library, EntryPoint = "LZ4F_isError", SetLastError = true)]
        public static partial uint FrameIsError(int code);

        [LibraryImport(Library, EntryPoint = "LZ4F_getErrorName", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial string FrameGetErrorName(int code);
    }
}
