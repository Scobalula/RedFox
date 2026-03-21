namespace RedFox.IO;

/// <summary>
/// Provides helper methods for validating stream capabilities and throwing exceptions when a stream does not support
/// required operations.
/// </summary>
public static class StreamExceptions
{
    /// <summary>
    /// Throws an exception if the specified stream does not support reading.
    /// </summary>
    /// <param name="stream">The stream to check for read capability.</param>
    /// <exception cref="NotSupportedException">Thrown if the stream does not support reading.</exception>
    public static void ThrowIfUnreadable(Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new NotSupportedException("Stream does not support reading.");
        }
    }

    /// <summary>
    /// Throws an exception if the specified stream does not support seeking.
    /// </summary>
    /// <param name="stream">The stream to check for seek capability.</param>
    /// <exception cref="NotSupportedException">Thrown if the stream does not support seeking.</exception>
    public static void ThrowIfUnseekable(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new NotSupportedException("Stream does not support seeking.");
        }
    }
}
