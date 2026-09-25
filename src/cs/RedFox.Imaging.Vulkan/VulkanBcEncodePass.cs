using Silk.NET.Vulkan;

namespace RedFox.Imaging.Vulkan;

/// <summary>
/// Groups the parameters for a single BC encode pass including pipeline, buffers, constants, and dispatch group count.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VulkanBcEncodePass"/> struct.
/// </remarks>
/// <param name="pipeline">The compute pipeline for this encode pass.</param>
/// <param name="inputBuffer">The input storage buffer.</param>
/// <param name="outputBuffer">The output storage buffer.</param>
/// <param name="constants">The encode constants for this pass.</param>
/// <param name="groupCount">The number of dispatch groups.</param>
public readonly struct VulkanBcEncodePass(VulkanBcComputePipeline pipeline, VulkanBcBuffer inputBuffer, VulkanBcBuffer outputBuffer, VulkanBcEncodeConstants constants, uint groupCount)
{
    /// <summary>
    /// Gets the compute pipeline for a encode pass.
    /// </summary>
    public VulkanBcComputePipeline Pipeline { get; } = pipeline;

    /// <summary>
    /// Gets the input storage buffer.
    /// </summary>
    public VulkanBcBuffer InputBuffer { get; } = inputBuffer;

    /// <summary>
    /// Gets the output storage buffer.
    /// </summary>
    public VulkanBcBuffer OutputBuffer { get; } = outputBuffer;

    /// <summary>
    /// Gets the encode constants for this pass.
    /// </summary>
    public VulkanBcEncodeConstants Constants { get; } = constants;

    /// <summary>
    /// Gets the number of dispatch groups.
    /// </summary>
    public uint GroupCount { get; } = groupCount;
}
