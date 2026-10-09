namespace RedFox.Compression.LZ4;

internal unsafe struct LZ4FramePreferences
{
    internal LZ4FrameInfo	FrameInfo;
    internal int			CompressionLevel;
    internal uint			AutoFlush;
    internal uint			FavorDecompressionSpeed;
    private fixed uint _reserved[3];
}
