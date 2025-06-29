// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.Compression.Deflate
{
    internal unsafe partial class DeflateInterop
    {
        internal ref struct MZStream
        {
            public byte* NextIn; /* pointer to next byte to read */
            public uint AvailIn;        /* number of bytes available at next_in */
            public uint TotalIn;            /* total number of bytes consumed so far */

            public byte* NextOut; /* pointer to next byte to write */
            public uint AvailOut;  /* number of bytes that can be written to next_out */
            public uint TotalOut;      /* total number of bytes produced so far */

            public IntPtr Msg;                       /* error msg (unused) */
            public IntPtr State; /* internal state, allocated by zalloc/zfree */

            public IntPtr ZAlloc; /* optional heap allocation function (defaults to malloc) */
            public IntPtr ZFree;   /* optional heap free function (defaults to free) */
            public IntPtr Opaque;         /* heap alloc function user pointer */

            public int DataType;     /* data_type (unused) */
            public uint Adler;    /* adler32 of the source or uncompressed data */
            public uint Reserved; /* not used */
        }

        public const int MZDeflated = 8;

        public const int MZDefaultWindowBits = 15;

        public const int MZDefaultStrategy = 0;

        private const string Library = "Native\\miniz";

        [LibraryImport(Library, EntryPoint = "mz_deflateInit2", SetLastError = true)]
        public static partial int DeflateInit(ref MZStream pStream, DeflateLevel level, int method, int window_bits, int mem_level, int strategy);

        [LibraryImport(Library, EntryPoint = "mz_deflate", SetLastError = true)]
        public static partial int Deflate(ref MZStream pStream, int flush);

        [LibraryImport(Library, EntryPoint = "mz_deflateEnd", SetLastError = true)]
        public static partial int DeflateEnd(ref MZStream pStream);

        [LibraryImport(Library, EntryPoint = "mz_inflateInit2", SetLastError = true)]
        public static partial int InflateInit(ref MZStream pStream, int windowBits);

        [LibraryImport(Library, EntryPoint = "mz_inflate", SetLastError = true)]
        public static partial int Inflate(ref MZStream pStream, int flush);

        [LibraryImport(Library, EntryPoint = "mz_inflateEnd", SetLastError = true)]
        public static partial int InflateEnd(ref MZStream pStream);

        [LibraryImport(Library, EntryPoint = "mz_compressBound", SetLastError = true)]
        public static partial int CompressBound(int sourceLen);
    }
}
