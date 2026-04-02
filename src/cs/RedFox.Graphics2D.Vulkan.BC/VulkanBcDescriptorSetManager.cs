using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Provides static helpers for Vulkan descriptor pool creation, descriptor set allocation, and descriptor set updates for BC encode and decode pipelines.
/// </summary>
public static unsafe class VulkanBcDescriptorSetManager
{
    /// <summary>
    /// Attempts to create a descriptor pool with the specified maximum set count and pool sizes.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="maxSets">The maximum number of descriptor sets that can be allocated from the pool.</param>
    /// <param name="poolSizes">The descriptor type counts that the pool should support.</param>
    /// <param name="descriptorPool">Receives the created descriptor pool on success.</param>
    /// <returns><see langword="true"/> when the pool was created; otherwise <see langword="false"/>.</returns>
    public static bool TryCreateDescriptorPool(VulkanBcContext context, uint maxSets, ReadOnlySpan<DescriptorPoolSize> poolSizes, out DescriptorPool descriptorPool)
    {
        fixed (DescriptorPoolSize* poolSizesPtr = poolSizes)
        {
            DescriptorPoolCreateInfo createInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = maxSets,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizesPtr,
            };

            return context.Vk.CreateDescriptorPool(context.Device, in createInfo, null, out descriptorPool) == Result.Success;
        }
    }

    /// <summary>
    /// Attempts to allocate a single descriptor set from the given pool using the specified layout.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="descriptorPool">The descriptor pool to allocate from.</param>
    /// <param name="descriptorSetLayout">The descriptor set layout to use for the allocation.</param>
    /// <param name="descriptorSet">Receives the allocated descriptor set on success.</param>
    /// <returns><see langword="true"/> when the descriptor set was allocated; otherwise <see langword="false"/>.</returns>
    public static bool TryAllocateDescriptorSet(VulkanBcContext context, DescriptorPool descriptorPool, DescriptorSetLayout descriptorSetLayout, out DescriptorSet descriptorSet)
    {
        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &descriptorSetLayout,
        };

        return context.Vk.AllocateDescriptorSets(context.Device, in allocateInfo, out descriptorSet) == Result.Success;
    }

    /// <summary>
    /// Updates a decode descriptor set with the source image view, output buffer, and constant buffer.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="descriptorSet">The descriptor set to update.</param>
    /// <param name="imageView">The sampled image view for the BC-compressed source.</param>
    /// <param name="outputBuffer">The storage buffer that receives decoded pixel data.</param>
    /// <param name="constantBuffer">The uniform buffer containing decode constants.</param>
    public static void UpdateDecodeDescriptorSet(VulkanBcContext context, DescriptorSet descriptorSet, ImageView imageView, VulkanBcBuffer outputBuffer, VulkanBcBuffer constantBuffer)
    {
        DescriptorImageInfo imageInfo = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = imageView,
        };

        DescriptorBufferInfo outputBufferInfo = new()
        {
            Buffer = outputBuffer.Handle,
            Offset = 0,
            Range = outputBuffer.Size,
        };

        DescriptorBufferInfo constantBufferInfo = new()
        {
            Buffer = constantBuffer.Handle,
            Offset = 0,
            Range = constantBuffer.Size,
        };

        WriteDescriptorSet* writes = stackalloc WriteDescriptorSet[3];

        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.SampledImage,
            PImageInfo = &imageInfo,
        };

        writes[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &outputBufferInfo,
        };

        writes[2] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 2,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            PBufferInfo = &constantBufferInfo,
        };

        context.Vk.UpdateDescriptorSets(context.Device, 3, writes, 0, null);
    }

    /// <summary>
    /// Updates an encode descriptor set with the source image view, input and output storage buffers, and constant buffer.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="descriptorSet">The descriptor set to update.</param>
    /// <param name="imageView">The sampled image view for the uncompressed source.</param>
    /// <param name="inputBuffer">The storage buffer providing input data for the current encode pass.</param>
    /// <param name="outputBuffer">The storage buffer that receives the encode pass output.</param>
    /// <param name="constantBuffer">The uniform buffer containing encode constants.</param>
    public static void UpdateEncodeDescriptorSet(VulkanBcContext context, DescriptorSet descriptorSet, ImageView imageView, VulkanBcBuffer inputBuffer, VulkanBcBuffer outputBuffer, VulkanBcBuffer constantBuffer)
    {
        DescriptorImageInfo imageInfo = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = imageView,
        };

        DescriptorBufferInfo inputBufferInfo = new()
        {
            Buffer = inputBuffer.Handle,
            Offset = 0,
            Range = inputBuffer.Size,
        };

        DescriptorBufferInfo outputBufferInfo = new()
        {
            Buffer = outputBuffer.Handle,
            Offset = 0,
            Range = outputBuffer.Size,
        };

        DescriptorBufferInfo constantBufferInfo = new()
        {
            Buffer = constantBuffer.Handle,
            Offset = 0,
            Range = constantBuffer.Size,
        };

        WriteDescriptorSet* writes = stackalloc WriteDescriptorSet[4];

        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.SampledImage,
            PImageInfo = &imageInfo,
        };

        writes[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &inputBufferInfo,
        };

        writes[2] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 2,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &outputBufferInfo,
        };

        writes[3] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 3,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            PBufferInfo = &constantBufferInfo,
        };

        context.Vk.UpdateDescriptorSets(context.Device, 4, writes, 0, null);
    }
}
