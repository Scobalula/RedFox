using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Provides low-level blittable struct read/write operations for DDS header serialization.
    /// </summary>
    public static class DdsStructSerializer
    {
        /// <summary>
        /// Reads an unmanaged struct from a byte span at the given offset.
        /// </summary>
        /// <typeparam name="T">The unmanaged struct type to read.</typeparam>
        /// <param name="data">The source byte data.</param>
        /// <param name="offset">The byte offset to begin reading.</param>
        /// <returns>The deserialized struct value.</returns>
        public static T Read<T>(ReadOnlySpan<byte> data, int offset) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            if (offset < 0 || data.Length < offset + size)
            {
                throw new InvalidDataException($"Data buffer is too small to read {typeof(T).Name} at offset {offset}.");
            }

            return MemoryMarshal.Read<T>(data[offset..]);
        }

        /// <summary>
        /// Writes an unmanaged struct to a binary writer as raw bytes.
        /// </summary>
        /// <typeparam name="T">The unmanaged struct type to write.</typeparam>
        /// <param name="writer">The binary writer to write to.</param>
        /// <param name="value">The struct value to serialize.</param>
        public static void Write<T>(BinaryWriter writer, T value) where T : unmanaged
        {
            Span<T> valueSpan = stackalloc T[1];
            valueSpan[0] = value;
            writer.Write(MemoryMarshal.AsBytes(valueSpan));
        }
    }
}
