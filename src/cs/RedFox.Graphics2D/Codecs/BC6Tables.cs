using System;

namespace RedFox.Graphics2D.Codecs
{
    internal static class BC6Tables
    {
        // Mode info and partition/anchor tables for BC6H.
        // Exposed as ReadOnlySpan<byte> and ReadOnlySpan<byte>/ushort arrays to match the original layout.
        internal static ReadOnlySpan<byte> ModeInfo =>
        [
        //   modeBits, subsets, transformed, partBits, rw, gw, bw, rx, gx, bx, ry, gy, by, rz, gz, bz
        /*0*/  2, 2, 1, 5,   10,10,10,  5, 5, 5,  5, 5, 5,  5, 5, 5,
        /*1*/  2, 2, 1, 5,    7, 7, 7,  6, 6, 6,  6, 6, 6,  6, 6, 6,
        /*2*/  5, 2, 1, 5,   11,11,11,  5, 4, 4,  5, 4, 4,  5, 4, 4,
        /*3*/  5, 2, 0, 5,   10,10,10, 10,10,10, 10,10,10, 10,10,10,
        /*4*/  5, 2, 1, 5,   11,11,11,  4, 5, 4,  4, 5, 4,  4, 5, 4,
        /*5*/  5, 2, 1, 5,   11,11,11,  4, 4, 5,  4, 4, 5,  4, 4, 5,
        /*6*/  5, 2, 1, 5,    9, 9, 9,  5, 5, 5,  5, 5, 5,  5, 5, 5,
        /*7*/  5, 2, 1, 5,    8, 8, 8,  6, 6, 6,  6, 6, 6,  6, 6, 6,
        /*8*/  5, 2, 1, 5,    8, 8, 8,  5, 5, 5,  5, 5, 5,  5, 5, 5,
        /*9*/  5, 2, 0, 5,    6, 6, 6,  6, 6, 6,  6, 6, 6,  6, 6, 6,
        /*10*/ 5, 1, 0, 0,   10,10,10, 10,10,10,  0, 0, 0,  0, 0, 0,
        /*11*/ 5, 1, 1, 0,   11,11,11,  9, 9, 9,  0, 0, 0,  0, 0, 0,
        /*12*/ 5, 1, 1, 0,   12,12,12,  8, 8, 8,  0, 0, 0,  0, 0, 0,
        /*13*/ 5, 1, 1, 0,   16,16,16,  4, 4, 4,  0, 0, 0,  0, 0, 0,
        ];

        // BC6H partition table for 2-subsets (32 entries × 16 pixels) - same as BC7 first 32
        internal static ReadOnlySpan<ushort> Partitions2 =>
        [
            0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80,
            0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000,
            0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE,
            0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C,
        ];

        // Anchor index for second subset (reuse from BC7 concept but BC6H has 32 partitions)
        internal static ReadOnlySpan<byte> AnchorTable =>
        [
            15,15,15,15,15,15,15,15,
            15,15,15,15,15,15,15,15,
            15, 2, 8, 2, 2, 8, 8,15,
             2, 8, 2, 2, 8, 8, 2, 2,
        ];

        internal static ReadOnlySpan<byte> Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];
        internal static ReadOnlySpan<byte> Weights4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];
    }
}
