using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Provides safe destruction helpers for Vulkan objects allocated by the BC converter engine.
/// Each method checks for default (zero) handles before calling the underlying Vulkan destroy routine.
/// </summary>
public static unsafe class VulkanBcDestroyer
{
    /// <summary>
    /// Destroys a compute pipeline and its associated layout and descriptor set layout.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the pipeline.</param>
    /// <param name="pipeline">The pipeline to destroy.</param>
    public static void DestroyPipeline(VulkanBcContext context, VulkanBcComputePipeline pipeline)
    {
        if (pipeline.Pipeline.Handle != 0)
            context.Vk.DestroyPipeline(context.Device, pipeline.Pipeline, null);
        if (pipeline.PipelineLayout.Handle != 0)
            context.Vk.DestroyPipelineLayout(context.Device, pipeline.PipelineLayout, null);
        if (pipeline.DescriptorSetLayout.Handle != 0)
            context.Vk.DestroyDescriptorSetLayout(context.Device, pipeline.DescriptorSetLayout, null);
    }

    /// <summary>
    /// Destroys a Vulkan buffer and frees its backing device memory.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the buffer.</param>
    /// <param name="buffer">The buffer to destroy.</param>
    public static void DestroyBuffer(VulkanBcContext context, VulkanBcBuffer buffer)
    {
        if (buffer.Handle.Handle != 0)
            context.Vk.DestroyBuffer(context.Device, buffer.Handle, null);
        if (buffer.Memory.Handle != 0)
            context.Vk.FreeMemory(context.Device, buffer.Memory, null);
    }

    /// <summary>
    /// Destroys a Vulkan image, its image view, and frees its backing device memory.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the image.</param>
    /// <param name="image">The image to destroy.</param>
    public static void DestroyImage(VulkanBcContext context, VulkanBcImage image)
    {
        if (image.View.Handle != 0)
            context.Vk.DestroyImageView(context.Device, image.View, null);
        if (image.Handle.Handle != 0)
            context.Vk.DestroyImage(context.Device, image.Handle, null);
        if (image.Memory.Handle != 0)
            context.Vk.FreeMemory(context.Device, image.Memory, null);
    }

    /// <summary>
    /// Destroys a Vulkan descriptor pool.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the pool.</param>
    /// <param name="descriptorPool">The descriptor pool to destroy.</param>
    public static void DestroyDescriptorPool(VulkanBcContext context, DescriptorPool descriptorPool)
    {
        if (descriptorPool.Handle != 0)
            context.Vk.DestroyDescriptorPool(context.Device, descriptorPool, null);
    }

    /// <summary>
    /// Destroys a Vulkan fence.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the fence.</param>
    /// <param name="fence">The fence to destroy.</param>
    public static void DestroyFence(VulkanBcContext context, Fence fence)
    {
        if (fence.Handle != 0)
            context.Vk.DestroyFence(context.Device, fence, null);
    }

    /// <summary>
    /// Frees a Vulkan command buffer back to its command pool.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the command buffer.</param>
    /// <param name="commandBuffer">The command buffer to free.</param>
    public static void FreeCommandBuffer(VulkanBcContext context, CommandBuffer commandBuffer)
    {
        if (commandBuffer.Handle != 0)
            context.Vk.FreeCommandBuffers(context.Device, context.CommandPool, 1, in commandBuffer);
    }
}
