// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.ZStandard;

internal partial class ZStandardInterop
{
    private const string ZSTDLibrary = "libzstd";

    [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_compress", SetLastError = true)]
    public static partial nuint Compress(Span<byte> destination, nuint destinationCapacity, ReadOnlySpan<byte> source, nuint sourceSize, int compressionLevel);

    [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_decompress", SetLastError = true)]
    public static partial nuint Decompress(Span<byte> destination, nuint destinationCapacity, ReadOnlySpan<byte> source, nuint compressedSize);

    [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_isError", SetLastError = true)]
    public static partial byte IsError(nuint code);

    [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_getErrorName", SetLastError = true)]
    private static partial nint GetErrorNamePointer(nuint code);

    public static string GetErrorName(nuint code) => Marshal.PtrToStringUTF8(GetErrorNamePointer(code)) ?? "Unknown Zstandard error.";

    [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_compressBound", SetLastError = true)]
    public static partial nuint GetMaxCompressedSize(nuint sourceSize);

    [LibraryImport(ZSTDLibrary, EntryPoint = "ZSTD_getFrameContentSize", SetLastError = true)]
    public static partial nuint GetDecompressedSize(ReadOnlySpan<byte> source, nuint sourceSize);
}
