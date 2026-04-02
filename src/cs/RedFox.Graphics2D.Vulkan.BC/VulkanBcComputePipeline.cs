using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Stores a compute pipeline together with the layout objects it depends on.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcComputePipeline"/> struct.
/// </remarks>
/// <param name="descriptorSetLayout">The descriptor set layout used by the pipeline.</param>
/// <param name="pipelineLayout">The pipeline layout used by the pipeline.</param>
/// <param name="pipeline">The Vulkan compute pipeline handle.</param>
public readonly struct VulkanBcComputePipeline(DescriptorSetLayout descriptorSetLayout, PipelineLayout pipelineLayout, Pipeline pipeline)
{
    /// <summary>
    /// Gets the descriptor set layout used by the pipeline.
    /// </summary>
    public DescriptorSetLayout DescriptorSetLayout { get; } = descriptorSetLayout;

    /// <summary>
    /// Gets the pipeline layout used by the pipeline.
    /// </summary>
    public PipelineLayout PipelineLayout { get; } = pipelineLayout;

    /// <summary>
    /// Gets the Vulkan compute pipeline handle.
    /// </summary>
    public Pipeline Pipeline { get; } = pipeline;
}
