using Silk.NET.Vulkan;

namespace RedFox.Imaging.Vulkan;

/// <summary>
/// Describes a buffer memory barrier including access masks and pipeline stage transitions.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcBufferMemoryBarrier"/> struct.
/// </remarks>
/// <param name="buffer">The buffer to insert the barrier for.</param>
/// <param name="sourceAccessMask">The access mask of the producing stage.</param>
/// <param name="destinationAccessMask">The access mask of the consuming stage.</param>
/// <param name="sourceStageMask">The producing pipeline stage.</param>
/// <param name="destinationStageMask">The consuming pipeline stage.</param>
public readonly struct VulkanBcBufferMemoryBarrier(VulkanBcBuffer buffer, AccessFlags sourceAccessMask, AccessFlags destinationAccessMask, PipelineStageFlags sourceStageMask, PipelineStageFlags destinationStageMask)
{
    /// <summary>
    /// Gets the buffer to insert the barrier for.
    /// </summary>
    public VulkanBcBuffer Buffer { get; } = buffer;

    /// <summary>
    /// Gets the access mask of the producing stage.
    /// </summary>
    public AccessFlags SourceAccessMask { get; } = sourceAccessMask;

    /// <summary>
    /// Gets the access mask of the consuming stage.
    /// </summary>
    public AccessFlags DestinationAccessMask { get; } = destinationAccessMask;

    /// <summary>
    /// Gets the producing pipeline stage.
    /// </summary>
    public PipelineStageFlags SourceStageMask { get; } = sourceStageMask;

    /// <summary>
    /// Gets the consuming pipeline stage.
    /// </summary>
    public PipelineStageFlags DestinationStageMask { get; } = destinationStageMask;
}
