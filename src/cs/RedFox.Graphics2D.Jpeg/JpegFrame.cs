namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Describes a JPEG frame parsed from a SOF (Start of Frame) marker, including dimensions, sampling factors, and component layout.
/// </summary>
public sealed class JpegFrame
{
    /// <summary>Sample precision in bits (typically 8).</summary>
    public int Precision { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Whether the frame uses progressive encoding.</summary>
    public bool Progressive { get; set; }

    /// <summary>Maximum horizontal sampling factor across all components.</summary>
    public int MaxHSample { get; set; }

    /// <summary>Maximum vertical sampling factor across all components.</summary>
    public int MaxVSample { get; set; }

    /// <summary>Width of the MCU grid in 8×8 blocks.</summary>
    public int McuWidth { get; set; }

    /// <summary>Height of the MCU grid in 8×8 blocks.</summary>
    public int McuHeight { get; set; }

    /// <summary>Total number of MCUs in the frame.</summary>
    public int McuCount { get; set; }

    /// <summary>Components keyed by their identifier.</summary>
    public Dictionary<int, JpegFrameComponent> Components { get; } = [];

    /// <summary>Component identifiers in the order they appear in the frame header.</summary>
    public List<int> ComponentOrder { get; } = [];
}
