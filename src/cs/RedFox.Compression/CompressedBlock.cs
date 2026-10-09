namespace RedFox.Compression;

/// <summary>
/// Describes a compressed block and its position in the source data.
/// </summary>
/// <param name="CompressedOffset">The block offset relative to the source start.</param>
/// <param name="CompressedLength">The block length in compressed bytes.</param>
/// <param name="DecompressedLength">The block length in decompressed bytes.</param>
public readonly record struct CompressedBlock(long CompressedOffset, int CompressedLength, int DecompressedLength);
