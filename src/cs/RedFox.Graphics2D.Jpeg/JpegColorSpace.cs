namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Identifies the color space used by a JPEG image.
/// </summary>
public enum JpegColorSpace
{
    /// <summary>Single-channel luminance.</summary>
    Grayscale,

    /// <summary>Luminance plus blue-difference and red-difference chroma (standard JPEG).</summary>
    YCbCr,

    /// <summary>Direct red, green, blue channels.</summary>
    Rgb,

    /// <summary>Cyan, magenta, yellow, key (black) channels.</summary>
    Cmyk,

    /// <summary>YCbCr-based CMYK variant.</summary>
    Ycck,
}
