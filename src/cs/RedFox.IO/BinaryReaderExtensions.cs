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
using System.Runtime.CompilerServices;

namespace RedFox.IO;

/// <summary>
/// Provides extension methods for <see cref="BinaryReader"/>.
/// </summary>
public static class BinaryReaderExtensions
{
    /// <summary>
    /// Reads a native data structure from the current stream and advances the current position of the stream by the size of the structure
    /// </summary>
    /// <typeparam name="T">The structure type to read</typeparam>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <returns>A structure of the given type from the current stream</returns>
    public static T ReadStruct<T>(this BinaryReader reader) where T : unmanaged
    {
        Span<byte> buf = stackalloc byte[Unsafe.SizeOf<T>()];
        if (reader.Read(buf) < buf.Length)
            throw new IOException();
        return MemoryMarshal.Cast<byte, T>(buf)[0];
    }

    /// <summary>
    /// Reads a native data structure from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <typeparam name="T">The structure type to read</typeparam>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <returns>A structure of the given type from the current stream</returns>
    public static T ReadStruct<T>(this BinaryReader reader, long position, bool returnBack) where T : unmanaged
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadStruct<T>();
        if (returnBack)
            reader.BaseStream.Position = temp;
        return result;
    }

    public static T ReadStruct<T>(this BinaryReader reader, long position) where T : unmanaged
    {
        return ReadStruct<T>(reader, position, false);
    }

    /// <summary>
    /// Reads a native data structure from the current stream and advances the current position of the stream by the size of the array
    /// </summary>
    /// <typeparam name="T">The structure type to read</typeparam>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="count">The number of items to read. This value must be 0 or a non-negative number or an exception will occur.</param>
    /// <param name="position">Position of the data</param>
    /// <returns>A structure array of the given type from the current stream</returns>
    public static Span<T> ReadStructArray<T>(this BinaryReader reader, int count) where T : unmanaged
    {
        if (count == 0)
            return new Span<T>();
        Span<byte> buf = new byte[count * Unsafe.SizeOf<T>()];
        if (reader.Read(buf) < buf.Length)
            throw new IOException(); 
        return MemoryMarshal.Cast<byte, T>(buf);
    }

    /// <summary>
    /// Reads a native data structure from the current stream and advances the current position of the stream by the size of the array
    /// </summary>
    /// <typeparam name="T">The structure type to read</typeparam>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="count">The number of items to read. This value must be 0 or a non-negative number or an exception will occur.</param>
    /// <param name="position">Position of the data</param>
    /// <returns>A structure array of the given type from the current stream</returns>
    public static IEnumerable<T> EnumerateStructArray<T>(this BinaryReader reader, int count) where T : unmanaged
    {
        for (int i = 0; i < count; i++)
        {
            yield return reader.ReadStruct<T>();
        }
    }

    /// <summary>
    /// Reads a native data structure from the current stream and advances the current position of the stream by the size of the array
    /// </summary>
    /// <typeparam name="T">The structure type to read</typeparam>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="count">The number of items to read. This value must be 0 or a non-negative number or an exception will occur.</param>
    /// <param name="position">Position of the data</param>
    /// <returns>A structure array of the given type from the current stream</returns>
    public static void ReadStructArray<T>(this BinaryReader reader, ref Span<T> input) where T : unmanaged
    {
        if (input.Length == 0)
            return;

        var asBytes = MemoryMarshal.Cast<T, byte>(input);

        if (reader.Read(asBytes) < asBytes.Length)
            throw new IOException();
    }

    /// <summary>
    /// Reads a native data structure from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <typeparam name="T">The structure type to read</typeparam>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="count">The number of items to read. This value must be 0 or a non-negative number or an exception will occur.</param>
    /// <param name="position">Position of the data</param>
    /// <returns>A structure array of the given type from the current stream</returns>
    public static Span<T> ReadStructArray<T>(this BinaryReader reader, int count, long position, bool returnBack) where T : unmanaged
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadStructArray<T>(count);

        if(returnBack)
            reader.BaseStream.Position = temp;

        return result;
    }

    public static Span<T> ReadStructArray<T>(this BinaryReader reader, int count, long position) where T : unmanaged
    {
        return ReadStructArray<T>(reader, count, position, false);
    }

    /// <summary>
    /// Returns the next available byte and does not advance the byte or character position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <returns></returns>
    public static byte PeekByte(this BinaryReader reader)
    {
        long temp = reader.BaseStream.Position;
        var result = reader.ReadByte();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Returns the next available byte at the position and does not advance the byte or character position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <returns></returns>
    public static byte ReadByte(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadByte();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a 2-byte unsigned integer from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="position">Position of the data</param>
    /// <returns>A 2-byte signed integer read from this stream.</returns>
    public static short ReadInt16(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadInt16();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a 2-byte unsigned integer from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="position">Position of the data</param>
    /// <returns>A 2-byte signed integer read from this stream.</returns>
    public static ushort ReadUInt16(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadUInt16();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a 4-byte unsigned integer from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="position">Position of the data</param>
    /// <returns>A 4-byte signed integer read from this stream.</returns>
    public static int ReadInt32(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadInt32();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a 4-byte unsigned integer from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="position">Position of the data</param>
    /// <returns>A 4-byte signed integer read from this stream.</returns>
    public static uint ReadUInt32(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadUInt32();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a 8-byte unsigned integer from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="position">Position of the data</param>
    /// <returns>A 8-byte signed integer read from this stream.</returns>
    public static long ReadInt64(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadInt64();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a 8-byte unsigned integer from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="position">Position of the data</param>
    /// <returns>A 8-byte signed integer read from this stream.</returns>
    public static ulong ReadUInt64(this BinaryReader reader, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadUInt64();
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads the specified number of bytes from the current stream at the given position and returns the stream back to its original position.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="count">The number of bytes to read. This value must be 0 or a non-negative number or an exception will occur.</param>
    /// <param name="position">Position of the data</param>
    /// <returns>A byte array containing data read from the underlying stream. This might be less than the number of bytes requested if the end of the stream is reached.</returns>
    public static byte[] ReadBytes(this BinaryReader reader, int count, long position)
    {
        long temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = reader.ReadBytes(count);
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a UTF-8 string from the reader terminated by a null byte (also known as C string)
    /// </summary>
    /// <param name="reader">Reader</param>
    /// <param name="bufferSize">Initial size of the buffer</param>
    /// <returns>Resulting string</returns>
    public static string ReadUTF8NullTerminatedString(this BinaryReader reader)
    {
        return ReadNullTerminatedString(reader, Encoding.UTF8);
    }

    /// <summary>
    /// Reads a UTF-8 string from the reader terminated by a null byte (also known as C string)
    /// </summary>
    /// <param name="reader">Reader</param>
    /// <param name="position">Position in the reader where the data is located</param>
    /// <returns>Resulting string</returns>
    public static string ReadUTF8NullTerminatedString(this BinaryReader reader, long position)
    {
        if (!reader.BaseStream.CanSeek)
            throw new NotSupportedException("Stream does not support seeking");

        var temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = ReadUTF8NullTerminatedString(reader);
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a UTF-8 string from the reader terminated by a null byte (also known as C string)
    /// </summary>
    /// <param name="reader">Reader</param>
    /// <param name="bufferSize">Initial size of the buffer</param>
    /// <returns>Resulting string</returns>
    public static string ReadUTF16NullTerminatedString(this BinaryReader reader)
    {
        return ReadNullTerminatedString(reader, Encoding.Unicode);
    }

    /// <summary>
    /// Reads a UTF-8 string from the reader terminated by a null byte (also known as C string)
    /// </summary>
    /// <param name="reader">Reader</param>
    /// <param name="position">Position in the reader where the data is located</param>
    /// <returns>Resulting string</returns>
    public static string ReadUTF16NullTerminatedString(this BinaryReader reader, long position)
    {
        if (!reader.BaseStream.CanSeek)
            throw new NotSupportedException("Stream does not support seeking");

        var temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = ReadUTF16NullTerminatedString(reader);
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Reads a UTF-8 string from the reader terminated by a null byte (also known as C string)
    /// </summary>
    /// <param name="reader">Reader</param>
    /// <param name="bufferSize">Initial size of the buffer</param>
    /// <returns>Resulting string</returns>
    public static string ReadUTF32NullTerminatedString(this BinaryReader reader)
    {
        return ReadNullTerminatedString(reader, Encoding.UTF32);
    }

    private static string ReadNullTerminatedString(BinaryReader reader, Encoding encoding)
    {
        int characterWidth = GetCharacterWidth(encoding);

        List<byte> bytes = [];
        Span<byte> characterBuffer = stackalloc byte[4];

        while (true)
        {
            Span<byte> readWindow = characterBuffer[..characterWidth];

            if (reader.Read(readWindow) < readWindow.Length)
                throw new EndOfStreamException("Null-terminated string was not found before the end of the stream.");
            if (IsAllZero(readWindow))
                break;
            for (int i = 0; i < readWindow.Length; i++)
                bytes.Add(readWindow[i]);
        }

        return bytes.Count == 0 ? string.Empty : encoding.GetString([.. bytes]);
    }

    private static int GetCharacterWidth(Encoding encoding)
    {
        if (encoding is UnicodeEncoding)
        {
            return 2;
        }

        if (encoding is UTF32Encoding)
        {
            return 4;
        }

        return 1;
    }

    private static bool IsAllZero(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads a UTF-8 string from the reader terminated by a null byte (also known as C string)
    /// </summary>
    /// <param name="reader">Reader</param>
    /// <param name="position">Position in the reader where the data is located</param>
    /// <returns>Resulting string</returns>
    public static string ReadUTF32NullTerminatedString(this BinaryReader reader, long position)
    {
        if (!reader.BaseStream.CanSeek)
            throw new NotSupportedException("Stream does not support seeking");

        var temp = reader.BaseStream.Position;
        reader.BaseStream.Position = position;
        var result = ReadUTF32NullTerminatedString(reader);
        reader.BaseStream.Position = temp;
        return result;
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, string hexString)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(hexString);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, string hexString, bool firstOccurence)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(hexString, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <param name="startPosition">Position to start searching from</param>
    /// <param name="endPosition">Position to end the search at</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, string hexString, long startPosition, long endPosition)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(hexString, startPosition, endPosition);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <param name="startPosition">Position to start searching from</param>
    /// <param name="endPosition">Position to end the search at</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, string hexString, long startPosition, long endPosition, bool firstOccurence)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(hexString, startPosition, endPosition, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="pattern">Pattern to search for</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, Pattern<byte> pattern)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(pattern);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="pattern">Pattern to search for</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, Pattern<byte> pattern, bool firstOccurence)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(pattern, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="pattern">Pattern to search for</param>
    /// <param name="startPosition">Position to start searching from</param>
    /// <param name="endPosition">Position to end the search at</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, Pattern<byte> pattern, long startPosition, long endPosition)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(pattern, startPosition, endPosition);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="pattern">Pattern to search for</param>
    /// <param name="startPosition">Position to start searching from</param>
    /// <param name="endPosition">Position to end the search at</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, Pattern<byte> pattern, long startPosition, long endPosition, bool firstOccurence)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(pattern, startPosition, endPosition, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="BinaryReader"/> base stream.
    /// </summary>
    /// <param name="reader">Current <see cref="BinaryReader"/></param>
    /// <param name="needle">Byte Array Needle to search for</param>
    /// <param name="mask">Mask array for unknown bytes/pattern matching</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <param name="first">Whether or not to stop at the first result</param>
    /// <param name="bufferSize">The size of the scan buffer.</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this BinaryReader reader, byte[] needle, byte[] mask, long start, long end, bool first, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.BaseStream.Scan(needle, mask, start, end, first, bufferSize);
    }

    /// <summary>
    /// Advances the position of the underlying stream to the next address aligned to the specified boundary.
    /// </summary>
    /// <param name="reader">The binary reader whose underlying stream position will be aligned.</param>
    /// <param name="alignment">The alignment boundary, in bytes. Must be a power of two. If zero, no alignment is performed.</param>
    /// <returns>The new position of the underlying stream after alignment.</returns>
    public static long Align(this BinaryReader reader, long alignment)
    {
        if(alignment != 0)
        {
            alignment -= 1;
            reader.BaseStream.Position = ~alignment & reader.BaseStream.Position + alignment;
        }

        return reader.BaseStream.Position;
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> from the current position of the <see cref="BinaryReader.BaseStream"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="reader">The binary reader whose base stream will be used.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance anchored at the current stream position.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public static StreamPointer<T> AsPointer<T>(this BinaryReader reader) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StreamPointer<T>(reader.BaseStream);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> from the specified position in the <see cref="BinaryReader.BaseStream"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="reader">The binary reader whose base stream will be used.</param>
    /// <param name="pointer">The offset in the stream where the data begins.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance anchored at the specified position.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public static StreamPointer<T> AsPointer<T>(this BinaryReader reader, long pointer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StreamPointer<T>(reader.BaseStream, pointer);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> for a contiguous array at the specified position in the <see cref="BinaryReader.BaseStream"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="reader">The binary reader whose base stream will be used.</param>
    /// <param name="pointer">The offset in the stream where the array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance for the specified array range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static StreamPointer<T> AsPointer<T>(this BinaryReader reader, long pointer, int count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StreamPointer<T>(reader.BaseStream, pointer, count);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> with pointer-chase mode control from the <see cref="BinaryReader.BaseStream"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="reader">The binary reader whose base stream will be used.</param>
    /// <param name="pointer">The offset in the stream where the data or pointer array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <param name="isPointerArray">
    /// If <c>true</c>, the index accesses a pointer table where each index points to the actual data;
    /// if <c>false</c>, accesses are direct contiguous reads.
    /// </param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance with the specified configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static StreamPointer<T> AsPointer<T>(this BinaryReader reader, long pointer, int count, bool isPointerArray) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StreamPointer<T>(reader.BaseStream, pointer, count, isPointerArray);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> for a contiguous array at the specified position in the <see cref="BinaryReader.BaseStream"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="reader">The binary reader whose base stream will be used.</param>
    /// <param name="pointer">The offset in the stream where the array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance for the specified array range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static StreamPointer<T> CreatePointer<T>(this BinaryReader reader, long pointer, int count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StreamPointer<T>(reader.BaseStream, pointer, count);
    }
}
