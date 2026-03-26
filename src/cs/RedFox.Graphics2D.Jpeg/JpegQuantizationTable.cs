namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// A 64-element quantization table used during JPEG DCT coefficient quantization.
/// </summary>
public sealed class JpegQuantizationTable
{
    /// <summary>The 64 quantization values in zig-zag order.</summary>
    public readonly int[] Values = new int[64];

    /// <summary>Table identifier (0–3).</summary>
    public int Id { get; set; }
}
