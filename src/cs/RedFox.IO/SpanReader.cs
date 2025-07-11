// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Drawing;
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
        /// Gets or Sets the current position within the buffer.
        /// </summary>
        public int Position { readonly get { return _position; } set { Seek(value, SeekOrigin.Begin); } }

        /// <summary>
        /// Reads a read-only byte buffer from the buffer of the given size and advances the position by the size of the data.
        /// </summary>
        /// <param name="size">The size of the byte buffer to read.</param>
        /// <returns>Resulting read-only byte buffer.</returns>
        public ReadOnlySpan<byte> Read(int size)
        {
            var result = _buffer.Slice(_position, size);
            _position += size;
            return result;
        }

        /// <summary>
        /// Reads a read-only byte buffer from the buffer of the given size. This does not modify the current position.
        /// </summary>
        /// <param name="position">The size of the byte buffer to read.</param>
        /// <param name="size">The size of the byte buffer to read.</param>
        /// <returns>Resulting read-only byte buffer.</returns>
        public readonly ReadOnlySpan<byte> Read(int position, int size) => _buffer.Slice(position, size);

        /// <summary>
        /// Reads the value of the given type from the buffer and advances the position by the size of the element.
        /// </summary>
        /// <typeparam name="T">The type to read from the buffer, this must be an unmanaged plain data type.</typeparam>
        /// <returns>Resulting type from the buffer.</returns>
        public T Read<T>() where T : unmanaged
        {
            var result = MemoryMarshal.Read<T>(_buffer[_position..]);
            _position += Unsafe.SizeOf<T>();
            return result;
        }

        /// <summary>
        /// Reads the value of the given type from the buffer at the given position. This does not modify the current position.
        /// </summary>
        /// <typeparam name="T">The type to read from the buffer, this must be an unmanaged plain data type.</typeparam>
        /// <param name="position">The absolute position of the elements within the buffer.</param>
        /// <param name="count">The number of elements to read.</param>
        /// <returns>Resulting type from the buffer.</returns>
        public readonly ReadOnlySpan<T> Read<T>(int position, int count) where T : unmanaged => MemoryMarshal.Cast<byte, T>(Read(position, Unsafe.SizeOf<T>() * count));

        /// <summary>
        /// Reads a UTF-8 encoded string from the buffer of the size and advances the position by the size of the data.
        /// </summary>
        /// <param name="size">The size of the string to read.</param>
        /// <returns>Resulting string.</returns>
        public string ReadString(int size)
        {
            var result = Encoding.UTF8.GetString(_buffer.Slice(_position, size));
            _position += size;
            return result;
        }

        /// <summary>
        /// Reads a UTF-8 encoded string from the buffer of the size at the given position. This does not modify the current position.
        /// </summary>
        /// <param name="position">The size of the string to read.</param>
        /// <param name="size">The size of the string to read.</param>
        /// <returns>Resulting string.</returns>
        public readonly string ReadString(int position, int size) => Encoding.UTF8.GetString(_buffer.Slice(position, size));

        /// <summary>
        /// Seeks to the provided offset within the buffer for the given seek origin.
        /// </summary>
        /// <param name="offset">The offset to seek to.</param>
        /// <param name="origin">The origin to seek from.</param>
        /// <returns>New buffer position.</returns>
        /// <exception cref="NotImplementedException">Thrown if an attempt to seek to an umimplemented seek origin is performed.</exception>
        /// <exception cref="EndOfStreamException">Thrown if an attempt to seek past the end of the buffer is performed.</exception>
        /// <exception cref="IOException">Thrown if an attempt to seek to before the start of the buffer is performed.</exception>
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
    }
}
