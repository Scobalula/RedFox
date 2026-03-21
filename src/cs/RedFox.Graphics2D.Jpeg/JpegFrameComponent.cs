namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegFrameComponent
{
    public int Id { get; set; }
    public int HSample { get; set; }
    public int VSample { get; set; }
    public int QuantizationTableId { get; set; }
    public int BlocksPerRow { get; set; }
    public int BlocksPerColumn { get; set; }
    public int[][]? Blocks { get; set; }
    public int PreviousDc { get; set; }
}
