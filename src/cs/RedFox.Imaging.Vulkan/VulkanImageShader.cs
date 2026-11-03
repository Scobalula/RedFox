using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Imaging.Primitives;
using Silk.NET.Vulkan;

namespace RedFox.Imaging.Vulkan;

/// <summary>
/// A compute shader that reads an image as stored and writes one or more RGBA8 images from it.
/// </summary>
/// <remarks>
/// The entry point is <c>ProcessMain</c> with 8x8 thread groups. Binding 0 is the source texture, binding 1 the
/// <see cref="VulkanImageShaderConstants"/>, and bindings 2 onwards a packed RGBA8 <c>uint</c> buffer per output.
/// </remarks>
public sealed unsafe class VulkanImageShader : IDisposable
{
    private const uint ThreadGroupSize = 8;

    private static readonly VulkanBcImageLayoutTransition UploadTransition		= new(ImageLayout.Undefined, ImageLayout.TransferDstOptimal, AccessFlags.None, AccessFlags.TransferWriteBit, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);
    private static readonly VulkanBcImageLayoutTransition ShaderReadTransition	= new(ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit, PipelineStageFlags.TransferBit, PipelineStageFlags.ComputeShaderBit);

    private readonly VulkanBcContext _context;
    private readonly VulkanBcComputePipeline _pipeline;

    /// <summary>
    /// Gets the number of images the shader writes.
    /// </summary>
    public int OutputCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanImageShader"/> class.
    /// </summary>
    /// <param name="context">The Vulkan context the shader runs on.</param>
    /// <param name="spirv">The compiled SPIR-V of the shader.</param>
    /// <param name="outputCount">The number of images the shader writes.</param>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline could not be created.</exception>
    public VulkanImageShader(VulkanBcContext context, byte[] spirv, int outputCount)
    {
        DescriptorSetLayoutBinding[] bindings = new DescriptorSetLayoutBinding[outputCount + 2];

        bindings[0] = new(0, DescriptorType.SampledImage, 1, ShaderStageFlags.ComputeBit, null);
        bindings[1] = new(1, DescriptorType.UniformBuffer, 1, ShaderStageFlags.ComputeBit, null);

        for (int i = 0; i < outputCount; i++)
        {
            bindings[i + 2] = new((uint)i + 2, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, null);
        }

        if (!context.TryCreateComputePipeline(spirv, "ProcessMain", bindings, out _pipeline))
            throw new InvalidOperationException("The image shader's pipeline could not be created.");

        _context	= context;
        OutputCount	= outputCount;
    }

    /// <summary>
    /// Runs the shader over the image's first mip.
    /// </summary>
    /// <param name="input">The image to read, in its stored format.</param>
    /// <param name="parameters">The shader specific parameters.</param>
    /// <param name="flags">The shader specific flags.</param>
    /// <returns>The images the shader wrote, as R8G8B8A8 with the size of the first mip.</returns>
    /// <exception cref="NotSupportedException">Thrown when the image's format can't be sampled by the device.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the shader could not be run.</exception>
    public Image[] Run(Image input, Vector4 parameters, uint flags)
    {
        ImageSlice slice = input.GetSlice(0);

        if (!TryGetSourceFormat(slice.Format, out Format sourceFormat) || !_context.SupportsSampledImage(sourceFormat))
            throw new NotSupportedException($"Images of format {slice.Format} can't be read by the image shader.");

        int pixelCount = slice.Width * slice.Height;
        VulkanBcBuffer uploadBuffer = default;
        VulkanBcBuffer constantBuffer = default;
        VulkanBcBuffer[] outputBuffers = new VulkanBcBuffer[OutputCount];
        VulkanBcBuffer[] readbackBuffers = new VulkanBcBuffer[OutputCount];
        VulkanBcImage sourceImage = default;
        DescriptorPool descriptorPool = default;
        CommandBuffer commandBuffer = default;
        Fence fence = default;

        try
        {
            VulkanBcBufferAllocation uploadAlloc	= new((ulong)slice.Pixels.Length, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCoherentBit);
            VulkanBcBufferAllocation constantAlloc	= new((ulong)Marshal.SizeOf<VulkanImageShaderConstants>(), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCoherentBit);
            VulkanBcBufferAllocation outputAlloc	= new((ulong)pixelCount * 4, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostCoherentBit, MemoryPropertyFlags.HostCoherentBit);
            VulkanBcBufferAllocation readbackAlloc	= new((ulong)pixelCount * 4, BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCachedBit | MemoryPropertyFlags.HostCoherentBit);
            VulkanBcImageAllocation imageAlloc		= new(sourceFormat, (uint)slice.Width, (uint)slice.Height, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit);

            VulkanImageShaderConstants constants = new()
            {
                Width = (uint)slice.Width,
                Height = (uint)slice.Height,
                Flags = flags,
                Parameters = parameters,
            };

            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(_context, in uploadAlloc, out uploadBuffer) || !VulkanBcGpuResourceAllocator.TryWriteBuffer(_context, uploadBuffer, slice.PixelSpan))
                throw new InvalidOperationException("The image could not be uploaded.");
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(_context, in constantAlloc, out constantBuffer) || !VulkanBcGpuResourceAllocator.TryWriteBuffer(_context, constantBuffer, constants))
                throw new InvalidOperationException("The image shader's constants could not be uploaded.");
            if (!VulkanBcGpuResourceAllocator.TryCreateImage(_context, in imageAlloc, out sourceImage))
                throw new InvalidOperationException("The image shader's source image could not be created.");

