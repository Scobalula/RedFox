using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Describes a buffer memory barrier including access masks and pipeline stage transitions.
/// </summary>
public readonly struct VulkanBcBufferMemoryBarrier
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcBufferMemoryBarrier"/> struct.
    /// </summary>
    /// <param name="buffer">The buffer to insert the barrier for.</param>
    /// <param name="sourceAccessMask">The access mask of the producing stage.</param>
    /// <param name="destinationAccessMask">The access mask of the consuming stage.</param>
    /// <param name="sourceStageMask">The producing pipeline stage.</param>
    /// <param name="destinationStageMask">The consuming pipeline stage.</param>
    public VulkanBcBufferMemoryBarrier(VulkanBcBuffer buffer, AccessFlags sourceAccessMask, AccessFlags destinationAccessMask, PipelineStageFlags sourceStageMask, PipelineStageFlags destinationStageMask)
    {
        Buffer = buffer;
        SourceAccessMask = sourceAccessMask;
        DestinationAccessMask = destinationAccessMask;
        SourceStageMask = sourceStageMask;
        DestinationStageMask = destinationStageMask;
    }

    /// <summary>
    /// Gets the buffer to insert the barrier for.
    /// </summary>
    public VulkanBcBuffer Buffer { get; }

    /// <summary>
    /// Gets the access mask of the producing stage.
    /// </summary>
    public AccessFlags SourceAccessMask { get; }

    /// <summary>
    /// Gets the access mask of the consuming stage.
    /// </summary>
    public AccessFlags DestinationAccessMask { get; }

    /// <summary>
    /// Gets the producing pipeline stage.
    /// </summary>
    public PipelineStageFlags SourceStageMask { get; }

    /// <summary>
    /// Gets the consuming pipeline stage.
    /// </summary>
    public PipelineStageFlags DestinationStageMask { get; }
}
