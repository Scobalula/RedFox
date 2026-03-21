namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Options for controlling JPEG encoding behavior.
/// </summary>
public sealed class JpegEncoderOptions
{
    /// <summary>
    /// Quality factor from 1 (worst) to 100 (best). Default is 75.
    /// Uses the standard IJG/libjpeg quality scaling formula.
    /// </summary>
    public int Quality { get; set; } = 75;

    /// <summary>
    /// Chroma subsampling mode. Default is 4:2:0 for best compression.
    /// </summary>
    public JpegChromaSubsampling Subsampling { get; set; } = JpegChromaSubsampling.Yuv420;

    /// <summary>
    /// Whether to write an optimized Huffman table derived from the image data.
    /// When false (default), uses the standard JPEG Annex K tables.
    /// </summary>
    public bool OptimizeHuffmanTables { get; set; }
}
