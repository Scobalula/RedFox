using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Buffers;

/// <summary>
/// Provides helpers for packing strided source bytes into canonical contiguous <see cref="DataBuffer{T}"/> instances.
/// </summary>
public static class DataBufferPacking
{
    /// <summary>
    /// Creates a contiguous <see cref="DataBuffer{T}"/> by copying values from a strided byte source.
    /// </summary>
    /// <typeparam name="T">The unmanaged numeric component type.</typeparam>
    /// <param name="source">The source bytes containing the strided data.</param>
    /// <param name="elementCount">The number of logical elements in the source.</param>
    /// <param name="byteOffset">The byte offset of the first element in <paramref name="source"/>.</param>
    /// <param name="byteStride">The byte distance between consecutive elements in <paramref name="source"/>.</param>
    /// <param name="valueCount">The number of values stored per element.</param>
    /// <param name="componentCount">The number of components stored per value.</param>
    /// <param name="byteValueStride">The byte distance between consecutive values within a single element.</param>
    /// <returns>A contiguous data buffer containing the packed values in value-major order.</returns>
    public static DataBuffer<T> CreateStrided<T>(byte[] source, int elementCount, int byteOffset, int byteStride, int valueCount, int componentCount, int byteValueStride)
        where T : unmanaged, INumber<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(elementCount);
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteStride);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valueCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(componentCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteValueStride);

        int totalComponentCount = checked(elementCount * valueCount * componentCount);
        T[] items = new T[totalComponentCount];
        int componentSizeBytes = Unsafe.SizeOf<T>();
        ReadOnlySpan<byte> sourceBytes = source;
        int destinationIndex = 0;

        for (int elementIndex = 0; elementIndex < elementCount; elementIndex++)
        {
            int elementOffset = checked(byteOffset + (elementIndex * byteStride));
            for (int valueIndex = 0; valueIndex < valueCount; valueIndex++)
            {
                int valueOffset = checked(elementOffset + (valueIndex * byteValueStride));
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    int componentOffset = checked(valueOffset + (componentIndex * componentSizeBytes));
                    items[destinationIndex++] = MemoryMarshal.Read<T>(sourceBytes[componentOffset..]);
                }
            }
        }

        return new DataBuffer<T>(items, valueCount, componentCount);
    }
}