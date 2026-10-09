// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.LZ4;

internal partial class LZ4Interop
{
    [LibraryImport("liblz4", EntryPoint = "LZ4_createStream")]
    internal static partial nint CreateStream();

    [LibraryImport("liblz4", EntryPoint = "LZ4_freeStream")]
    internal static partial int FreeStream(nint stream);

    [LibraryImport("liblz4", EntryPoint = "LZ4_resetStream_fast")]
    internal static partial void ResetStream(nint stream);

    [LibraryImport("liblz4", EntryPoint = "LZ4_loadDict")]
    internal static partial int LoadDictionary(nint stream, nint dictionary, int size);

    [LibraryImport("liblz4", EntryPoint = "LZ4_attach_dictionary")]
    internal static partial void AttachDictionary(nint stream, nint dictionaryStream);

    [LibraryImport("liblz4", EntryPoint = "LZ4_compress_fast_continue")]
    internal static partial int Compress(nint stream, ReadOnlySpan<byte> source, Span<byte> destination, int sourceSize, int capacity, int acceleration);

    [LibraryImport("liblz4", EntryPoint = "LZ4_decompress_safe_usingDict")]
    internal static partial int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, int sourceSize, int capacity, ReadOnlySpan<byte> dictionary, int dictionarySize);

    [LibraryImport("liblz4", EntryPoint = "LZ4_compressBound")]
    internal static partial int GetMaxCompressedSize(int size);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_createCompressionContext")]
    internal static partial nuint CreateCompressionContext(out nint context, uint version);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_createDecompressionContext")]
    internal static partial nuint CreateDecompressionContext(out nint context, uint version);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_freeCompressionContext")]
    internal static partial nuint FreeCompressionContext(nint context);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_freeDecompressionContext")]
    internal static partial nuint FreeDecompressionContext(nint context);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_resetDecompressionContext")]
    internal static partial void ResetDecompressionContext(nint context);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_createCDict")]
    internal static partial nint CreateDictionary(ReadOnlySpan<byte> dictionary, nuint size);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_freeCDict")]
    internal static partial void FreeDictionary(nint dictionary);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_compressFrameBound")]
    internal static partial nuint FrameBound(nuint size, in LZ4FramePreferences preferences);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_compressFrame_usingCDict")]
    internal static partial nuint FrameCompress(nint context, Span<byte> destination, nuint capacity, ReadOnlySpan<byte> source, nuint size, nint dictionary, in LZ4FramePreferences preferences);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_compressBegin_usingCDict")]
    internal static partial nuint Begin(nint context, Span<byte> destination, nuint capacity, nint dictionary, in LZ4FramePreferences preferences);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_compressUpdate")]
    internal static partial nuint Update(nint context, Span<byte> destination, nuint capacity, ReadOnlySpan<byte> source, nuint size, nint options);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_flush")]
    internal static partial nuint Flush(nint context, Span<byte> destination, nuint capacity, nint options);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_compressEnd")]
    internal static partial nuint End(nint context, Span<byte> destination, nuint capacity, nint options);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_decompress_usingDict")]
    internal static partial nuint FrameDecompress(nint context, Span<byte> destination, ref nuint capacity, ReadOnlySpan<byte> source, ref nuint size, ReadOnlySpan<byte> dictionary, nuint dictionarySize, nint options);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_getFrameInfo")]
    internal static partial nuint GetFrameInfo(nint context, out LZ4FrameInfo info, ReadOnlySpan<byte> source, ref nuint size);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_isError")]
    internal static partial uint IsError(nuint code);

    [LibraryImport("liblz4", EntryPoint = "LZ4F_getErrorName")]
    internal static partial nint GetErrorName(nuint code);

    internal static nuint Check(nuint result)
    {
        if (IsError(result) != 0)
            throw new CompressionException($"LZ4 frame failed: {Marshal.PtrToStringUTF8(GetErrorName(result))}");

        return result;
    }
}
