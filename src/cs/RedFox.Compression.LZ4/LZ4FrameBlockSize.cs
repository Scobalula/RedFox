namespace RedFox.Compression.LZ4;

/// <summary>
/// Specifies the maximum block size used by an LZ4 frame.
/// </summary>
public enum LZ4FrameBlockSize
{
    /// <summary>
    /// Limits frame blocks to 64 KiB.
    /// </summary>
    KB64	= 4,

    /// <summary>
    /// Limits frame blocks to 256 KiB.
    /// </summary>
    KB256	= 5,

    /// <summary>
    /// Limits frame blocks to 1 MiB.
    /// </summary>
    MB1		= 6,

    /// <summary>
    /// Limits frame blocks to 4 MiB.
    /// </summary>
    MB4		= 7
}
