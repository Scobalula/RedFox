using System;
using System.Runtime.CompilerServices;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Buffers;

internal static class DataBufferGpuElementTypes
{
    public static bool TryGet<T>(out GpuBufferElementType elementType, out int sizeBytes) where T : unmanaged
    {
        Type type = typeof(T);
        if (type == typeof(Half))
        {
            elementType = GpuBufferElementType.Float16;
        }
        else if (type == typeof(float))
        {
            elementType = GpuBufferElementType.Float32;
        }
        else if (type == typeof(double))
        {
            elementType = GpuBufferElementType.Float64;
        }
        else if (type == typeof(sbyte))
        {
            elementType = GpuBufferElementType.Int8;
        }
        else if (type == typeof(byte))
        {
            elementType = GpuBufferElementType.UInt8;
        }
        else if (type == typeof(short))
        {
            elementType = GpuBufferElementType.Int16;
        }
        else if (type == typeof(ushort))
        {
            elementType = GpuBufferElementType.UInt16;
        }
        else if (type == typeof(int))
        {
            elementType = GpuBufferElementType.Int32;
        }
        else if (type == typeof(uint))
        {
            elementType = GpuBufferElementType.UInt32;
        }
        else if (type == typeof(long))
        {
            elementType = GpuBufferElementType.Int64;
        }
        else if (type == typeof(ulong))
        {
            elementType = GpuBufferElementType.UInt64;
        }
        else
        {
            elementType = GpuBufferElementType.Unknown;
            sizeBytes = 0;
            return false;
        }

        sizeBytes = Unsafe.SizeOf<T>();
        return true;
    }
}
