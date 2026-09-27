namespace RedFox.Patterns;

/// <summary>
/// Reads bytes at a source offset into a destination span.
/// </summary>
/// <param name="offset">The source offset to read from.</param>
/// <param name="destination">The destination span to fill.</param>
/// <returns>The number of bytes written.</returns>
public delegate int BytePatternChunkReader(long offset, Span<byte> destination);
