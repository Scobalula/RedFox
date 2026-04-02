using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Uniform data consumed by the BC decode compute shaders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VulkanBcDecodeConstants
{
    /// <summary>
    /// Gets or sets the output image width in pixels.
    /// </summary>
    public uint Width;

    /// <summary>
    /// Gets or sets the output image height in pixels.
    /// </summary>
    public uint Height;
}