            for (int i = 0; i < OutputCount; i++)
            {
                if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(_context, in outputAlloc, out outputBuffers[i]) || !VulkanBcGpuResourceAllocator.TryCreateBuffer(_context, in readbackAlloc, out readbackBuffers[i]))
                    throw new InvalidOperationException("The image shader's output buffers could not be created.");
            }

            DescriptorPoolSize[] poolSizes =
            [
                new(DescriptorType.SampledImage, 1),
                new(DescriptorType.UniformBuffer, 1),
                new(DescriptorType.StorageBuffer, (uint)OutputCount),
            ];

            if (!VulkanBcDescriptorSetManager.TryCreateDescriptorPool(_context, 1, poolSizes, out descriptorPool) || !VulkanBcDescriptorSetManager.TryAllocateDescriptorSet(_context, descriptorPool, _pipeline.DescriptorSetLayout, out DescriptorSet descriptorSet))
                throw new InvalidOperationException("The image shader's descriptor set could not be created.");
            if (!_context.TryAllocateCommandBuffer(out commandBuffer) || !_context.TryCreateFence(out fence))
                throw new InvalidOperationException("The image shader's command buffer could not be created.");

            VulkanBcDescriptorSetManager.UpdateImageShaderDescriptorSet(_context, descriptorSet, sourceImage.View, constantBuffer, outputBuffers);

            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            if (_context.Vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
                throw new InvalidOperationException("The image shader's command buffer could not be started.");

            RecordCommands(commandBuffer, uploadBuffer, sourceImage, outputBuffers, readbackBuffers, descriptorSet, (uint)slice.Width, (uint)slice.Height);

            if (_context.Vk.EndCommandBuffer(commandBuffer) != Result.Success || !_context.TrySubmitAndWait(commandBuffer, fence))
                throw new InvalidOperationException("The image shader could not be run.");

            Image[] outputs = new Image[OutputCount];

            for (int i = 0; i < OutputCount; i++)
            {
                byte[] pixels = new byte[pixelCount * 4];

                if (!VulkanBcGpuResourceAllocator.TryReadBuffer(_context, readbackBuffers[i], pixels))
                    throw new InvalidOperationException("The image shader's output could not be read back.");

                outputs[i] = new Image(slice.Width, slice.Height, ImageFormat.R8G8B8A8Unorm, pixels);
            }

            return outputs;
        }
        finally
        {
            VulkanBcDestroyer.DestroyFence(_context, fence);
            VulkanBcDestroyer.FreeCommandBuffer(_context, commandBuffer);
            VulkanBcDestroyer.DestroyDescriptorPool(_context, descriptorPool);
            VulkanBcDestroyer.DestroyImage(_context, sourceImage);
            VulkanBcDestroyer.DestroyBuffer(_context, constantBuffer);
            VulkanBcDestroyer.DestroyBuffer(_context, uploadBuffer);

            for (int i = 0; i < OutputCount; i++)
            {
                VulkanBcDestroyer.DestroyBuffer(_context, readbackBuffers[i]);
                VulkanBcDestroyer.DestroyBuffer(_context, outputBuffers[i]);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        VulkanBcDestroyer.DestroyPipeline(_context, _pipeline);
    }

    private void RecordCommands(CommandBuffer commandBuffer, VulkanBcBuffer uploadBuffer, VulkanBcImage sourceImage, VulkanBcBuffer[] outputBuffers, VulkanBcBuffer[] readbackBuffers, DescriptorSet descriptorSet, uint width, uint height)
    {
        VulkanBcCommandRecorder.TransitionImageLayout(_context, commandBuffer, sourceImage.Handle, in UploadTransition);
        VulkanBcCommandRecorder.CopyBufferToImage(_context, commandBuffer, uploadBuffer.Handle, sourceImage.Handle, width, height);
        VulkanBcCommandRecorder.TransitionImageLayout(_context, commandBuffer, sourceImage.Handle, in ShaderReadTransition);

        _context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _pipeline.Pipeline);
        _context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _pipeline.PipelineLayout, 0, 1, in descriptorSet, 0, null);
        _context.Vk.CmdDispatch(commandBuffer, (width + ThreadGroupSize - 1) / ThreadGroupSize, (height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        for (int i = 0; i < outputBuffers.Length; i++)
        {
            VulkanBcCommandRecorder.InsertBufferBarrier(_context, commandBuffer, outputBuffers[i].Handle, AccessFlags.ShaderWriteBit, AccessFlags.TransferReadBit, PipelineStageFlags.ComputeShaderBit, PipelineStageFlags.TransferBit);
            VulkanBcCommandRecorder.CopyBuffer(_context, commandBuffer, outputBuffers[i].Handle, readbackBuffers[i].Handle, outputBuffers[i].Size);
            VulkanBcCommandRecorder.InsertBufferBarrier(_context, commandBuffer, readbackBuffers[i].Handle, AccessFlags.TransferWriteBit, AccessFlags.HostReadBit, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit);
        }
    }

    private static bool TryGetSourceFormat(ImageFormat format, out Format vulkanFormat)
    {
        if (VulkanBcFormatMap.TryGetDecodeSourceFormat(format, out vulkanFormat))
            return true;

        vulkanFormat = format switch
        {
            ImageFormat.R8G8B8A8Unorm or
            ImageFormat.R8G8B8A8UnormSrgb	=> Format.R8G8B8A8Unorm,
            _								=> Format.Undefined,
        };

        return vulkanFormat != Format.Undefined;
    }
}
