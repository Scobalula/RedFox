// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RedFox.IO
{
    /// <summary>
    /// Provides a simple method to read continous data from a <see cref="Span{T}"/> byte buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    public ref struct SpanReader(ReadOnlySpan<byte> buffer)
    {
        private readonly ReadOnlySpan<byte> _buffer = buffer;

        private int _position = 0;

        /// <summary>
        /// Gets or sets the current position within the buffer.
        /// </summary>
        public int Position { readonly get { return _position; } set { Seek(value, SeekOrigin.Begin); } }

        /// <summary>
        /// Gets the total length of the underlying buffer in bytes.
        /// </summary>
        public readonly int Length => _buffer.Length;

        /// <summary>
        /// Gets the number of bytes remaining from the current position to the end of the buffer.
        /// </summary>
        public readonly int Remaining => _buffer.Length - _position;

        /// <summary>
        /// 
        /// </summary>
        public readonly ReadOnlySpan<byte> Span => _buffer;

        /// <summary>
        /// Reads a read-only byte slice of the given size from the current position and advances
        /// the position by <paramref name="size"/> bytes.
        /// </summary>
        /// <param name="size">The number of bytes to read.</param>
        /// <returns>A read-only byte span over the requested region of the buffer.</returns>
        public ReadOnlySpan<byte> Read(int size)
        {
            var result = _buffer.Slice(_position, size);
            _position += size;
            return result;
        }

        /// <summary>
        /// Reads a read-only byte slice of the given size from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <param name="size">The number of bytes to read.</param>
        /// <returns>A read-only byte span over the requested region of the buffer.</returns>
        public readonly ReadOnlySpan<byte> Read(int position, int size) => _buffer.Slice(position, size);

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the current position and advances
        /// the position by <c>sizeof(<typeparamref name="T"/>)</c> bytes.
        /// </summary>
        /// <typeparam name="T">An unmanaged, blittable value type to read.</typeparam>
        /// <returns>The value read from the buffer.</returns>
        public T Read<T>() where T : unmanaged
        {
            var result = MemoryMarshal.Read<T>(_buffer[_position..]);
            _position += Unsafe.SizeOf<T>();
            return result;
        }

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <typeparam name="T">An unmanaged, blittable value type to read.</typeparam>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The value read from the buffer.</returns>
        public readonly T Read<T>(int position) where T : unmanaged => MemoryMarshal.Read<T>(_buffer[position..]);

        /// <summary>
        /// Reads a span of <paramref name="count"/> values of type <typeparamref name="T"/> from the
        /// specified position. This does not modify the current position.
        /// </summary>
        /// <typeparam name="T">An unmanaged, blittable value type to read.</typeparam>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <param name="count">The number of elements to read.</param>
        /// <returns>A read-only span of <typeparamref name="T"/> values reinterpreted from the buffer.</returns>
        public readonly ReadOnlySpan<T> ReadArray<T>(int count, int position) where T : unmanaged =>
            MemoryMarshal.Cast<byte, T>(Read(position, Unsafe.SizeOf<T>() * count));

        /// <summary>
        /// Reads a 64-bit signed integer from the current position and advances the position by 8 bytes.
        /// </summary>
        /// <returns>The <see cref="long"/> read from the buffer.</returns>
        public long ReadInt64() => Read<long>();

        /// <summary>
        /// Reads a 64-bit unsigned integer from the current position and advances the position by 8 bytes.
        /// </summary>
        /// <returns>The <see cref="ulong"/> read from the buffer.</returns>
        public ulong ReadUInt64() => Read<ulong>();

        /// <summary>
        /// Reads a 32-bit signed integer from the current position and advances the position by 4 bytes.
        /// </summary>
        /// <returns>The <see cref="int"/> read from the buffer.</returns>
        public int ReadInt32() => Read<int>();

        /// <summary>
        /// Reads a 32-bit unsigned integer from the current position and advances the position by 4 bytes.
        /// </summary>
        /// <returns>The <see cref="uint"/> read from the buffer.</returns>
        public uint ReadUInt32() => Read<uint>();

        /// <summary>
        /// Reads a 16-bit signed integer from the current position and advances the position by 2 bytes.
        /// </summary>
        /// <returns>The <see cref="short"/> read from the buffer.</returns>
        public short ReadInt16() => Read<short>();

        /// <summary>
        /// Reads a 16-bit unsigned integer from the current position and advances the position by 2 bytes.
        /// </summary>
        /// <returns>The <see cref="ushort"/> read from the buffer.</returns>
        public ushort ReadUInt16() => Read<ushort>();

        /// <summary>
        /// Reads an 8-bit unsigned integer from the current position and advances the position by 1 byte.
        /// </summary>
        /// <returns>The <see cref="byte"/> read from the buffer.</returns>
        public byte ReadByte() => Read<byte>();

        /// <summary>
        /// Reads an 8-bit signed integer from the current position and advances the position by 1 byte.
        /// </summary>
        /// <returns>The <see cref="sbyte"/> read from the buffer.</returns>
        public sbyte ReadSByte() => Read<sbyte>();

        /// <summary>
        /// Reads a 32-bit IEEE 754 floating-point value from the current position and advances the position by 4 bytes.
        /// </summary>
        /// <returns>The <see cref="float"/> read from the buffer.</returns>
        public float ReadSingle() => Read<float>();

        /// <summary>
        /// Reads a 64-bit IEEE 754 floating-point value from the current position and advances the position by 8 bytes.
        /// </summary>
        /// <returns>The <see cref="double"/> read from the buffer.</returns>
        public double ReadDouble() => Read<double>();

        /// <summary>
        /// Reads a Boolean value from the current position and advances the position by 1 byte.
        /// A non-zero byte is interpreted as <see langword="true"/>.
        /// </summary>
        /// <returns>The <see cref="bool"/> read from the buffer.</returns>
        public bool ReadBoolean() => Read<byte>() != 0;

        /// <summary>
        /// Reads a UTF-16 character from the current position and advances the position by 2 bytes.
        /// </summary>
        /// <returns>The <see cref="char"/> read from the buffer.</returns>
        public char ReadChar() => Read<char>();

        /// <summary>
        /// Reads a <see cref="decimal"/> value from the current position and advances the position by 16 bytes.
        /// The value is stored as four consecutive 32-bit signed integers (lo, mid, hi, flags).
        /// </summary>
        /// <returns>The <see cref="decimal"/> read from the buffer.</returns>
        public decimal ReadDecimal()
        {
            int lo = ReadInt32();
            int mid = ReadInt32();
            int hi = ReadInt32();
            int flags = ReadInt32();
            return new decimal(lo, mid, hi, (flags & unchecked((int)0x80000000)) != 0, (byte)((flags & 0x00FF0000) >> 16));
        }

        /// <summary>
        /// Reads a 64-bit signed integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="long"/> read from the buffer.</returns>
        public readonly long ReadInt64(int position) => Read<long>(position);

        /// <summary>
        /// Reads a 64-bit unsigned integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="ulong"/> read from the buffer.</returns>
        public readonly ulong ReadUInt64(int position) => Read<ulong>(position);

        /// <summary>
        /// Reads a 32-bit signed integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="int"/> read from the buffer.</returns>
        public readonly int ReadInt32(int position) => Read<int>(position);

        /// <summary>
        /// Reads a 32-bit unsigned integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="uint"/> read from the buffer.</returns>
        public readonly uint ReadUInt32(int position) => Read<uint>(position);

        /// <summary>
        /// Reads a 16-bit signed integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="short"/> read from the buffer.</returns>
        public readonly short ReadInt16(int position) => Read<short>(position);

        /// <summary>
        /// Reads a 16-bit unsigned integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="ushort"/> read from the buffer.</returns>
        public readonly ushort ReadUInt16(int position) => Read<ushort>(position);

        /// <summary>
        /// Reads an 8-bit unsigned integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="byte"/> read from the buffer.</returns>
        public readonly byte ReadByte(int position) => Read<byte>(position);

        /// <summary>
        /// Reads an 8-bit signed integer from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="sbyte"/> read from the buffer.</returns>
        public readonly sbyte ReadSByte(int position) => Read<sbyte>(position);

        /// <summary>
        /// Reads a 32-bit IEEE 754 floating-point value from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="float"/> read from the buffer.</returns>
        public readonly float ReadSingle(int position) => Read<float>(position);

        /// <summary>
        /// Reads a 64-bit IEEE 754 floating-point value from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="double"/> read from the buffer.</returns>
        public readonly double ReadDouble(int position) => Read<double>(position);

        /// <summary>
        /// Reads a Boolean value from the specified position.
        /// A non-zero byte is interpreted as <see langword="true"/>.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="bool"/> read from the buffer.</returns>
        public readonly bool ReadBoolean(int position) => Read<byte>(position) != 0;

        /// <summary>
        /// Reads a UTF-16 character from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="char"/> read from the buffer.</returns>
        public readonly char ReadChar(int position) => Read<char>(position);

        /// <summary>
        /// Reads a <see cref="decimal"/> value from the specified position.
        /// The value is stored as four consecutive 32-bit signed integers (lo, mid, hi, flags).
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <returns>The <see cref="decimal"/> read from the buffer.</returns>
        public readonly decimal ReadDecimal(int position)
        {
            int lo = ReadInt32(position);
            int mid = ReadInt32(position + 4);
            int hi = ReadInt32(position + 8);
            int flags = ReadInt32(position + 12);
            return new decimal(lo, mid, hi, (flags & unchecked((int)0x80000000)) != 0, (byte)((flags & 0x00FF0000) >> 16));
        }

        /// <summary>
        /// Reads a UTF-8 encoded string of the given byte length from the current position
        /// and advances the position by <paramref name="size"/> bytes.
        /// </summary>
        /// <param name="size">The number of bytes to read and decode as a string.</param>
        /// <returns>The decoded <see cref="string"/>.</returns>
        public string ReadString(int size) => ReadString(size, Encoding.UTF8);

        /// <summary>
        /// Reads an encoded string of the given byte length from the current position using
        /// the specified <paramref name="encoding"/>, and advances the position by <paramref name="size"/> bytes.
        /// </summary>
        /// <param name="size">The number of bytes to read and decode as a string.</param>
        /// <param name="encoding">
        /// The <see cref="Encoding"/> used to decode the bytes into a string.
        /// Common choices include <see cref="Encoding.UTF8"/>, <see cref="Encoding.Unicode"/> (UTF-16 LE),
        /// <see cref="Encoding.BigEndianUnicode"/> (UTF-16 BE), and <see cref="Encoding.Latin1"/>.
        /// </param>
        /// <returns>The decoded <see cref="string"/>.</returns>
        public string ReadString(int size, Encoding encoding)
        {
            var result = encoding.GetString(_buffer.Slice(_position, size));
            _position += size;
            return result;
        }

        /// <summary>
        /// Reads a UTF-8 encoded string of the given byte length from the specified position.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <param name="size">The number of bytes to read and decode as a string.</param>
        /// <returns>The decoded <see cref="string"/>.</returns>
        public readonly string ReadString(int position, int size) => ReadString(position, size, Encoding.UTF8);

        /// <summary>
        /// Reads an encoded string of the given byte length from the specified position using
        /// the specified <paramref name="encoding"/>.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to read from.</param>
        /// <param name="size">The number of bytes to read and decode as a string.</param>
        /// <param name="encoding">
        /// The <see cref="Encoding"/> used to decode the bytes into a string.
        /// Common choices include <see cref="Encoding.UTF8"/>, <see cref="Encoding.Unicode"/> (UTF-16 LE),
        /// <see cref="Encoding.BigEndianUnicode"/> (UTF-16 BE), and <see cref="Encoding.Latin1"/>.
        /// </param>
        /// <returns>The decoded <see cref="string"/>.</returns>
        public readonly string ReadString(int position, int size, Encoding encoding) =>
            encoding.GetString(_buffer.Slice(position, size));

        /// <summary>
        /// Reads a null-terminated string from the current position using the specified encoding,
        /// and advances the position past the null terminator.
        /// </summary>
        /// <param name="encoding">
        /// The <see cref="Encoding"/> used to decode the bytes and to determine the null-terminator
        /// width (e.g. 1 byte for UTF-8/Latin-1, 2 bytes for UTF-16, 4 bytes for UTF-32).
        /// </param>
        /// <returns>The decoded <see cref="string"/>, not including the null terminator.</returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when a null terminator is not found before the end of the buffer.
        /// </exception>
        public string ReadNullTerminatedString(Encoding encoding)
        {
            int terminatorSize = encoding.GetByteCount("\0");

            for (int end = _position; end <= _buffer.Length - terminatorSize; end += terminatorSize)
            {
                bool isTerminator = true;

                for (int i = 0; i < terminatorSize; i++)
                {
                    if (_buffer[end + i] != 0)
                    {
                        isTerminator = false;
                        break;
                    }
                }

                if (isTerminator)
                {
                    string value = encoding.GetString(_buffer[_position..end]);
                    _position = end + terminatorSize;
                    return value;
                }
            }

            throw new EndOfStreamException("Null terminator was not found.");
        }

        /// <summary>
        /// Reads a null-terminated string from the specified position using the specified encoding.
        /// This does not modify the current position.
        /// </summary>
        /// <param name="position">The absolute byte offset within the buffer to begin reading from.</param>
        /// <param name="encoding">
        /// The <see cref="Encoding"/> used to decode the bytes and to determine the null-terminator
        /// width (e.g. 1 byte for UTF-8/Latin-1, 2 bytes for UTF-16, 4 bytes for UTF-32).
        /// </param>
        /// <returns>The decoded <see cref="string"/>, not including the null terminator.</returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when a null terminator is not found before the end of the buffer.
        /// </exception>
        public readonly string ReadNullTerminatedString(Encoding encoding, int position)
        {
            int terminatorSize = encoding.GetByteCount("\0");

            for (int end = position; end <= _buffer.Length - terminatorSize; end += terminatorSize)
            {
                bool isTerminator = true;

                for (int i = 0; i < terminatorSize; i++)
                {
                    if (_buffer[end + i] != 0)
                    {
                        isTerminator = false;
                        break;
                    }
                }

                if (isTerminator)
                {
                    return encoding.GetString(_buffer[position..end]);
                }
            }

            throw new EndOfStreamException("Null terminator was not found.");
        }

        /// <summary>
        /// Seeks to the provided offset within the buffer for the given seek origin.
        /// </summary>
        /// <param name="offset">The byte offset to seek to, relative to <paramref name="origin"/>.</param>
        /// <param name="origin">The reference point used to interpret <paramref name="offset"/>.</param>
        /// <returns>The new absolute position within the buffer after seeking.</returns>
        /// <exception cref="NotImplementedException">
        /// Thrown when an unrecognised <see cref="SeekOrigin"/> value is supplied.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the resulting position would be beyond the end of the buffer.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the resulting position would be before the start of the buffer.
        /// </exception>
        public long Seek(int offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = _buffer.Length - offset;
                    break;
                default:
                    throw new NotImplementedException(origin.ToString());
            }

            if (_position > _buffer.Length)
                throw new EndOfStreamException("Attempted to seek past the end of the buffer.");
            if (_position < 0)
                throw new IOException("Attempted to seek before the start of the buffer.");

            return _position;
        }

        public object Slice(object animationTableBuffer, int v)
        {
            throw new NotImplementedException();
        }
    }
}
