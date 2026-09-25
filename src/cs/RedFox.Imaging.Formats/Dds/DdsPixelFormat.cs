using System.Runtime.InteropServices;

namespace RedFox.Imaging.Formats.Dds;

/// <summary>
/// Represents the 32-byte DDS pixel format descriptor embedded in the DDS header.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct DdsPixelFormat
{
    /// <summary>
    /// Size of this structure in bytes (must be 32).
    /// </summary>
    public uint Size;

    /// <summary>
    /// Flags describing the pixel format contents.
    /// </summary>
    public DdsPixelFormatFlags Flags;

    /// <summary>
    /// Four-character code identifying the compression scheme, if any.
    /// </summary>
    public uint FourCC;

    /// <summary>
    /// Number of bits per pixel for uncompressed formats.
    /// </summary>
    public uint RgbBitCount;

    /// <summary>
    /// Bit mask for the red (or luminance) channel.
    /// </summary>
    public uint RBitMask;

    /// <summary>
    /// Bit mask for the green channel.
    /// </summary>
    public uint GBitMask;

    /// <summary>
    /// Bit mask for the blue channel.
    /// </summary>
    public uint BBitMask;

    /// <summary>
    /// Bit mask for the alpha channel.
    /// </summary>
    public uint ABitMask;
}
