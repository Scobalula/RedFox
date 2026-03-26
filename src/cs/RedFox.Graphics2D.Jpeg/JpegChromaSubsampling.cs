namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Chroma subsampling modes used during JPEG encoding.
/// </summary>
public enum JpegChromaSubsampling
{
    /// <summary>No subsampling; full chroma resolution (4:4:4).</summary>
    Yuv444,

    /// <summary>Horizontal chroma subsampling (4:2:2).</summary>
    Yuv422,
    Yuv420,
}
