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
}
