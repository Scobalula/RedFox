namespace RedFox.IO;

/// <summary>
/// Reads the next sequence of bytes into a destination span.
/// </summary>
/// <param name="destination">The destination span to fill.</param>
/// <returns>The number of bytes written, or zero at end of source.</returns>
public delegate int NullTerminatedChunkReader(Span<byte> destination);
