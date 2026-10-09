namespace RedFox.Compression.Deflate;

/// <summary>
/// Compresses and decompresses data in the gzip format.
/// </summary>
public sealed class GZipCodec : DeflateCodec
{
    /// <summary>
    /// Initializes a gzip codec using the default compression level.
    /// </summary>
    public GZipCodec() : this(DeflateLevel.DefaultCompression) { }

    /// <summary>
    /// Initializes a gzip codec using the specified compression level.
    /// </summary>
    /// <param name="level">The compression level.</param>
    public GZipCodec(DeflateLevel level) : base(31, level, ReadOnlySpan<byte>.Empty) { }
}
