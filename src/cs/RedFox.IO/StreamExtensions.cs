// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.IO
{
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
            GetDefaultRange(stream, out long startPosition, out long endPosition);
            return Scan(stream, BytePattern.Parse(hexString), startPosition, endPosition, false);
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
            GetDefaultRange(stream, out long startPosition, out long endPosition);
            return Scan(stream, BytePattern.Parse(hexString), startPosition, endPosition, firstOccurence);
        }

        /// <summary>
        /// Scans for the given pattern in the <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
        /// <param name="startPosition">Position to start searching from</param>
        /// <param name="endPosition">Position to end the search at</param>
        /// <returns>Absolute positions of occurences</returns>
        public static long[] Scan(this Stream stream, string hexString, long startPosition, long endPosition)
        {
            return Scan(stream, BytePattern.Parse(hexString), startPosition, endPosition, false);
        }

        /// <summary>
        /// Scans for the given pattern in the <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="hexString">Hex String with masks, for example: "1A FF ?? ?? 00"</param>
        /// <param name="startPosition">Position to start searching from</param>
        /// <param name="endPosition">Position to end the search at</param>
        /// <param name="firstOccurence">Whether or not to stop at the first result</param>
        /// <returns>Absolute positions of occurences</returns>
        public static long[] Scan(this Stream stream, string hexString, long startPosition, long endPosition, bool firstOccurence)
        {
            return Scan(stream, BytePattern.Parse(hexString), startPosition, endPosition, firstOccurence);
        }

        /// <summary>
        /// Scans for the given pattern in the <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="pattern">Pattern to search for</param>
        /// <returns>Absolute positions of occurences</returns>
        public static long[] Scan(this Stream stream, Pattern<byte> pattern)
        {
            GetDefaultRange(stream, out long startPosition, out long endPosition);
            return Scan(stream, pattern, startPosition, endPosition, false);
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
            GetDefaultRange(stream, out long startPosition, out long endPosition);
            return Scan(stream, pattern, startPosition, endPosition, firstOccurence);
        }

        /// <summary>
        /// Scans for the given pattern in the <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="pattern">Pattern to search for</param>
        /// <param name="startPosition">Position to start searching from</param>
        /// <param name="endPosition">Position to end the search at</param>
        /// <returns>Absolute positions of occurences</returns>
        public static long[] Scan(this Stream stream, Pattern<byte> pattern, long startPosition, long endPosition)
        {
            return Scan(stream, pattern, startPosition, endPosition, false);
        }

        /// <summary>
        /// Scans for the given pattern in the <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="pattern">Pattern to search for</param>
        /// <param name="startPosition">Position to start searching from</param>
        /// <param name="endPosition">Position to end the search at</param>
        /// <param name="firstOccurence">Whether or not to stop at the first result</param>
        /// <returns>Absolute positions of occurences</returns>
        public static long[] Scan(this Stream stream, Pattern<byte> pattern, long startPosition, long endPosition, bool firstOccurence)
        {
            return Scan(stream, pattern.Needle, pattern.Mask, startPosition, endPosition, firstOccurence, DefaultScanBufferSize);
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
            GetDefaultRange(stream, out long startPosition, out long endPosition);
            return Scan(stream, needle, mask, startPosition, endPosition, false, DefaultScanBufferSize);
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
            GetDefaultRange(stream, out long startPosition, out long endPosition);
            return Scan(stream, needle, mask, startPosition, endPosition, firstOccurence, DefaultScanBufferSize);
        }

        /// <summary>
        /// Scans for the given pattern in the <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="needle">Byte Array Needle to search for</param>
        /// <param name="mask">Mask array for unknown bytes/pattern matching</param>
        /// <param name="startPosition">Position to start searching from</param>
        /// <param name="endPosition">Position to end the search at</param>
        /// <returns>Absolute positions of occurences</returns>
        public static long[] Scan(this Stream stream, byte[] needle, byte[] mask, long startPosition, long endPosition)
        {
            return Scan(stream, needle, mask, startPosition, endPosition, false, DefaultScanBufferSize);
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
            EnsureSeekableReadable(stream);

            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "Start position must be zero or greater.");
            }

            if (end < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(end), end, "End position must be zero or greater.");
            }

            if (end < start)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(end),
                    end,
                    "End position must be greater than or equal to start position.");
            }

            if (start > stream.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(start),
                    start,
                    "Start position cannot be greater than stream length.");
            }

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
                    readChunk: (offset, destination) => ReadChunk(stream, offset, destination));
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        private static void GetDefaultRange(Stream stream, out long startPosition, out long endPosition)
        {
            ArgumentNullException.ThrowIfNull(stream);
            EnsureSeekableReadable(stream);
            startPosition = 0;
            endPosition = stream.Length;
        }

        private static void EnsureSeekableReadable(Stream stream)
        {
            if (!stream.CanRead)
            {
                throw new NotSupportedException("Stream does not support reading.");
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("Stream does not support seeking.");
            }
        }

        private static int ReadChunk(Stream stream, long position, Span<byte> destination)
        {
            stream.Position = position;
            return stream.Read(destination);
        }
    }
}
