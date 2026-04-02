using Silk.NET.Vulkan;
using VulkanBufferHandle = Silk.NET.Vulkan.Buffer;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Groups the parameters for a single BC encode pass including pipeline, buffers, constants, and dispatch group count.
/// </summary>
public readonly struct VulkanBcEncodePass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcEncodePass"/> struct.
    /// </summary>
    /// <param name="pipeline">The compute pipeline for this encode pass.</param>
    /// <param name="inputBuffer">The input storage buffer.</param>
    /// <param name="outputBuffer">The output storage buffer.</param>
    /// <param name="constants">The encode constants for this pass.</param>
    /// <param name="groupCount">The number of dispatch groups.</param>
    public VulkanBcEncodePass(VulkanBcComputePipeline pipeline, VulkanBcBuffer inputBuffer, VulkanBcBuffer outputBuffer, VulkanBcEncodeConstants constants, uint groupCount)
    {
        Pipeline = pipeline;
        InputBuffer = inputBuffer;
        OutputBuffer = outputBuffer;
        Constants = constants;
        GroupCount = groupCount;
    }

    /// <summary>
    /// Gets the compute pipeline for a encode pass.
    /// </summary>
    public VulkanBcComputePipeline Pipeline { get; }

    /// <summary>
    /// Gets the input storage buffer.
    /// </summary>
    public VulkanBcBuffer InputBuffer { get; }

    /// <summary>
    /// Gets the output storage buffer.
    /// </summary>
    public VulkanBcBuffer OutputBuffer { get; }

    /// <summary>
    /// Gets the encode constants for this pass.
    /// </summary>
    public VulkanBcEncodeConstants Constants { get; }

    /// <summary>
    /// Gets the number of dispatch groups.
    /// </summary>
    public uint GroupCount { get; }
}
