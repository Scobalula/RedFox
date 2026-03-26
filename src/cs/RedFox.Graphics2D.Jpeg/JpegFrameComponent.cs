namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Describes a single component within a JPEG frame, including sampling factors, quantization assignment, and decoded block storage.
/// </summary>
public sealed class JpegFrameComponent
{
    /// <summary>Component identifier as declared in the frame header.</summary>
    public int Id { get; set; }

    /// <summary>Horizontal sampling factor.</summary>
    public int HSample { get; set; }

    /// <summary>Vertical sampling factor.</summary>
    public int VSample { get; set; }

    /// <summary>Quantization table identifier assigned to this component.</summary>
    public int QuantizationTableId { get; set; }

    /// <summary>Number of 8×8 blocks per row for this component.</summary>
    public int BlocksPerRow { get; set; }

    /// <summary>Number of 8×8 blocks per column for this component.</summary>
    public int BlocksPerColumn { get; set; }

    /// <summary>Decoded DCT coefficient blocks; each inner array contains 64 coefficients.</summary>
    public int[][]? Blocks { get; set; }

    /// <summary>Running DC coefficient prediction value used during scan decoding.</summary>
    public int PreviousDc { get; set; }
}
