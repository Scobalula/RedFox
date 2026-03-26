namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Parsed SOS (Start of Scan) segment header containing component assignments and progressive-scan parameters.
/// </summary>
public sealed class JpegScanHeader
{
    /// <summary>Components participating in this scan.</summary>
    public JpegScanComponent[] Components { get; set; } = [];

    /// <summary>First DCT coefficient index in this scan (progressive mode).</summary>
    public int SpectralStart { get; set; }

    /// <summary>Last DCT coefficient index in this scan (progressive mode).</summary>
    public int SpectralEnd { get; set; }

    /// <summary>High bit of the successive approximation range.</summary>
    public int SuccessiveHigh { get; set; }

    /// <summary>Low bit of the successive approximation range.</summary>
    public int SuccessiveLow { get; set; }
}
