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
    [LibraryImport("libzstd", EntryPoint = "ZSTD_createCCtx")]
    internal static partial nint CreateCompressionContext();

    [LibraryImport("libzstd", EntryPoint = "ZSTD_createDCtx")]
    internal static partial nint CreateDecompressionContext();

    [LibraryImport("libzstd", EntryPoint = "ZSTD_freeCCtx")]
    internal static partial nuint FreeCompressionContext(nint context);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_freeDCtx")]
    internal static partial nuint FreeDecompressionContext(nint context);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_createCDict")]
    internal static partial nint CreateCompressionDictionary(ReadOnlySpan<byte> dictionary, nuint size, int level);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_createDDict")]
    internal static partial nint CreateDecompressionDictionary(ReadOnlySpan<byte> dictionary, nuint size);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_freeCDict")]
    internal static partial nuint FreeCompressionDictionary(nint dictionary);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_freeDDict")]
    internal static partial nuint FreeDecompressionDictionary(nint dictionary);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_CCtx_refCDict")]
    internal static partial nuint ReferenceCompressionDictionary(nint context, nint dictionary);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_DCtx_refDDict")]
    internal static partial nuint ReferenceDecompressionDictionary(nint context, nint dictionary);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_CCtx_setParameter")]
    internal static partial nuint SetParameter(nint context, int parameter, int value);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_CCtx_setPledgedSrcSize")]
    internal static partial nuint SetPledgedSize(nint context, ulong size);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_CCtx_reset")]
    internal static partial nuint ResetCompression(nint context, int directive);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_DCtx_reset")]
    internal static partial nuint ResetDecompression(nint context, int directive);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_compress2")]
    internal static partial nuint Compress(nint context, Span<byte> destination, nuint capacity, ReadOnlySpan<byte> source, nuint sourceSize);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_compress")]
    internal static partial nuint Compress(Span<byte> destination, nuint capacity, ReadOnlySpan<byte> source, nuint sourceSize, int level);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_decompress_usingDDict")]
    internal static partial nuint Decompress(nint context, Span<byte> destination, nuint capacity, ReadOnlySpan<byte> source, nuint sourceSize, nint dictionary);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_decompress")]
    internal static partial nuint Decompress(Span<byte> destination, nuint capacity, ReadOnlySpan<byte> source, nuint sourceSize);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_compressStream2")]
    internal static partial nuint CompressStream(nint context, ref ZStandardBuffer output, ref ZStandardBuffer input, int operation);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_decompressStream")]
    internal static partial nuint DecompressStream(nint context, ref ZStandardBuffer output, ref ZStandardBuffer input);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_isError")]
    internal static partial uint IsError(nuint code);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_getErrorName")]
    internal static partial nint GetErrorName(nuint code);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_compressBound")]
    internal static partial nuint GetMaxCompressedSize(nuint size);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_getFrameContentSize")]
    internal static partial ulong GetDecompressedSize(ReadOnlySpan<byte> source, nuint size);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_findFrameCompressedSize")]
    internal static partial nuint GetFrameCompressedSize(ReadOnlySpan<byte> source, nuint size);

    [LibraryImport("libzstd", EntryPoint = "ZSTD_minCLevel")]
    internal static partial int MinLevel();

    [LibraryImport("libzstd", EntryPoint = "ZSTD_maxCLevel")]
    internal static partial int MaxLevel();

    [LibraryImport("libzstd", EntryPoint = "ZDICT_trainFromBuffer")]
    internal static partial nuint TrainDictionary(Span<byte> destination, nuint capacity, ReadOnlySpan<byte> samples, ReadOnlySpan<nuint> sampleSizes, uint sampleCount);

    [LibraryImport("libzstd", EntryPoint = "ZDICT_isError")]
    internal static partial uint DictionaryIsError(nuint code);

    [LibraryImport("libzstd", EntryPoint = "ZDICT_getErrorName")]
    internal static partial nint DictionaryErrorName(nuint code);

    internal static nuint Check(nuint result)
    {
        if (IsError(result) != 0)
            throw new CompressionException($"Zstandard failed: {Marshal.PtrToStringUTF8(GetErrorName(result))}");

        return result;
    }
}
