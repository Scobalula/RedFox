// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.Deflate;

internal unsafe partial class DeflateInterop
{
    [LibraryImport("zlib1", EntryPoint = "zlibVersion")]
    internal static partial nint Version();

    [LibraryImport("zlib1", EntryPoint = "deflateInit2_")]
    internal static partial int DeflateInit(ZLibState* state, int level, int method, int windowBits, int memoryLevel, int strategy, nint version, int streamSize);

    [LibraryImport("zlib1", EntryPoint = "inflateInit2_")]
    internal static partial int InflateInit(ZLibState* state, int windowBits, nint version, int streamSize);

    [LibraryImport("zlib1", EntryPoint = "deflate")]
    internal static partial int Deflate(ZLibState* state, int flush);

    [LibraryImport("zlib1", EntryPoint = "deflateEnd")]
    internal static partial int DeflateEnd(ZLibState* state);

    [LibraryImport("zlib1", EntryPoint = "deflateReset")]
    internal static partial int DeflateReset(ZLibState* state);

    [LibraryImport("zlib1", EntryPoint = "deflateSetDictionary")]
    internal static partial int DeflateSetDictionary(ZLibState* state, byte* dictionary, uint dictionaryLength);

    [LibraryImport("zlib1", EntryPoint = "inflate")]
    internal static partial int Inflate(ZLibState* state, int flush);

    [LibraryImport("zlib1", EntryPoint = "inflateEnd")]
    internal static partial int InflateEnd(ZLibState* state);

    [LibraryImport("zlib1", EntryPoint = "inflateReset")]
    internal static partial int InflateReset(ZLibState* state);

    [LibraryImport("zlib1", EntryPoint = "inflateSetDictionary")]
    internal static partial int InflateSetDictionary(ZLibState* state, byte* dictionary, uint dictionaryLength);

    [LibraryImport("zlib1", EntryPoint = "compressBound")]
    internal static partial uint CompressBound(uint sourceSize);
}
