namespace RedFox.Graphics2D.Jpeg;

internal readonly struct JpegScanComponent
{
    public int ComponentId { get; init; }
    public int DcTableId { get; init; }
    public int AcTableId { get; init; }
}
