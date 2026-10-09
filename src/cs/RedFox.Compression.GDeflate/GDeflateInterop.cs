// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.GDeflate;

internal partial class GDeflateInterop
{
    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_alloc_gdeflate_compressor")]
    internal static partial nint CreateCompressor(int level);

    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_alloc_gdeflate_decompressor")]
    internal static partial nint CreateDecompressor();

    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_free_gdeflate_compressor")]
    internal static partial void FreeCompressor(nint context);

    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_free_gdeflate_decompressor")]
    internal static partial void FreeDecompressor(nint context);

    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_gdeflate_compress_bound")]
    internal static partial nuint CompressBound(nint context, nuint size, out nuint pageCount);

    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_gdeflate_compress")]
    internal static partial nuint Compress(nint context, ReadOnlySpan<byte> source, nuint size, Span<GDeflatePage> pages, nuint pageCount);

    [LibraryImport("libgdeflate", EntryPoint = "libdeflate_gdeflate_decompress")]
    internal static partial int Decompress(nint context, ReadOnlySpan<GDeflatePage> pages, nuint pageCount, Span<byte> destination, nuint capacity, out nuint written);
}
