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
        private const string Library = "liblz4";

        public const int MaxInputSize = 0x7E000000;

        [LibraryImport(Library, EntryPoint = "LZ4_compress_default", SetLastError = true)]
        public static partial int Compress(ReadOnlySpan<byte> source, Span<byte> destination, int sourceSize, int destinationCapacity);

        [LibraryImport(Library, EntryPoint = "LZ4_decompress_safe", SetLastError = true)]
        public static partial int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, int compressedSize, int destinationCapacity);

        [LibraryImport(Library, EntryPoint = "LZ4_compressBound", SetLastError = true)]
        public static partial int GetMaxCompressedSize(int sourceSize);

        [LibraryImport(Library, EntryPoint = "LZ4F_compressFrameBound", SetLastError = true)]
        public static partial nuint FrameGetMaxCompressedSize(nuint sourceSize, ReadOnlySpan<byte> preferences);

        [LibraryImport(Library, EntryPoint = "LZ4F_compressFrame", SetLastError = true)]
        public static partial nuint FrameCompress(Span<byte> destination, nuint destinationCapacity, ReadOnlySpan<byte> source, nuint sourceSize, ReadOnlySpan<byte> preferences);

        [LibraryImport(Library, EntryPoint = "LZ4F_createDecompressionContext", SetLastError = true)]
        public static partial nuint FrameCreateDecompressionContext(out nint decompressionContextPointer, uint versionNumber);

        [LibraryImport(Library, EntryPoint = "LZ4F_decompress", SetLastError = true)]
        public static partial nuint FrameDecompress(nint decompressionContextPointer, Span<byte> destination, ref nuint destinationSize, ReadOnlySpan<byte> source, ref nuint sourceSize, ReadOnlySpan<byte> options);

        [LibraryImport(Library, EntryPoint = "LZ4F_freeDecompressionContext", SetLastError = true)]
        public static partial nuint FrameFreeDecompressionContext(nint decompressionContextPointer);

        [LibraryImport(Library, EntryPoint = "LZ4F_getVersion", SetLastError = true)]
        public static partial uint FrameGetVersion();

        [LibraryImport(Library, EntryPoint = "LZ4F_isError", SetLastError = true)]
        public static partial uint FrameIsError(nuint code);

        [LibraryImport(Library, EntryPoint = "LZ4F_getErrorName", SetLastError = true)]
        private static partial nint FrameGetErrorNamePointer(nuint code);

        public static string FrameGetErrorName(nuint code) => Marshal.PtrToStringUTF8(FrameGetErrorNamePointer(code)) ?? "Unknown LZ4 error.";
    }
}
