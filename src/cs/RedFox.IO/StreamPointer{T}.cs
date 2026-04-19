// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.IO;

/// <summary>
/// Provides a mechanism for reading unmanaged structures from a stream at a specific offset,
/// with support for both direct data access and pointer-chased (indirect) access patterns.
/// </summary>
/// <typeparam name="T">The unmanaged structure type to read from the stream.</typeparam>
/// <remarks>
/// <para>
/// <see cref="StreamPointer{T}"/> acts as a window into stream data, allowing random access
/// to structures without modifying the underlying stream's position. It is particularly useful
/// when working with file formats that contain embedded pointer tables or when needing to
/// read scattered data structures.
/// </para>
/// <para>
/// Two access modes are supported:
/// <list type="bullet">
///   <item><description><b>Direct:</b> Items are accessed contiguously from the pointer offset.</description></item>
///   <item><description><b>Pointer-chased:</b> Each index contains a 64-bit pointer that must be followed to reach the actual data.</description></item>
/// </list>
/// </para>
/// </remarks>
[DebuggerDisplay("Pointer = {Pointer}, Count = {Count}")]
[DebuggerTypeProxy(typeof(StreamPointerDebugView<>))]
public sealed class StreamPointer<T> where T : unmanaged
{
    /// <summary>
    /// Gets the size, in bytes, of type <typeparamref name="T"/>.
    /// </summary>
    public static readonly int SizeOf = Unsafe.SizeOf<T>();

    private readonly int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamPointer{T}"/> class from the current stream position.
    /// </summary>
    /// <param name="stream">The base stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public StreamPointer(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        BaseStream = stream;
        Pointer = stream.Position;
        _count = -1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamPointer{T}"/> class from the specified position.
    /// </summary>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the data begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public StreamPointer(Stream stream, long pointer)
    {
        ArgumentNullException.ThrowIfNull(stream);
        BaseStream = stream;
        Pointer = pointer;
        _count = -1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamPointer{T}"/> class for a contiguous array.
    /// </summary>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public StreamPointer(Stream stream, long pointer, int count)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        BaseStream = stream;
        Pointer = pointer;
        _count = count;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamPointer{T}"/> class with pointer-chase mode control.
    /// </summary>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the data or pointer array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <param name="isPointerArray">
    /// If <c>true</c>, the index accesses a pointer table where each index points to the actual data;
    /// if <c>false</c>, accesses are direct contiguous reads.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public StreamPointer(Stream stream, long pointer, int count, bool isPointerArray)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        BaseStream = stream;
        Pointer = pointer;
        _count = count;
        IsPointerArray = isPointerArray;
    }

    /// <summary>
    /// Gets the base stream from which data is read.
    /// </summary>
    public Stream BaseStream { get; }

    /// <summary>
    /// Gets or sets the base offset in the stream where data reading begins.
    /// </summary>
    /// <remarks>
    /// Modifying this property effectively repositions the stream pointer window
    /// without creating a new instance.
    /// </remarks>
    public long Pointer { get; set; }

    /// <summary>
    /// Gets the number of items accessible through this stream pointer,
    /// or <c>-1</c> if unbounded.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the stream offset immediately following the last accessible item.
    /// </summary>
    /// <remarks>
    /// This is the calculated end offset based on <see cref="Pointer"/>, <see cref="Count"/>,
    /// and <see cref="IsPointerArray"/>. For unbounded pointers, returns the stream length.
    /// </remarks>
    public long EndOffset => _count < 0 ? BaseStream.Length : Pointer + (IsPointerArray ? 8 : SizeOf) * _count;

    /// <summary>
    /// Gets a value indicating whether this instance accesses data through pointer indirection.
    /// </summary>
    /// <value>
    /// <c>true</c> if each index contains a 64-bit pointer to the actual data; otherwise, <c>false</c>.
    /// </value>
    public bool IsPointerArray { get; }

    /// <summary>
    /// Reads the item at the specified index from the stream.
    /// </summary>
    /// <param name="index">The zero-based index of the item to read.</param>
    /// <returns>The unmanaged structure read from the stream.</returns>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than or equal to <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            ValidateIndex(index);

            long originalPosition = BaseStream.Position;
            Span<byte> buffer = stackalloc byte[SizeOf];

            try
            {
                long targetPosition = GetItemPosition(index);

                BaseStream.Position = targetPosition;
                BaseStream.ReadExactly(buffer);

                return MemoryMarshal.Read<T>(buffer);
            }
            finally
            {
                BaseStream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// Gets the stream offset of the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item.</param>
    /// <returns>
    /// The stream offset where the item's data is located.
    /// In pointer-chase mode, this returns the resolved pointer value;
    /// otherwise, returns the calculated direct offset.
    /// </returns>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than or equal to <see cref="Count"/>.
    /// </exception>
    public long AddressOf(int index)
    {
        ValidateIndex(index);

        if (IsPointerArray)
        {
            long originalPosition = BaseStream.Position;
            Span<byte> pointerBuffer = stackalloc byte[8];

            try
            {
                BaseStream.Position = Pointer + (long)index * 8;
                BaseStream.ReadExactly(pointerBuffer);
                return BinaryPrimitives.ReadInt64LittleEndian(pointerBuffer);
            }
            finally
            {
                BaseStream.Position = originalPosition;
            }
        }

        return Pointer + (long)index * SizeOf;
    }

    /// <summary>
    /// Reads all accessible items into a span.
    /// </summary>
    /// <returns>A span containing the items read from the stream.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Count"/> is less than zero (unbounded).
    /// </exception>
    public Span<T> ToSpan()
    {
        if (_count < 0)
            throw new InvalidOperationException("Cannot create a span from an unbounded stream pointer. Specify a count first.");

        if (_count == 0)
            return [];

        T[] result = new T[_count];

        for (int i = 0; i < _count; i++)
        {
            result[i] = this[i];
        }

        return result;
    }

    /// <summary>
    /// Copies accessible items to the provided span.
    /// </summary>
    /// <param name="destination">The destination span to copy items to.</param>
    /// <returns>
    /// The number of items copied. This is the lesser of
    /// <paramref name="destination"/>.Length and <see cref="Count"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Count"/> is less than zero (unbounded).
    /// </exception>
    public int CopyTo(Span<T> destination)
    {
        if (_count < 0)
            throw new InvalidOperationException("Cannot copy from an unbounded stream pointer. Specify a count first.");

        int countToCopy = Math.Min(_count, destination.Length);

        for (int i = 0; i < countToCopy; i++)
        {
            destination[i] = this[i];
        }

        return countToCopy;
    }

    /// <summary>
    /// Validates that the specified index is within bounds.
    /// </summary>
    private void ValidateIndex(int index)
    {
        if (index < 0 || (_count >= 0 && index >= _count))
            throw new IndexOutOfRangeException();
    }

    /// <summary>
    /// Calculates the stream position for the item at the specified index.
    /// </summary>
    private long GetItemPosition(int index)
    {
        if (IsPointerArray)
        {
            Span<byte> pointerBuffer = stackalloc byte[8];
            BaseStream.Position = Pointer + (long)index * 8;
            BaseStream.ReadExactly(pointerBuffer);
            return BinaryPrimitives.ReadInt64LittleEndian(pointerBuffer);
        }

        return Pointer + (long)index * SizeOf;
    }
}
