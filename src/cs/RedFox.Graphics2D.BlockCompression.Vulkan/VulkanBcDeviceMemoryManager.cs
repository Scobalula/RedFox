using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Provides static helpers for Vulkan device memory type selection, mapped memory flush/invalidate, and alignment calculations.
/// </summary>
public static unsafe class VulkanBcDeviceMemoryManager
{
    /// <summary>
    /// Attempts to find a memory type that satisfies the required and preferred property flags.
    /// The best match is scored by counting how many preferred flags are present and penalising extra flags.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose physical device is queried.</param>
    /// <param name="memoryTypeBits">The bitmask of acceptable memory types from the resource's memory requirements.</param>
    /// <param name="requiredProperties">Memory property flags that must all be present.</param>
    /// <param name="preferredProperties">Memory property flags that are preferred but not required.</param>
    /// <param name="memoryTypeIndex">Receives the index of the selected memory type.</param>
    /// <param name="selectedProperties">Receives the actual property flags of the selected memory type.</param>
    /// <returns><see langword="true"/> when a compatible memory type was found; otherwise <see langword="false"/>.</returns>
    public static bool TryFindMemoryType(VulkanBcContext context, uint memoryTypeBits, MemoryPropertyFlags requiredProperties, MemoryPropertyFlags preferredProperties, out uint memoryTypeIndex, out MemoryPropertyFlags selectedProperties)
    {
        context.Vk.GetPhysicalDeviceMemoryProperties(context.PhysicalDevice, out PhysicalDeviceMemoryProperties memoryProperties);
        int bestScore = int.MinValue;
        bool found = false;
        memoryTypeIndex = 0;
        selectedProperties = 0;

        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++)
        {
            MemoryPropertyFlags candidateProperties = memoryProperties.MemoryTypes[(int)i].PropertyFlags;
            bool isCompatible = (memoryTypeBits & (1u << (int)i)) != 0;
            bool hasRequiredFlags = (candidateProperties & requiredProperties) == requiredProperties;

            if (!isCompatible || !hasRequiredFlags)
            {
                continue;
            }

            int preferredScore = CountFlags(candidateProperties & preferredProperties);
            int penalty = CountFlags(candidateProperties & ~(requiredProperties | preferredProperties));
            int score = (preferredScore * 8) - penalty;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                memoryTypeIndex = i;
                selectedProperties = candidateProperties;
            }
        }

        return found;
    }

    /// <summary>
    /// Flushes a range of mapped memory so that writes performed by the CPU are visible to the GPU.
    /// This is a no-op for host-coherent memory.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="buffer">The buffer whose mapped memory should be flushed.</param>
    /// <param name="offset">The start offset in bytes within the mapped range.</param>
    /// <param name="size">The number of bytes to flush.</param>
    /// <returns><see langword="true"/> when the flush succeeded or was unnecessary; otherwise <see langword="false"/>.</returns>
    public static bool FlushMappedMemory(VulkanBcContext context, VulkanBcBuffer buffer, ulong offset, ulong size)
    {
        if ((buffer.MemoryPropertyFlags & MemoryPropertyFlags.HostCoherentBit) != 0)
        {
            return true;
        }

        MappedMemoryRange mappedMemoryRange = new()
        {
            SType = StructureType.MappedMemoryRange,
            Memory = buffer.Memory,
            Offset = AlignDown(offset, context.NonCoherentAtomSize),
            Size = AlignMappedRangeSize(context.NonCoherentAtomSize, offset, size),
        };

        return context.Vk.FlushMappedMemoryRanges(context.Device, 1, in mappedMemoryRange) == Result.Success;
    }

    /// <summary>
    /// Invalidates a range of mapped memory so that writes performed by the GPU are visible to the CPU.
    /// This is a no-op for host-coherent memory.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="buffer">The buffer whose mapped memory should be invalidated.</param>
    /// <param name="offset">The start offset in bytes within the mapped range.</param>
    /// <param name="size">The number of bytes to invalidate.</param>
    /// <returns><see langword="true"/> when the invalidation succeeded or was unnecessary; otherwise <see langword="false"/>.</returns>
    public static bool InvalidateMappedMemory(VulkanBcContext context, VulkanBcBuffer buffer, ulong offset, ulong size)
    {
        if ((buffer.MemoryPropertyFlags & MemoryPropertyFlags.HostCoherentBit) != 0)
        {
            return true;
        }

        MappedMemoryRange mappedMemoryRange = new()
        {
            SType = StructureType.MappedMemoryRange,
            Memory = buffer.Memory,
            Offset = AlignDown(offset, context.NonCoherentAtomSize),
            Size = AlignMappedRangeSize(context.NonCoherentAtomSize, offset, size),
        };

        return context.Vk.InvalidateMappedMemoryRanges(context.Device, 1, in mappedMemoryRange) == Result.Success;
    }

    /// <summary>
    /// Aligns a mapped memory range size to the non-coherent atom size.
    /// </summary>
    /// <param name="nonCoherentAtomSize">The non-coherent atom size from the physical device limits.</param>
    /// <param name="offset">The start offset of the range.</param>
    /// <param name="size">The size of the range in bytes.</param>
    /// <returns>The aligned size, guaranteed to be at least one atom.</returns>
    public static ulong AlignMappedRangeSize(ulong nonCoherentAtomSize, ulong offset, ulong size)
    {
        ulong alignedOffset = AlignDown(offset, nonCoherentAtomSize);
        ulong alignedEnd = AlignUp(offset + size, nonCoherentAtomSize);
        ulong alignedSize = alignedEnd - alignedOffset;
        return alignedSize == 0 ? nonCoherentAtomSize : alignedSize;
    }

    /// <summary>
    /// Aligns a value down to the nearest multiple of the specified alignment.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The alignment granularity.</param>
    /// <returns>The largest multiple of <paramref name="alignment"/> that is less than or equal to <paramref name="value"/>.</returns>
    public static ulong AlignDown(ulong value, ulong alignment)
    {
        return value - (value % alignment);
    }

    /// <summary>
    /// Aligns a value up to the nearest multiple of the specified alignment.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The alignment granularity.</param>
    /// <returns>The smallest multiple of <paramref name="alignment"/> that is greater than or equal to <paramref name="value"/>.</returns>
    public static ulong AlignUp(ulong value, ulong alignment)
    {
        ulong remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }

    /// <summary>
    /// Counts the number of individual flags set in a <see cref="MemoryPropertyFlags"/> value.
    /// </summary>
    /// <param name="flags">The flags to count.</param>
    /// <returns>The population count (number of set bits) of the flags value.</returns>
    public static int CountFlags(MemoryPropertyFlags flags)
    {
        int count = 0;
        uint value = (uint)flags;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }
}
