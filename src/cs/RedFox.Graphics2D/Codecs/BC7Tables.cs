using System;

namespace RedFox.Graphics2D.Codecs
{
    internal static class BC7Tables
    {
        // Mode parameters: NumSubsets, PartitionBits, RotationBits, IndexSelectionBits,
        //                   ColorBits, AlphaBits, EndpointPBits, SharedPBits,
        //                   IndexBits, SecondaryIndexBits
        internal static readonly ModeInfo[] Modes =
        [
            new(3, 4, 0, 0, 4, 0, 1, 0, 3, 0), // Mode 0
            new(2, 6, 0, 0, 6, 0, 0, 1, 3, 0), // Mode 1
            new(3, 6, 0, 0, 5, 0, 0, 0, 2, 0), // Mode 2
            new(2, 6, 0, 0, 7, 0, 1, 0, 2, 0), // Mode 3
            new(1, 0, 2, 1, 5, 6, 0, 0, 2, 3), // Mode 4
            new(1, 0, 2, 0, 7, 8, 0, 0, 2, 2), // Mode 5
            new(1, 0, 0, 0, 7, 7, 1, 0, 4, 0), // Mode 6
            new(2, 6, 0, 0, 5, 5, 1, 0, 2, 0), // Mode 7
        ];

        internal readonly record struct ModeInfo(int NumSubsets, int PartitionBits, int RotationBits, int IndexSelectionBits, int ColorBits, int AlphaBits, int EndpointPBits, int SharedPBits, int IndexBits, int SecondaryIndexBits);

        // 2-subset partition table: bit i = subset index for pixel i.
        internal static ReadOnlySpan<ushort> Partitions2 =>
        [
            0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80,
            0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000,
            0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE,
            0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C,
            0xAAAA, 0xF0F0, 0x5A5A, 0x33CC, 0x3C3C, 0x55AA, 0x9696, 0xA55A,
            0x73CE, 0x13C8, 0x324C, 0x3BDC, 0x6996, 0xC33C, 0x9966, 0x0660,
            0x0272, 0x04E4, 0x4E40, 0x2720, 0xC936, 0x936C, 0x39C6, 0x639C,
            0x9336, 0x9CC6, 0x817E, 0xE718, 0xCCF0, 0x0FCC, 0x7744, 0xEE22,
        ];

        // 3-subset partition table: 2 bits per pixel, packed LSB = pixel 0.
        internal static ReadOnlySpan<uint> Partitions3 =>
        [
            0xAA685050, 0x6A5A5040, 0x5A5A4200, 0x5450A0A8,
            0xA5A50000, 0xA0A05050, 0x5555A0A0, 0x5A5A5050,
            0xAA550000, 0xAA555500, 0xAAAA5500, 0x90909090,
            0x94949494, 0xA4A4A4A4, 0xA9A59450, 0x2A0A4250,
            0xA5945040, 0x0A425054, 0xA5A5A500, 0x55A0A0A0,
            0xA8A85454, 0x6A6A4040, 0xA4A45000, 0x1A1A0500,
            0x0050A4A4, 0xAAA59090, 0x14696914, 0x69691400,
            0xA08585A0, 0xAA821414, 0x50A4A450, 0x6A5A0200,
            0xA9A58000, 0x5090A0A8, 0xA8A09050, 0x24242424,
            0x00AA5500, 0x24924924, 0x24499224, 0x50A50A50,
            0x500AA550, 0xAAAA4444, 0x66660000, 0xA5A0A5A0,
            0x50A050A0, 0x69286928, 0x44AAAA44, 0x66666600,
            0xAA444444, 0x54A854A8, 0x95809580, 0x96969600,
            0xA85454A8, 0x80959580, 0xAA141414, 0x96960000,
            0xAAAA1414, 0xA05050A0, 0xA0A0A0A0, 0x96000000,
            0x40804080, 0xA9A8A9A8, 0xAAAAAA44, 0x2A4A5254,
        ];

        // Anchor index for subset 1 in 2-subset partitions.
        internal static ReadOnlySpan<byte> AnchorTable2 =>
        [
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15,  2,  8,  2,  2,  8,  8, 15,
             2,  8,  2,  2,  8,  8,  2,  2,
            15, 15,  6,  8,  2,  8, 15, 15,
             2,  8,  2,  2,  2, 15, 15,  6,
             6,  2,  6,  8, 15, 15,  2,  2,
            15, 15, 15, 15, 15,  2,  2, 15,
        ];

        // Anchor index for subset 1 in 3-subset partitions.
        internal static ReadOnlySpan<byte> AnchorTable3a =>
        [
             3,  3, 15, 15,  8,  3, 15, 15,
             8,  8,  6,  6,  6,  5,  3,  3,
             3,  3,  8, 15,  3,  3,  6, 10,
             5,  8,  8,  6,  8,  5, 15, 15,
             8, 15,  3,  5,  6, 10,  8, 15,
            15,  3, 15,  5, 15, 15, 15, 15,
             3, 15,  5,  5,  5,  8,  5, 10,
             5, 10,  8, 13, 15, 12,  3,  3,
        ];

        // Anchor index for subset 2 in 3-subset partitions.
        internal static ReadOnlySpan<byte> AnchorTable3b =>
        [
            15,  8,  8,  3, 15, 15,  3,  8,
            15, 15, 15, 15, 15, 15, 15,  8,
            15,  8, 15,  3, 15,  8, 15,  8,
             3, 15,  6, 10, 15, 15, 10,  8,
            15,  3, 15, 10, 10,  8,  9, 10,
             6, 15,  8, 15,  3,  6,  6,  8,
            15,  3, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15,  3, 15, 15,  8,
        ];

        // Interpolation weight tables indexed by decoded index value.
        internal static ReadOnlySpan<byte> Weights2 => [0, 21, 43, 64];
        internal static ReadOnlySpan<byte> Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];
        internal static ReadOnlySpan<byte> Weights4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];
    }
}
