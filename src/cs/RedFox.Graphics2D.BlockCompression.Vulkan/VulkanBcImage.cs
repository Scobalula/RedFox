using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Represents a Vulkan image, its view, and its backing memory allocation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcImage"/> struct.
/// </remarks>
/// <param name="handle">The Vulkan image handle.</param>
/// <param name="memory">The backing device memory allocation.</param>
/// <param name="view">The image view used by shaders.</param>
public readonly struct VulkanBcImage(Silk.NET.Vulkan.Image handle, DeviceMemory memory, ImageView view)
{

    /// <summary>
    /// Gets the Vulkan image handle.
    /// </summary>
    public Silk.NET.Vulkan.Image Handle { get; } = handle;

    /// <summary>
    /// Gets the backing device memory allocation.
    /// </summary>
    public DeviceMemory Memory { get; } = memory;

    /// <summary>
    /// Gets the image view used by shaders.
    /// </summary>
    public ImageView View { get; } = view;
}
