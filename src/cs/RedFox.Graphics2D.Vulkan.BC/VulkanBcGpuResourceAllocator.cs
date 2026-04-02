using Silk.NET.Vulkan;
using System.Runtime.InteropServices;
using VulkanBufferHandle = Silk.NET.Vulkan.Buffer;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Provides static helpers for allocating Vulkan buffers and images, and for reading/writing host-visible memory.
/// </summary>
public static unsafe class VulkanBcGpuResourceAllocator
{
    /// <summary>
    /// Attempts to create a Vulkan buffer described by the specified allocation parameters.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device and memory manager are used.</param>
    /// <param name="allocation">The buffer allocation parameters.</param>
    /// <param name="buffer">Receives the created buffer on success.</param>
    /// <returns><see langword="true"/> when the buffer was created; otherwise <see langword="false"/>.</returns>
    public static bool TryCreateBuffer(VulkanBcContext context, in VulkanBcBufferAllocation allocation, out VulkanBcBuffer buffer)
    {
        return TryCreateBuffer(context, allocation.Size, allocation.Usage, allocation.RequiredProperties, allocation.PreferredProperties, out buffer);
    }

    /// <summary>
    /// Attempts to create a Vulkan buffer with the specified usage and memory properties.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device and memory manager are used.</param>
    /// <param name="size">The buffer size in bytes.</param>
    /// <param name="usage">The buffer usage flags.</param>
    /// <param name="requiredProperties">Memory property flags that must all be present.</param>
    /// <param name="preferredProperties">Memory property flags that are preferred but not required.</param>
    /// <param name="buffer">Receives the created buffer on success.</param>
    /// <returns><see langword="true"/> when the buffer was created; otherwise <see langword="false"/>.</returns>
    public static bool TryCreateBuffer(VulkanBcContext context, ulong size, BufferUsageFlags usage, MemoryPropertyFlags requiredProperties, MemoryPropertyFlags preferredProperties, out VulkanBcBuffer buffer)
    {
        buffer = default;

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        if (context.Vk.CreateBuffer(context.Device, in createInfo, null, out VulkanBufferHandle handle) != Result.Success)
            return false;

        context.Vk.GetBufferMemoryRequirements(context.Device, handle, out MemoryRequirements memoryRequirements);

        if (!VulkanBcDeviceMemoryManager.TryFindMemoryType(context, memoryRequirements.MemoryTypeBits, requiredProperties, preferredProperties, out uint memoryTypeIndex, out MemoryPropertyFlags selectedProperties))
        {
            context.Vk.DestroyBuffer(context.Device, handle, null);
            return false;
        }

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memoryRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        if (context.Vk.AllocateMemory(context.Device, in allocateInfo, null, out DeviceMemory memory) != Result.Success)
        {
            context.Vk.DestroyBuffer(context.Device, handle, null);
            return false;
        }

        if (context.Vk.BindBufferMemory(context.Device, handle, memory, 0) != Result.Success)
        {
            context.Vk.FreeMemory(context.Device, memory, null);
            context.Vk.DestroyBuffer(context.Device, handle, null);
            return false;
        }

        buffer = new VulkanBcBuffer(handle, memory, size, selectedProperties);
        return true;
    }

    /// <summary>
    /// Attempts to create a Vulkan image described by the specified allocation parameters.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device and memory manager are used.</param>
    /// <param name="allocation">The image allocation parameters.</param>
    /// <param name="image">Receives the created image on success.</param>
    /// <returns><see langword="true"/> when the image was created; otherwise <see langword="false"/>.</returns>
    public static bool TryCreateImage(VulkanBcContext context, in VulkanBcImageAllocation allocation, out VulkanBcImage image)
    {
        return TryCreateImage(context, allocation.Format, allocation.Width, allocation.Height, allocation.Usage, out image);
    }

