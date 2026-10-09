namespace RedFox.Compression.Deflate;

/// <summary>
/// Compresses and decompresses data in the zlib format.
/// </summary>
public sealed class ZLibCodec : DeflateCodec
{
    /// <summary>
    /// Initializes a zlib codec using the default compression level.
    /// </summary>
    public ZLibCodec() : this(DeflateLevel.DefaultCompression) { }

    /// <summary>
    /// Initializes a zlib codec using the specified compression level.
    /// </summary>
    /// <param name="level">The compression level.</param>
    public ZLibCodec(DeflateLevel level) : base(15, level, ReadOnlySpan<byte>.Empty) { }

    /// <summary>
    /// Initializes a zlib codec using the specified compression level and preset dictionary.
    /// </summary>
    /// <param name="level">The compression level.</param>
    /// <param name="dictionary">The preset history dictionary.</param>
    public ZLibCodec(DeflateLevel level, ReadOnlySpan<byte> dictionary) : base(15, level, dictionary) { }
}
