using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Represents a Vulkan buffer and its backing memory allocation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcBuffer"/> struct.
/// </remarks>
/// <param name="handle">The Vulkan buffer handle.</param>
/// <param name="memory">The backing device memory allocation.</param>
/// <param name="size">The buffer size in bytes.</param>
/// <param name="memoryPropertyFlags">The memory properties selected for the allocation.</param>
public readonly struct VulkanBcBuffer(Silk.NET.Vulkan.Buffer handle, DeviceMemory memory, ulong size, MemoryPropertyFlags memoryPropertyFlags)
{
    /// <summary>
    /// Gets the Vulkan buffer handle.
    /// </summary>
    public Silk.NET.Vulkan.Buffer Handle { get; } = handle;

    /// <summary>
    /// Gets the backing device memory allocation.
    /// </summary>
    public DeviceMemory Memory { get; } = memory;

    /// <summary>
    /// Gets the buffer size in bytes.
    /// </summary>
    public ulong Size { get; } = size;

    /// <summary>
    /// Gets the Vulkan memory property flags for the backing allocation.
    /// </summary>
    public MemoryPropertyFlags MemoryPropertyFlags { get; } = memoryPropertyFlags;
}
