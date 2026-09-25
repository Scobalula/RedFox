using Silk.NET.Vulkan;

namespace RedFox.Imaging.Vulkan;

/// <summary>
/// Describes a Vulkan image layout transition including the access masks and pipeline stage barriers.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcImageLayoutTransition"/> struct.
/// </remarks>
/// <param name="oldLayout">The current image layout before the transition.</param>
/// <param name="newLayout">The target image layout after the transition.</param>
/// <param name="sourceAccessMask">The access flags for the source stage of the transition.</param>
/// <param name="destinationAccessMask">The access flags for the destination stage of the transition.</param>
/// <param name="sourceStageMask">The pipeline stage that produces the source access.</param>
/// <param name="destinationStageMask">The pipeline stage that consumes the destination access.</param>
public readonly struct VulkanBcImageLayoutTransition(ImageLayout oldLayout, ImageLayout newLayout, AccessFlags sourceAccessMask, AccessFlags destinationAccessMask, PipelineStageFlags sourceStageMask, PipelineStageFlags destinationStageMask)
{

    /// <summary>
    /// Gets the current image layout before the transition.
    /// </summary>
    public ImageLayout OldLayout { get; } = oldLayout;

    /// <summary>
    /// Gets the target image layout after the transition.
    /// </summary>
    public ImageLayout NewLayout { get; } = newLayout;

    /// <summary>
    /// Gets the access flags for the source stage of the transition.
    /// </summary>
    public AccessFlags SourceAccessMask { get; } = sourceAccessMask;

    /// <summary>
    /// Gets the access flags for the destination stage of the transition.
    /// </summary>
    public AccessFlags DestinationAccessMask { get; } = destinationAccessMask;

    /// <summary>
    /// Gets the pipeline stage that produces the source access.
    /// </summary>
    public PipelineStageFlags SourceStageMask { get; } = sourceStageMask;

    /// <summary>
    /// Gets the pipeline stage that consumes the destination access.
    /// </summary>
    public PipelineStageFlags DestinationStageMask { get; } = destinationStageMask;
}
