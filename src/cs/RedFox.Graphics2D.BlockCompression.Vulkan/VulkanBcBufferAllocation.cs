using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Groups the parameters for a Vulkan buffer creation call.
/// </summary>
public readonly struct VulkanBcBufferAllocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcBufferAllocation"/> struct.
    /// </summary>
    /// <param name="size">The buffer size in bytes.</param>
    /// <param name="usage">The buffer usage flags.</param>
    /// <param name="requiredProperties">Memory property flags that must all be present.</param>
    /// <param name="preferredProperties">Memory property flags that are preferred but not required.</param>
    public VulkanBcBufferAllocation(ulong size, BufferUsageFlags usage, MemoryPropertyFlags requiredProperties, MemoryPropertyFlags preferredProperties)
    {
        Size = size;
        Usage = usage;
        RequiredProperties = requiredProperties;
        PreferredProperties = preferredProperties;
    }

    /// <summary>
    /// Gets the buffer size in bytes.
    /// </summary>
    public ulong Size { get; }

    /// <summary>
    /// Gets the buffer usage flags.
    /// </summary>
    public BufferUsageFlags Usage { get; }

    /// <summary>
    /// Gets the memory property flags that must all be present.
    /// </summary>
    public MemoryPropertyFlags RequiredProperties { get; }

    /// <summary>
    /// Gets the memory property flags that are preferred but not required.
    /// </summary>
    public MemoryPropertyFlags PreferredProperties { get; }
}