    /// <summary>
    /// Attempts to create a Vulkan image with an associated image view and device memory allocation.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device and memory manager are used.</param>
    /// <param name="format">The image pixel format.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="usage">The image usage flags.</param>
    /// <param name="image">Receives the created image on success.</param>
    /// <returns><see langword="true"/> when the image was created; otherwise <see langword="false"/>.</returns>
    public static bool TryCreateImage(VulkanBcContext context, Format format, uint width, uint height, ImageUsageFlags usage, out VulkanBcImage image)
    {
        image = default;

        ImageCreateInfo createInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        if (context.Vk.CreateImage(context.Device, in createInfo, null, out Silk.NET.Vulkan.Image handle) != Result.Success)
            return false;

        context.Vk.GetImageMemoryRequirements(context.Device, handle, out MemoryRequirements memoryRequirements);

        if (!VulkanBcDeviceMemoryManager.TryFindMemoryType(context, memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit, MemoryPropertyFlags.DeviceLocalBit, out uint memoryTypeIndex, out _))
        {
            context.Vk.DestroyImage(context.Device, handle, null);
            return false;
        }

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memoryRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        if (context.Vk.AllocateMemory(context.Device, in allocateInfo, null, out DeviceMemory memory) != Result.Success)
        {
            context.Vk.DestroyImage(context.Device, handle, null);
            return false;
        }

        if (context.Vk.BindImageMemory(context.Device, handle, memory, 0) != Result.Success)
        {
            context.Vk.FreeMemory(context.Device, memory, null);
            context.Vk.DestroyImage(context.Device, handle, null);
            return false;
        }

        ImageViewCreateInfo viewCreateInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = handle,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        if (context.Vk.CreateImageView(context.Device, in viewCreateInfo, null, out ImageView imageView) != Result.Success)
        {
            context.Vk.FreeMemory(context.Device, memory, null);
            context.Vk.DestroyImage(context.Device, handle, null);
            return false;
        }

        image = new VulkanBcImage(handle, memory, imageView);
        return true;
    }

    /// <summary>
    /// Writes a blittable value to a host-visible buffer.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the value to write.</typeparam>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="value">The value to write.</param>
    /// <returns><see langword="true"/> when the write succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryWriteBuffer<T>(VulkanBcContext context, VulkanBcBuffer buffer, T value) where T : unmanaged
    {
        ReadOnlySpan<T> valueSpan = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
        return TryWriteBuffer(context, buffer, MemoryMarshal.AsBytes(valueSpan));
    }

    /// <summary>
    /// Writes raw bytes into a host-visible buffer via mapped memory.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="data">The data to write.</param>
    /// <returns><see langword="true"/> when the write succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryWriteBuffer(VulkanBcContext context, VulkanBcBuffer buffer, ReadOnlySpan<byte> data)
    {
        void* mappedMemory = null;

        try
        {
            if (context.Vk.MapMemory(context.Device, buffer.Memory, 0, buffer.Size, 0, &mappedMemory) != Result.Success)
                return false;

            data.CopyTo(new Span<byte>(mappedMemory, data.Length));

            if (!VulkanBcDeviceMemoryManager.FlushMappedMemory(context, buffer, 0, (ulong)data.Length))
                return false;

            return true;
        }
        finally
        {
            if (mappedMemory is not null)
                context.Vk.UnmapMemory(context.Device, buffer.Memory);
        }
    }

    /// <summary>
    /// Reads raw bytes from a host-visible buffer via mapped memory.
    /// </summary>
    /// <param name="context">The Vulkan BC context whose device is used.</param>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="destination">The destination span to receive the data.</param>
    /// <returns><see langword="true"/> when the read succeeded or the destination is empty; otherwise <see langword="false"/>.</returns>
    public static bool TryReadBuffer(VulkanBcContext context, VulkanBcBuffer buffer, Span<byte> destination)
    {
        if (destination.IsEmpty)
            return true;

        void* mappedMemory = null;

        try
        {
            if (context.Vk.MapMemory(context.Device, buffer.Memory, 0, buffer.Size, 0, &mappedMemory) != Result.Success)
                return false;

            if (!VulkanBcDeviceMemoryManager.InvalidateMappedMemory(context, buffer, 0, (ulong)destination.Length))
                return false;

            fixed (byte* destinationPtr = destination)
            {
                System.Buffer.MemoryCopy(mappedMemory, destinationPtr, destination.Length, destination.Length);
            }

            return true;
        }
        finally
        {
            if (mappedMemory is not null)
                context.Vk.UnmapMemory(context.Device, buffer.Memory);
        }
    }
}
