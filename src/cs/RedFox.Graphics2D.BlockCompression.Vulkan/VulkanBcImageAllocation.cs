using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Groups the parameters for a Vulkan image creation call.
/// </summary>
public readonly struct VulkanBcImageAllocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcImageAllocation"/> struct.
    /// </summary>
    /// <param name="format">The image pixel format.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="usage">The image usage flags.</param>
    public VulkanBcImageAllocation(Format format, uint width, uint height, ImageUsageFlags usage)
    {
        Format = format;
        Width = width;
        Height = height;
        Usage = usage;
    }

    /// <summary>
    /// Gets the image pixel format.
    /// </summary>
    public Format Format { get; }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Gets the image usage flags.
    /// </summary>
    public ImageUsageFlags Usage { get; }
}
