// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using global::System.IO;

namespace RedFox.IO;

/// <summary>
/// Provides extension methods for <see cref="Stream"/>.
/// </summary>
public static class StreamExtensions
{
    private const int DefaultScanBufferSize = 0x10000;

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, string hexString)
    {
        return Scan(stream, BytePattern.Parse(hexString), 0, stream.Length, false);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, string hexString, bool firstOccurence)
    {
        return Scan(stream, BytePattern.Parse(hexString), 0, stream.Length, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, string hexString, long start, long end)
    {
        return Scan(stream, BytePattern.Parse(hexString), start, end, false);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, string hexString, long start, long end, bool firstOccurence)
    {
        return Scan(stream, BytePattern.Parse(hexString), start, end, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="pattern">Pattern to search for</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, Pattern<byte> pattern)
    {
        return Scan(stream, pattern, 0, stream.Length, false);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="pattern">Pattern to search for</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, Pattern<byte> pattern, bool firstOccurence)
    {
        return Scan(stream, pattern, 0, stream.Length, firstOccurence);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="pattern">Pattern to search for</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, Pattern<byte> pattern, long start, long end)
    {
        return Scan(stream, pattern, start, end, false);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="pattern">Pattern to search for</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, Pattern<byte> pattern, long start, long end, bool firstOccurence)
    {
        return Scan(stream, pattern.Needle, pattern.Mask, start, end, firstOccurence, DefaultScanBufferSize);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="needle">Byte Array Needle to search for</param>
    /// <param name="mask">Mask array for unknown bytes/pattern matching</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, byte[] needle, byte[] mask)
    {
        return Scan(stream, needle, mask, 0, stream.Length, false, DefaultScanBufferSize);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="needle">Byte Array Needle to search for</param>
    /// <param name="mask">Mask array for unknown bytes/pattern matching</param>
    /// <param name="firstOccurence">Whether or not to stop at the first result</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, byte[] needle, byte[] mask, bool firstOccurence)
    {
        return Scan(stream, needle, mask, 0, stream.Length, firstOccurence, DefaultScanBufferSize);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="needle">Byte Array Needle to search for</param>
    /// <param name="mask">Mask array for unknown bytes/pattern matching</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, byte[] needle, byte[] mask, long start, long end)
    {
        return Scan(stream, needle, mask, start, end, false, DefaultScanBufferSize);
    }

    /// <summary>
    /// Scans for the given pattern in the <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="needle">Byte Array Needle to search for</param>
    /// <param name="mask">Mask array for unknown bytes/pattern matching</param>
    /// <param name="start">Position to start searching from</param>
    /// <param name="end">Position to end the search at</param>
    /// <param name="firstOnly">Whether or not to stop at the first result</param>
    /// <param name="bufferSize">The size of the scan buffer.</param>
    /// <returns>Absolute positions of occurences</returns>
    public static long[] Scan(this Stream stream, byte[] needle, byte[] mask, long start, long end, bool firstOnly, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(needle);
        ArgumentNullException.ThrowIfNull(mask);
        if (!stream.CanRead)
        {
            throw new NotSupportedException("Stream does not support reading.");
        }

        if (!stream.CanSeek)
        {
            throw new NotSupportedException("Stream does not support seeking.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(start, nameof(start));
        ArgumentOutOfRangeException.ThrowIfNegative(end, nameof(start));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end, nameof(start));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, stream.Length, nameof(start));

        long originalPosition = stream.Position;
        try
        {
            long clampedEndPosition = Math.Min(end, stream.Length);
            return BytePatternScanner.Scan(
                new Pattern<byte>(needle, mask),
                start,
                clampedEndPosition,
                bufferSize,
                firstOnly,
                readChunk: (offset, destination) =>
                {
                    stream.Position = offset;
                    return stream.Read(destination);
                });
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> from the current position of the specified stream.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="stream">The base stream to read from.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance anchored at the current stream position.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public static StreamPointer<T> AsPointer<T>(this Stream stream) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new StreamPointer<T>(stream);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> from the specified position in the stream.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the data begins.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance anchored at the specified position.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public static StreamPointer<T> AsPointer<T>(this Stream stream, long pointer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new StreamPointer<T>(stream, pointer);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> for a contiguous array at the specified position.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance for the specified array range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static StreamPointer<T> AsPointer<T>(this Stream stream, long pointer, int count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new StreamPointer<T>(stream, pointer, count);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> with pointer-chase mode control.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the data or pointer array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <param name="isPointerArray">
    /// If <c>true</c>, the index accesses a pointer table where each index points to the actual data;
    /// if <c>false</c>, accesses are direct contiguous reads.
    /// </param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance with the specified configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static StreamPointer<T> AsPointer<T>(this Stream stream, long pointer, int count, bool isPointerArray) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new StreamPointer<T>(stream, pointer, count, isPointerArray);
    }

    /// <summary>
    /// Creates a <see cref="StreamPointer{T}"/> for a contiguous array at the specified position.
    /// </summary>
    /// <typeparam name="T">The unmanaged structure type to read.</typeparam>
    /// <param name="stream">The base stream to read from.</param>
    /// <param name="pointer">The offset in the stream where the array begins.</param>
    /// <param name="count">The number of items in the array.</param>
    /// <returns>A new <see cref="StreamPointer{T}"/> instance for the specified array range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static StreamPointer<T> CreatePointer<T>(this Stream stream, long pointer, int count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new StreamPointer<T>(stream, pointer, count);
    }
}
