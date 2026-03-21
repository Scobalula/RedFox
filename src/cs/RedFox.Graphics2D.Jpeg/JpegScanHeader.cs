namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegScanHeader
{
    public JpegScanComponent[] Components { get; set; } = [];
    public int SpectralStart { get; set; }
    public int SpectralEnd { get; set; }
    public int SuccessiveHigh { get; set; }
    public int SuccessiveLow { get; set; }
}
