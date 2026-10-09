namespace RedFox.Compression.LZ4;

internal struct LZ4FrameInfo
{
    internal int	BlockSize;
    internal int	BlockMode;
    internal int	ContentChecksum;
    internal int	FrameType;
    internal ulong	ContentSize;
    internal uint	DictionaryId;
    internal int	BlockChecksum;
}
