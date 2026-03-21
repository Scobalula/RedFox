namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegFrame
{
    public int Precision { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Progressive { get; set; }
    public int MaxHSample { get; set; }
    public int MaxVSample { get; set; }
    public int McuWidth { get; set; }
    public int McuHeight { get; set; }
    public int McuCount { get; set; }
    public Dictionary<int, JpegFrameComponent> Components { get; } = [];
    public List<int> ComponentOrder { get; } = [];
}
