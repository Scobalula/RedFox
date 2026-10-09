namespace RedFox.Compression;

/// <summary>
/// Decodes one indexed compressed block into a caller-provided buffer.
/// </summary>
/// <param name="blockIndex">The index of the block being decoded.</param>
/// <param name="source">The compressed block data.</param>
/// <param name="destination">The buffer that receives decoded data.</param>
/// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
public delegate int BlockDecoder(int blockIndex, ReadOnlySpan<byte> source, Span<byte> destination);
