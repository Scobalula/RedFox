// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using System.Runtime.InteropServices;

namespace RedFox.IO
{
    /// <summary>
    /// Provides extension methods for <see cref="BinaryWriter"/>.
    /// </summary>
    public static class BinaryWriterExtensions
    {
        /// <summary>
        /// Writes a null terminated string
        /// </summary>
        /// <param name="bw">Output Stream</param>
        /// <param name="value">Value to write</param>
        public static void WriteNullTerminatedString(this BinaryWriter bw, string value)
        {
            bw.Write(Encoding.UTF8.GetBytes(value));
            bw.Write((byte)0);
        }

        /// <summary>
        /// Writes a native data structure to the current stream.
        /// </summary>
        /// <typeparam name="T">The structure type to read</typeparam>
        /// <param name="writer">The writer that receives the data.</param>
        /// <param name="obj">The unmanaged value to write.</param>
        public static void WriteStruct<T>(this BinaryWriter writer, T obj) where T : unmanaged
        {
            writer.Write(MemoryMarshal.Cast<T, byte>(stackalloc T[1]
            {
                obj
            }));
        }

        /// <summary>
        /// Writes an array of native data structures to the current stream.
        /// </summary>
        /// <typeparam name="T">The structure type to read</typeparam>
        /// <param name="writer">The writer that receives the data.</param>
        /// <param name="obj">The unmanaged values to write.</param>
        public static void WriteStructArray<T>(this BinaryWriter writer, T[] obj) where T : unmanaged
        {
            writer.Write(MemoryMarshal.Cast<T, byte>(obj));
        }
    }
}
