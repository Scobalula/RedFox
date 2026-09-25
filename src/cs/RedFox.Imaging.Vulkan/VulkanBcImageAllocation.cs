using Silk.NET.Vulkan;

namespace RedFox.Imaging.Vulkan;

/// <summary>
/// Groups the parameters for a Vulkan image creation call.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcImageAllocation"/> struct.
/// </remarks>
/// <param name="format">The image pixel format.</param>
/// <param name="width">The image width in pixels.</param>
/// <param name="height">The image height in pixels.</param>
/// <param name="usage">The image usage flags.</param>
public readonly struct VulkanBcImageAllocation(Format format, uint width, uint height, ImageUsageFlags usage)
{

    /// <summary>
    /// Gets the image pixel format.
    /// </summary>
    public Format Format { get; } = format;

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public uint Width { get; } = width;

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public uint Height { get; } = height;

    /// <summary>
    /// Gets the image usage flags.
    /// </summary>
    public ImageUsageFlags Usage { get; } = usage;
}
