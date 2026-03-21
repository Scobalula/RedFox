using System;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.Codecs
{
    internal static class BC7Math
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSubset(int numSubsets, int partition, int pixelIndex)
        {
            if (numSubsets == 1) return 0;
            if (numSubsets == 2) return (BC7Tables.Partitions2[partition] >> pixelIndex) & 1;
            return (int)(BC7Tables.Partitions3[partition] >> (pixelIndex * 2)) & 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAnchorIndex(int numSubsets, int partition, int pixelIndex)
        {
            if (pixelIndex == 0) return true;
            if (numSubsets == 1) return false;
            if (numSubsets == 2) return pixelIndex == BC7Tables.AnchorTable2[partition];
            return pixelIndex == BC7Tables.AnchorTable3a[partition] || pixelIndex == BC7Tables.AnchorTable3b[partition];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Unquantize(int value, int precision)
        {
            if (precision >= 8) return value;
            return (value << (8 - precision)) | (value >> (2 * precision - 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Interpolate(int e0, int e1, int weight)
        {
            return ((64 - weight) * e0 + weight * e1 + 32) >> 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> GetWeights(int indexBits) => indexBits switch
        {
            2 => BC7Tables.Weights2,
            3 => BC7Tables.Weights3,
            4 => BC7Tables.Weights4,
            _ => throw new InvalidOperationException($"Unsupported index bit count: {indexBits}"),
        };

        internal ref struct BitReader
        {
            private readonly ReadOnlySpan<byte> _data;
            private int _position;

            public BitReader(ReadOnlySpan<byte> data) { _data = data; _position = 0; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Read(int numBits)
            {
                uint result = 0;
                for (int i = 0; i < numBits; i++)
                {
                    int byteIndex = _position >> 3;
                    int bitIndex = _position & 7;
                    result |= (uint)((_data[byteIndex] >> bitIndex) & 1) << i;
                    _position++;
                }
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Skip(int numBits) => _position += numBits;
        }
    }
}
