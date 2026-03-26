namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Describes a single component within a JPEG scan segment, including its Huffman table assignments.
/// </summary>
public readonly struct JpegScanComponent
{
    /// <summary>Component identifier matching the frame header.</summary>
    public int ComponentId { get; init; }

    /// <summary>DC Huffman table identifier (0–3).</summary>
    public int DcTableId { get; init; }

    /// <summary>AC Huffman table identifier (0–3).</summary>
    public int AcTableId { get; init; }
}
