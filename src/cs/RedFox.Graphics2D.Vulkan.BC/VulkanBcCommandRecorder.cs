using Silk.NET.Vulkan;
using VulkanBufferHandle = Silk.NET.Vulkan.Buffer;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Provides static helpers for recording Vulkan pipeline barriers, image layout transitions, and buffer/image copy commands.
/// </summary>
public static unsafe class VulkanBcCommandRecorder
{
    /// <summary>
    /// Records an image layout transition as a pipeline barrier into the specified command buffer.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose API is used.</param>
    /// <param name="commandBuffer">The command buffer to record into.</param>
    /// <param name="image">The image whose layout should be transitioned.</param>
    /// <param name="transition">The layout transition parameters including access masks and pipeline stages.</param>
    public static void TransitionImageLayout(VulkanBcContext context, CommandBuffer commandBuffer, Silk.NET.Vulkan.Image image, in VulkanBcImageLayoutTransition transition)
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = transition.OldLayout,
            NewLayout = transition.NewLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SrcAccessMask = transition.SourceAccessMask,
            DstAccessMask = transition.DestinationAccessMask,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        context.Vk.CmdPipelineBarrier(commandBuffer, transition.SourceStageMask, transition.DestinationStageMask, 0, 0, null, 0, null, 1, in barrier);
    }

    /// <summary>
    /// Records a buffer-to-image copy command that uploads pixel data into a Vulkan image.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose API is used.</param>
    /// <param name="commandBuffer">The command buffer to record into.</param>
    /// <param name="buffer">The source buffer containing the pixel data.</param>
    /// <param name="image">The destination image.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    public static void CopyBufferToImage(VulkanBcContext context, CommandBuffer commandBuffer, VulkanBufferHandle buffer, Silk.NET.Vulkan.Image image, uint width, uint height)
    {
        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1),
        };

        context.Vk.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, in region);
    }

    /// <summary>
    /// Records a buffer-to-buffer copy command.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose API is used.</param>
    /// <param name="commandBuffer">The command buffer to record into.</param>
    /// <param name="source">The source buffer handle.</param>
    /// <param name="destination">The destination buffer handle.</param>
    /// <param name="size">The number of bytes to copy.</param>
    public static void CopyBuffer(VulkanBcContext context, CommandBuffer commandBuffer, VulkanBufferHandle source, VulkanBufferHandle destination, ulong size)
    {
        BufferCopy region = new() { Size = size };
        context.Vk.CmdCopyBuffer(commandBuffer, source, destination, 1, in region);
    }

    /// <summary>
    /// Records a buffer memory barrier using the parameters in the specified <see cref="VulkanBcBufferMemoryBarrier"/>.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose API is used.</param>
    /// <param name="commandBuffer">The command buffer to record into.</param>
    /// <param name="barrier">The barrier parameters including buffer, access masks, and pipeline stages.</param>
    public static void InsertBufferBarrier(VulkanBcContext context, CommandBuffer commandBuffer, in VulkanBcBufferMemoryBarrier barrier)
    {
        BufferMemoryBarrier vkBarrier = new()
        {
            SType = StructureType.BufferMemoryBarrier,
            SrcAccessMask = barrier.SourceAccessMask,
            DstAccessMask = barrier.DestinationAccessMask,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = barrier.Buffer.Handle,
            Offset = 0,
            Size = Vk.WholeSize,
        };

        context.Vk.CmdPipelineBarrier(commandBuffer, barrier.SourceStageMask, barrier.DestinationStageMask, 0, 0, null, 1, in vkBarrier, 0, null);
    }

    /// <summary>
    /// Records a buffer memory barrier with explicit access masks and pipeline stages.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose API is used.</param>
    /// <param name="commandBuffer">The command buffer to record into.</param>
    /// <param name="buffer">The buffer handle to insert the barrier for.</param>
    /// <param name="sourceAccessMask">The access mask of the producing stage.</param>
    /// <param name="destinationAccessMask">The access mask of the consuming stage.</param>
    /// <param name="sourceStageMask">The producing pipeline stage.</param>
    /// <param name="destinationStageMask">The consuming pipeline stage.</param>
    public static void InsertBufferBarrier(VulkanBcContext context, CommandBuffer commandBuffer, VulkanBufferHandle buffer, AccessFlags sourceAccessMask, AccessFlags destinationAccessMask, PipelineStageFlags sourceStageMask, PipelineStageFlags destinationStageMask)
    {
        BufferMemoryBarrier vkBarrier = new()
        {
            SType = StructureType.BufferMemoryBarrier,
            SrcAccessMask = sourceAccessMask,
            DstAccessMask = destinationAccessMask,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = buffer,
            Offset = 0,
            Size = Vk.WholeSize,
        };

        context.Vk.CmdPipelineBarrier(commandBuffer, sourceStageMask, destinationStageMask, 0, 0, null, 1, in vkBarrier, 0, null);
    }
}
