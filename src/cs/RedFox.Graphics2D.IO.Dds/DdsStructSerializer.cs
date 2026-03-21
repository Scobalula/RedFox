using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    internal static class DdsStructSerializer
    {
        internal static T Read<T>(ReadOnlySpan<byte> data, int offset) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            if (offset < 0 || data.Length < offset + size)
            {
                throw new InvalidDataException($"Data buffer is too small to read {typeof(T).Name} at offset {offset}.");
            }

            return MemoryMarshal.Read<T>(data[offset..]);
        }

        internal static void Write<T>(BinaryWriter writer, T value) where T : unmanaged
        {
            Span<T> valueSpan = stackalloc T[1];
            valueSpan[0] = value;
            writer.Write(MemoryMarshal.AsBytes(valueSpan));
        }
    }
}
