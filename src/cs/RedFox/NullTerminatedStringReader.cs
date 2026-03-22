using System.Text;

namespace RedFox;

/// <summary>
/// Provides helpers for reading null-terminated byte and string data from chunked sources.
/// </summary>
public static class NullTerminatedStringReader
{
    /// <summary>
    /// Represents a callback that fills a destination span with the next sequence of bytes.
    /// </summary>
    /// <param name="destination">The destination span to fill.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>. Return 0 to indicate end-of-source.</returns>
    public delegate int ReadChunk(Span<byte> destination);

    /// <summary>
    /// Represents a callback that creates an exception when a null terminator is not found.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    public delegate Exception ExceptionFactory();

    /// <summary>
    /// Gets the null-terminator byte width for the specified encoding.
    /// </summary>
    /// <param name="encoding">The text encoding.</param>
    /// <returns>The number of bytes that represent a null terminator for the encoding.</returns>
    public static int GetTerminatorLength(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        int byteCount = encoding.GetByteCount("\0");
        return byteCount <= 0 ? 1 : byteCount;
    }

    /// <summary>
    /// Gets the alignment width in bytes for character boundaries of the specified encoding.
    /// </summary>
    /// <param name="encoding">The text encoding.</param>
    /// <returns>The character width in bytes used for terminator alignment checks.</returns>
    public static int GetCharacterWidth(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
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

    /// <summary>
    /// Reads null-terminated bytes using the provided encoding and chunk reader callback.
    /// </summary>
    /// <param name="encoding">The text encoding used to determine terminator width and character alignment.</param>
    /// <param name="maxBytes">The maximum number of bytes to inspect while searching for the terminator.</param>
    /// <param name="chunkSize">The requested chunk size passed to the reader callback per iteration.</param>
    /// <param name="readChunk">The callback used to read bytes from the source.</param>
    /// <param name="onMissing">Factory used when no terminator is found before reaching <paramref name="maxBytes"/>.</param>
    /// <returns>The bytes before the null terminator.</returns>
    public static byte[] ReadNullTerminatedBytes(Encoding encoding, int maxBytes, int chunkSize, ReadChunk readChunk, ExceptionFactory onMissing)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        int terminatorLength = GetTerminatorLength(encoding);
        int characterWidth = GetCharacterWidth(encoding);
        ValidateReadArguments(maxBytes, chunkSize, readChunk, onMissing);
        ValidateEncodingDerivedValues(encoding, terminatorLength, characterWidth);

        int effectiveChunkSize = Math.Min(chunkSize, maxBytes);
        byte[] outputBuffer = new byte[Math.Min(Math.Max(effectiveChunkSize * 2, effectiveChunkSize), maxBytes)];
        int totalBytesRead = 0;
        while (totalBytesRead < maxBytes)
        {
            int requestedBytes = Math.Min(effectiveChunkSize, maxBytes - totalBytesRead);
            EnsureCapacity(ref outputBuffer, totalBytesRead + requestedBytes, maxBytes);
            int bytesRead = readChunk(outputBuffer.AsSpan(totalBytesRead, requestedBytes));

            if (bytesRead == 0)
            {
                break;
            }

            ValidateChunkReadCount(bytesRead, requestedBytes);

            int searchStartIndex = Math.Max(0, totalBytesRead - terminatorLength + 1);
            int newTotalBytesRead = totalBytesRead + bytesRead;
            int searchEndExclusive = newTotalBytesRead - terminatorLength + 1;
            if (TryFindTerminator(outputBuffer, searchStartIndex, searchEndExclusive, terminatorLength, characterWidth, out int terminatorIndex))
            {
                byte[] result = new byte[terminatorIndex];
                Buffer.BlockCopy(outputBuffer, 0, result, 0, terminatorIndex);
                return result;
            }

            totalBytesRead = newTotalBytesRead;
        }

        throw onMissing();
    }

    /// <summary>
    /// Reads a null-terminated string using the provided encoding and chunk reader callback.
    /// </summary>
    /// <param name="encoding">The text encoding used to decode bytes.</param>
    /// <param name="maxBytes">The maximum number of bytes to inspect while searching for the terminator.</param>
    /// <param name="chunkSize">The requested chunk size passed to the reader callback per iteration.</param>
    /// <param name="readChunk">The callback used to read bytes from the source.</param>
    /// <param name="onMissing">Factory used when no terminator is found before reaching <paramref name="maxBytes"/>.</param>
    /// <returns>The decoded string value before the null terminator.</returns>
    public static string ReadNullTerminatedString(Encoding encoding, int maxBytes, int chunkSize, ReadChunk readChunk, ExceptionFactory onMissing)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        byte[] bytes = ReadNullTerminatedBytes(encoding, maxBytes, chunkSize, readChunk, onMissing);
        return encoding.GetString(bytes);
    }

    private static void ValidateReadArguments(int maxBytes, int chunkSize, ReadChunk readChunk, ExceptionFactory onMissing)
    {
        if (maxBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Maximum byte count must be at least one.");
        }

        if (chunkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be at least one.");
        }

        ArgumentNullException.ThrowIfNull(readChunk);
        ArgumentNullException.ThrowIfNull(onMissing);
    }

    private static void ValidateEncodingDerivedValues(Encoding encoding, int terminatorLength, int characterWidth)
    {
        if (terminatorLength < 1)
        {
            string message = $"Encoding '{encoding.WebName}' produced invalid terminator width {terminatorLength}.";
            throw new InvalidOperationException(message);
        }

        if (characterWidth < 1)
        {
            string message = $"Encoding '{encoding.WebName}' produced invalid character width {characterWidth}.";
            throw new InvalidOperationException(message);
        }
    }

    private static void ValidateChunkReadCount(int bytesRead, int requestedBytes)
    {
        if (bytesRead < 0)
        {
            throw new InvalidOperationException("Chunk reader returned a negative byte count.");
        }

        if (bytesRead > requestedBytes)
        {
            throw new InvalidOperationException("Chunk reader returned more bytes than requested.");
        }
    }

    private static bool TryFindTerminator(byte[] buffer, int start, int end, int terminatorLength, int characterWidth, out int terminatorIndex)
    {
        int alignedStart = AlignUp(start, characterWidth);
        for (int currentIndex = alignedStart; currentIndex < end; currentIndex += characterWidth)
        {
            if (!IsNullTerminatorAt(buffer, currentIndex, terminatorLength))
            {
                continue;
            }

            terminatorIndex = currentIndex;
            return true;
        }

        terminatorIndex = -1;
        return false;
    }

    private static bool IsNullTerminatorAt(byte[] buffer, int startIndex, int terminatorLength)
    {
        for (int index = 0; index < terminatorLength; index++)
        {
            if (buffer[startIndex + index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int AlignUp(int value, int alignment)
    {
        int remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }

    private static void EnsureCapacity(ref byte[] buffer, int requiredLength, int maxLength)
    {
        if (requiredLength <= buffer.Length)
        {
            return;
        }

        long doubledLength = (long)buffer.Length * 2;
        int doubledLengthCapped = doubledLength > int.MaxValue ? int.MaxValue : (int)doubledLength;
        int targetLength = Math.Max(requiredLength, doubledLengthCapped);
        int newLength = Math.Min(targetLength, maxLength);
        Array.Resize(ref buffer, newLength);
    }
}
