using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Describes a Vulkan image layout transition including the access masks and pipeline stage barriers.
/// </summary>
public readonly struct VulkanBcImageLayoutTransition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcImageLayoutTransition"/> struct.
    /// </summary>
    /// <param name="oldLayout">The current image layout before the transition.</param>
    /// <param name="newLayout">The target image layout after the transition.</param>
    /// <param name="sourceAccessMask">The access flags for the source stage of the transition.</param>
    /// <param name="destinationAccessMask">The access flags for the destination stage of the transition.</param>
    /// <param name="sourceStageMask">The pipeline stage that produces the source access.</param>
    /// <param name="destinationStageMask">The pipeline stage that consumes the destination access.</param>
    public VulkanBcImageLayoutTransition(
        ImageLayout oldLayout,
        ImageLayout newLayout,
        AccessFlags sourceAccessMask,
        AccessFlags destinationAccessMask,
        PipelineStageFlags sourceStageMask,
        PipelineStageFlags destinationStageMask)
    {
        OldLayout = oldLayout;
        NewLayout = newLayout;
        SourceAccessMask = sourceAccessMask;
        DestinationAccessMask = destinationAccessMask;
        SourceStageMask = sourceStageMask;
        DestinationStageMask = destinationStageMask;
    }

    /// <summary>
    /// Gets the current image layout before the transition.
    /// </summary>
    public ImageLayout OldLayout { get; }

    /// <summary>
    /// Gets the target image layout after the transition.
    /// </summary>
    public ImageLayout NewLayout { get; }

    /// <summary>
    /// Gets the access flags for the source stage of the transition.
    /// </summary>
    public AccessFlags SourceAccessMask { get; }

    /// <summary>
    /// Gets the access flags for the destination stage of the transition.
    /// </summary>
    public AccessFlags DestinationAccessMask { get; }

    /// <summary>
    /// Gets the pipeline stage that produces the source access.
    /// </summary>
    public PipelineStageFlags SourceStageMask { get; }

    /// <summary>
    /// Gets the pipeline stage that consumes the destination access.
    /// </summary>
    public PipelineStageFlags DestinationStageMask { get; }
}
