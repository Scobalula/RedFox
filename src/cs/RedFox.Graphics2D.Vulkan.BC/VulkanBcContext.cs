using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System.Reflection;
using System.Runtime.InteropServices;
using RedFox.Graphics2D.Conversion;
using VulkanBufferHandle = Silk.NET.Vulkan.Buffer;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Owns the Vulkan instance, device, pipelines, and transient GPU resources used by the BC converter engine.
/// </summary>
public sealed unsafe class VulkanBcContext : IDisposable
{
    private const uint DecodeThreadGroupSizeX = 8;
    private const uint DecodeThreadGroupSizeY = 8;
    private const float DefaultBc7AlphaWeight = 1.0f;

    private static readonly VulkanBcImageLayoutTransition UploadTransition = new(
        ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
        AccessFlags.None, AccessFlags.TransferWriteBit,
        PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);

    private static readonly VulkanBcImageLayoutTransition ShaderReadTransition = new(
        ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
        AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit,
        PipelineStageFlags.TransferBit, PipelineStageFlags.ComputeShaderBit);

    private readonly Vk _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _queue;
    private uint _queueFamilyIndex;
    private ulong _nonCoherentAtomSize;
    private CommandPool _commandPool;
    private VulkanBcBuffer _dummyStorageBuffer;
    private VulkanBcComputePipeline _decodeRgba8Pipeline;
    private VulkanBcComputePipeline _decodeRgba16Pipeline;
    private VulkanBcComputePipeline _decodeRgba32Pipeline;
    private VulkanBcComputePipeline _bc6hTryModeG10Pipeline;
    private VulkanBcComputePipeline _bc6hTryModeLE10Pipeline;
    private VulkanBcComputePipeline _bc6hEncodeBlockPipeline;
    private VulkanBcComputePipeline _bc7TryMode456Pipeline;
    private VulkanBcComputePipeline _bc7TryMode137Pipeline;
    private VulkanBcComputePipeline _bc7TryMode02Pipeline;
    private VulkanBcComputePipeline _bc7EncodeBlockPipeline;
    private bool _disposed;

    /// <summary>Gets the Vulkan API instance.</summary>
    public Vk Vk => _vk;

    /// <summary>Gets the logical Vulkan device.</summary>
    public Device Device => _device;

    /// <summary>Gets the physical Vulkan device.</summary>
    public PhysicalDevice PhysicalDevice => _physicalDevice;

    /// <summary>Gets the device queue used for command submission.</summary>
    public Queue Queue => _queue;

    /// <summary>Gets the Vulkan instance.</summary>
    public Instance Instance => _instance;

    /// <summary>Gets the command pool used for short-lived command buffers.</summary>
    public CommandPool CommandPool => _commandPool;

    /// <summary>Gets the non-coherent atom size from the physical device limits.</summary>
    public ulong NonCoherentAtomSize => _nonCoherentAtomSize;

    /// <summary>Initializes a new instance of the <see cref="VulkanBcContext"/> class.</summary>
    /// <param name="vk">The Vulkan API instance to wrap.</param>
    public VulkanBcContext(Vk vk) { _vk = vk; }

    /// <summary>Creates a fully initialized Vulkan BC context when a suitable compute device is available.</summary>
    /// <returns>The initialized context when creation succeeds; otherwise <see langword="null"/>.</returns>
    public static VulkanBcContext? Create()
    {
        Vk vk = Vk.GetApi();
        VulkanBcContext context = new(vk);
        try { return context.Initialize() ? context : null; }
        catch { context.Dispose(); return null; }
    }

    /// <summary>Attempts to execute a BC encode or decode conversion on the GPU.</summary>
    /// <param name="request">The conversion request containing source, destination, dimensions, and flags.</param>
    /// <returns><see langword="true"/> when the GPU path handled the conversion; otherwise <see langword="false"/>.</returns>
    public bool TryConvert(in VulkanBcConversionRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (request.Width <= 0 || request.Height <= 0) return false;
        if (VulkanBcFormatMap.CanDecode(request.SourceFormat, request.DestinationFormat)) return TryDecode(in request);
        if (VulkanBcFormatMap.CanEncode(request.SourceFormat, request.DestinationFormat)) return TryEncode(in request);
        return false;
    }

    /// <summary>Attempts to execute a BC encode or decode conversion on the GPU.</summary>
    /// <param name="source">The source image bytes.</param>
    /// <param name="sourceFormat">The source image format.</param>
    /// <param name="destination">The destination image bytes.</param>
    /// <param name="destinationFormat">The destination image format.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="flags">Optional conversion hints.</param>
    /// <returns><see langword="true"/> when the GPU path handled the conversion; otherwise <see langword="false"/>.</returns>
    public bool TryConvert(ReadOnlySpan<byte> source, ImageFormat sourceFormat, Span<byte> destination, ImageFormat destinationFormat, int width, int height, ImageConvertFlags flags)
    {
        VulkanBcConversionRequest request = new(source, sourceFormat, destination, destinationFormat, width, height, flags);
        return TryConvert(in request);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        if (_device.Handle != 0) _vk.DeviceWaitIdle(_device);

        VulkanBcDestroyer.DestroyPipeline(this, _bc7EncodeBlockPipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _bc7TryMode02Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _bc7TryMode137Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _bc7TryMode456Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _bc6hEncodeBlockPipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _bc6hTryModeLE10Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _bc6hTryModeG10Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _decodeRgba32Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _decodeRgba16Pipeline);
        VulkanBcDestroyer.DestroyPipeline(this, _decodeRgba8Pipeline);
        VulkanBcDestroyer.DestroyBuffer(this, _dummyStorageBuffer);

        if (_commandPool.Handle != 0) _vk.DestroyCommandPool(_device, _commandPool, null);
        if (_device.Handle != 0) _vk.DestroyDevice(_device, null);
        if (_instance.Handle != 0) _vk.DestroyInstance(_instance, null);

        _vk.Dispose();
        _disposed = true;
    }

    private bool Initialize()
    {
        if (!TryCreateInstance()) return false;
        if (!TrySelectPhysicalDevice(out _physicalDevice, out _queueFamilyIndex)) return false;
        if (!TryInitializePhysicalDeviceProperties()) return false;
        if (!TryCreateDevice()) return false;
        if (!TryCreateCommandPool()) return false;

        VulkanBcBufferAllocation dummyAlloc = new(16, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, MemoryPropertyFlags.HostCoherentBit);
        if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in dummyAlloc, out _dummyStorageBuffer)) return false;
        if (!TryCreatePipelines()) return false;

        return true;
    }

    private bool TryInitializePhysicalDeviceProperties()
    {
        if (_physicalDevice.Handle == 0) return false;
        _vk.GetPhysicalDeviceProperties(_physicalDevice, out PhysicalDeviceProperties props);
        _nonCoherentAtomSize = Math.Max(props.Limits.NonCoherentAtomSize, 1UL);
        return true;
    }

    private bool TryCreateInstance()
    {
        byte* applicationName = null;
        byte* engineName = null;
        try
        {
            applicationName = (byte*)SilkMarshal.StringToPtr("RedFox.Graphics2D.BlockCompression.Vulkan");
            engineName = (byte*)SilkMarshal.StringToPtr("RedFox");

            ApplicationInfo appInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = applicationName,
                ApplicationVersion = 1,
                PEngineName = engineName,
                EngineVersion = 1,
                ApiVersion = (1u << 22) | (1u << 12),
            };

            InstanceCreateInfo createInfo = new() { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &appInfo };
            return _vk.CreateInstance(in createInfo, null, out _instance) == Result.Success;
        }
        finally { SilkMarshal.Free((nint)applicationName); SilkMarshal.Free((nint)engineName); }
    }

    private bool TrySelectPhysicalDevice(out PhysicalDevice physicalDevice, out uint queueFamilyIndex)
    {
        physicalDevice = default;
        queueFamilyIndex = 0;

        uint count = 0;
        if (_vk.EnumeratePhysicalDevices(_instance, ref count, null) != Result.Success || count == 0) return false;

        PhysicalDevice[] devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* ptr = devices)
        {
            if (_vk.EnumeratePhysicalDevices(_instance, ref count, ptr) != Result.Success) return false;
        }

        foreach (PhysicalDevice candidate in devices)
        {
            if (TryGetQueueFamilyIndex(candidate, out uint family))
            {
                physicalDevice = candidate;
                queueFamilyIndex = family;
                return true;
            }
        }
        return false;
    }

    private bool TryGetQueueFamilyIndex(PhysicalDevice physicalDevice, out uint queueFamilyIndex)
    {
        queueFamilyIndex = 0;
        uint count = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref count, null);
        if (count == 0) return false;

        QueueFamilyProperties[] families = new QueueFamilyProperties[count];
        fixed (QueueFamilyProperties* ptr = families) _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref count, ptr);

        for (uint i = 0; i < count; i++)
        {
            if ((families[i].QueueFlags & QueueFlags.ComputeBit) != 0) { queueFamilyIndex = i; return true; }
        }
        return false;
    }

    private bool TryCreateDevice()
    {
        float priority = 1.0f;
        DeviceQueueCreateInfo queueInfo = new() { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = _queueFamilyIndex, QueueCount = 1, PQueuePriorities = &priority };
        PhysicalDeviceFeatures features = default;
        DeviceCreateInfo deviceInfo = new() { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo, PEnabledFeatures = &features };

        if (_vk.CreateDevice(_physicalDevice, in deviceInfo, null, out _device) != Result.Success) return false;
        _vk.GetDeviceQueue(_device, _queueFamilyIndex, 0, out _queue);
        return true;
    }

    private bool TryCreateCommandPool()
    {
        CommandPoolCreateInfo info = new() { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = _queueFamilyIndex };
        return _vk.CreateCommandPool(_device, in info, null, out _commandPool) == Result.Success;
    }

    private bool TryCreatePipelines()
    {
        DescriptorSetLayoutBinding[] decodeBindings =
        [
            new(0, DescriptorType.SampledImage, 1, ShaderStageFlags.ComputeBit, null),
            new(1, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, null),
            new(2, DescriptorType.UniformBuffer, 1, ShaderStageFlags.ComputeBit, null),
        ];

        DescriptorSetLayoutBinding[] encodeBindings =
        [
            new(0, DescriptorType.SampledImage, 1, ShaderStageFlags.ComputeBit, null),
            new(1, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, null),
            new(2, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, null),
            new(3, DescriptorType.UniformBuffer, 1, ShaderStageFlags.ComputeBit, null),
        ];

        return TryCreateComputePipeline("BcDecodeRgba8.spv", "DecodeMain", decodeBindings, out _decodeRgba8Pipeline)
            && TryCreateComputePipeline("BcDecodeRgba16.spv", "DecodeMain", decodeBindings, out _decodeRgba16Pipeline)
            && TryCreateComputePipeline("BcDecodeRgba32.spv", "DecodeMain", decodeBindings, out _decodeRgba32Pipeline)
            && TryCreateComputePipeline("BC6HEncode_TryModeG10CS.spv", "TryModeG10CS", encodeBindings, out _bc6hTryModeG10Pipeline)
            && TryCreateComputePipeline("BC6HEncode_TryModeLE10CS.spv", "TryModeLE10CS", encodeBindings, out _bc6hTryModeLE10Pipeline)
            && TryCreateComputePipeline("BC6HEncode_EncodeBlockCS.spv", "EncodeBlockCS", encodeBindings, out _bc6hEncodeBlockPipeline)
            && TryCreateComputePipeline("BC7Encode_TryMode456CS.spv", "TryMode456CS", encodeBindings, out _bc7TryMode456Pipeline)
            && TryCreateComputePipeline("BC7Encode_TryMode137CS.spv", "TryMode137CS", encodeBindings, out _bc7TryMode137Pipeline)
            && TryCreateComputePipeline("BC7Encode_TryMode02CS.spv", "TryMode02CS", encodeBindings, out _bc7TryMode02Pipeline)
            && TryCreateComputePipeline("BC7Encode_EncodeBlockCS.spv", "EncodeBlockCS", encodeBindings, out _bc7EncodeBlockPipeline);
    }

    private bool TryCreateComputePipeline(string shaderFileName, string entryPoint, ReadOnlySpan<DescriptorSetLayoutBinding> bindings, out VulkanBcComputePipeline pipeline)
    {
        pipeline = default;
        if (!TryCreateDescriptorSetLayout(bindings, out DescriptorSetLayout layout)) return false;
        if (!TryCreatePipelineLayout(layout, out PipelineLayout pipelineLayout)) { _vk.DestroyDescriptorSetLayout(_device, layout, null); return false; }

        byte[] shaderBytes = LoadEmbeddedShader(shaderFileName);
        ShaderModule module = default;
        byte* entryPtr = null;
        try
        {
            fixed (byte* ptr = shaderBytes)
            {
                ShaderModuleCreateInfo modInfo = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)shaderBytes.Length, PCode = (uint*)ptr };
                if (_vk.CreateShaderModule(_device, in modInfo, null, out module) != Result.Success) return false;
            }

            entryPtr = (byte*)SilkMarshal.StringToPtr(entryPoint);
            PipelineShaderStageCreateInfo stage = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = module, PName = entryPtr };
            ComputePipelineCreateInfo pipeInfo = new() { SType = StructureType.ComputePipelineCreateInfo, Stage = stage, Layout = pipelineLayout };

            if (_vk.CreateComputePipelines(_device, default, 1, in pipeInfo, null, out Pipeline handle) != Result.Success) return false;
            pipeline = new VulkanBcComputePipeline(layout, pipelineLayout, handle);
            return true;
        }
        finally
        {
            if (module.Handle != 0) _vk.DestroyShaderModule(_device, module, null);
            SilkMarshal.Free((nint)entryPtr);
        }
    }

    private bool TryCreateDescriptorSetLayout(ReadOnlySpan<DescriptorSetLayoutBinding> bindings, out DescriptorSetLayout layout)
    {
        fixed (DescriptorSetLayoutBinding* ptr = bindings)
        {
            DescriptorSetLayoutCreateInfo info = new() { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = (uint)bindings.Length, PBindings = ptr };
            return _vk.CreateDescriptorSetLayout(_device, in info, null, out layout) == Result.Success;
        }
    }

    private bool TryCreatePipelineLayout(DescriptorSetLayout layout, out PipelineLayout pipelineLayout)
    {
        PipelineLayoutCreateInfo info = new() { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &layout };
        return _vk.CreatePipelineLayout(_device, in info, null, out pipelineLayout) == Result.Success;
    }

    private bool TryDecode(in VulkanBcConversionRequest request)
    {
        if (!VulkanBcFormatMap.TryGetDecodeSourceFormat(request.SourceFormat, out Format sourceFormat)) return false;
        if (!SupportsSampledImage(sourceFormat)) return false;

        VulkanBcComputePipeline pipeline = GetDecodePipeline(request.DestinationFormat);
        if (pipeline.Pipeline.Handle == 0) return false;

        VulkanBcBuffer uploadBuffer = default;
        VulkanBcBuffer outputBuffer = default;
        VulkanBcBuffer readbackBuffer = default;
        VulkanBcBuffer constantBuffer = default;
        VulkanBcImage sourceImage = default;
        DescriptorPool descriptorPool = default;
        CommandBuffer commandBuffer = default;
        Fence fence = default;

        try
        {
            VulkanBcBufferAllocation uploadAlloc = new((ulong)request.Source.Length, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCoherentBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in uploadAlloc, out uploadBuffer)) return false;
            if (!VulkanBcGpuResourceAllocator.TryWriteBuffer(this, uploadBuffer, request.Source)) return false;

            VulkanBcBufferAllocation outputAlloc = new((ulong)request.Destination.Length, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostCoherentBit, MemoryPropertyFlags.HostCoherentBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in outputAlloc, out outputBuffer)) return false;

            VulkanBcBufferAllocation readbackAlloc = new((ulong)request.Destination.Length, BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCachedBit | MemoryPropertyFlags.HostCoherentBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in readbackAlloc, out readbackBuffer)) return false;

            VulkanBcBufferAllocation constantAlloc = new((ulong)Marshal.SizeOf<VulkanBcDecodeConstants>(), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCoherentBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in constantAlloc, out constantBuffer)) return false;
            if (!VulkanBcGpuResourceAllocator.TryWriteBuffer(this, constantBuffer, new VulkanBcDecodeConstants { Width = (uint)request.Width, Height = (uint)request.Height })) return false;

            VulkanBcImageAllocation imageAlloc = new(sourceFormat, (uint)request.Width, (uint)request.Height, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateImage(this, in imageAlloc, out sourceImage)) return false;

            DescriptorPoolSize[] poolSizes = [new(DescriptorType.SampledImage, 1), new(DescriptorType.StorageBuffer, 1), new(DescriptorType.UniformBuffer, 1)];
            if (!VulkanBcDescriptorSetManager.TryCreateDescriptorPool(this, 1, poolSizes, out descriptorPool)) return false;
            if (!VulkanBcDescriptorSetManager.TryAllocateDescriptorSet(this, descriptorPool, pipeline.DescriptorSetLayout, out DescriptorSet descriptorSet)) return false;
            if (!TryAllocateCommandBuffer(out commandBuffer)) return false;
            if (!TryCreateFence(out fence)) return false;

            VulkanBcDescriptorSetManager.UpdateDecodeDescriptorSet(this, descriptorSet, sourceImage.View, outputBuffer, constantBuffer);

            CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            if (_vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success) return false;

            RecordDecodeCommands(commandBuffer, uploadBuffer, sourceImage, outputBuffer, readbackBuffer, pipeline, descriptorSet, request.Width, request.Height);

            if (_vk.EndCommandBuffer(commandBuffer) != Result.Success || !TrySubmitAndWait(commandBuffer, fence)) return false;
            return VulkanBcGpuResourceAllocator.TryReadBuffer(this, readbackBuffer, request.Destination);
        }
        finally
        {
            VulkanBcDestroyer.DestroyFence(this, fence);
            VulkanBcDestroyer.FreeCommandBuffer(this, commandBuffer);
            VulkanBcDestroyer.DestroyDescriptorPool(this, descriptorPool);
            VulkanBcDestroyer.DestroyImage(this, sourceImage);
            VulkanBcDestroyer.DestroyBuffer(this, constantBuffer);
            VulkanBcDestroyer.DestroyBuffer(this, readbackBuffer);
            VulkanBcDestroyer.DestroyBuffer(this, outputBuffer);
            VulkanBcDestroyer.DestroyBuffer(this, uploadBuffer);
        }
    }

    private void RecordDecodeCommands(CommandBuffer cmd, VulkanBcBuffer uploadBuffer, VulkanBcImage sourceImage, VulkanBcBuffer outputBuffer, VulkanBcBuffer readbackBuffer, VulkanBcComputePipeline pipeline, DescriptorSet descriptorSet, int width, int height)
    {
        VulkanBcCommandRecorder.TransitionImageLayout(this, cmd, sourceImage.Handle, in UploadTransition);
        VulkanBcCommandRecorder.CopyBufferToImage(this, cmd, uploadBuffer.Handle, sourceImage.Handle, (uint)width, (uint)height);
        VulkanBcCommandRecorder.TransitionImageLayout(this, cmd, sourceImage.Handle, in ShaderReadTransition);

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, pipeline.Pipeline);
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, pipeline.PipelineLayout, 0, 1, in descriptorSet, 0, null);
        _vk.CmdDispatch(cmd, DivideRoundUp((uint)width, DecodeThreadGroupSizeX), DivideRoundUp((uint)height, DecodeThreadGroupSizeY), 1);

        VulkanBcCommandRecorder.InsertBufferBarrier(this, cmd, outputBuffer.Handle, AccessFlags.ShaderWriteBit, AccessFlags.TransferReadBit, PipelineStageFlags.ComputeShaderBit, PipelineStageFlags.TransferBit);
        VulkanBcCommandRecorder.CopyBuffer(this, cmd, outputBuffer.Handle, readbackBuffer.Handle, outputBuffer.Size);
        VulkanBcCommandRecorder.InsertBufferBarrier(this, cmd, readbackBuffer.Handle, AccessFlags.TransferWriteBit, AccessFlags.HostReadBit, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit);
    }

    private bool TryEncode(in VulkanBcConversionRequest request)
    {
        if (!VulkanBcFormatMap.TryGetEncodeSourceFormat(request.SourceFormat, out Format sourceFormat)) return false;
        if (!VulkanBcFormatMap.TryGetEncodeTarget(request.DestinationFormat, out bool isBc7, out uint shaderFormatId)) return false;
        if (!SupportsSampledImage(sourceFormat)) return false;

        uint blockCountX = Math.Max(1u, (uint)((request.Width + 3) / 4));
        uint totalBlockCount = blockCountX * Math.Max(1u, (uint)((request.Height + 3) / 4));
        VulkanBcEncodeStageParameters stageParams = new((uint)request.Width, blockCountX, totalBlockCount, shaderFormatId);

        VulkanBcBuffer uploadBuffer = default;
        VulkanBcImage sourceImage = default;
        VulkanBcBuffer err1Buffer = default;
        VulkanBcBuffer err2Buffer = default;
        VulkanBcBuffer outputBuffer = default;
        VulkanBcBuffer readbackBuffer = default;
        DescriptorPool descriptorPool = default;
        CommandBuffer commandBuffer = default;
        Fence fence = default;
        VulkanBcBuffer[] constantBuffers = [];

        try
        {
            VulkanBcBufferAllocation uploadAlloc = new((ulong)request.Source.Length, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCoherentBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in uploadAlloc, out uploadBuffer)) return false;
            if (!VulkanBcGpuResourceAllocator.TryWriteBuffer(this, uploadBuffer, request.Source)) return false;

            VulkanBcImageAllocation imageAlloc = new(sourceFormat, (uint)request.Width, (uint)request.Height, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateImage(this, in imageAlloc, out sourceImage)) return false;

            VulkanBcBufferAllocation errAlloc = new((ulong)totalBlockCount * 16u, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.DeviceLocalBit, MemoryPropertyFlags.DeviceLocalBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in errAlloc, out err1Buffer)) return false;
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in errAlloc, out err2Buffer)) return false;

            VulkanBcBufferAllocation outputAlloc = new((ulong)request.Destination.Length, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.DeviceLocalBit, MemoryPropertyFlags.DeviceLocalBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in outputAlloc, out outputBuffer)) return false;

            VulkanBcBufferAllocation readbackAlloc = new((ulong)request.Destination.Length, BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCachedBit | MemoryPropertyFlags.HostCoherentBit);
            if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in readbackAlloc, out readbackBuffer)) return false;

            List<VulkanBcEncodePass> stages = BuildEncodeStages(isBc7, request.Flags, stageParams, err1Buffer, err2Buffer, outputBuffer);
            if (stages.Count == 0) return false;

            constantBuffers = new VulkanBcBuffer[stages.Count];
            DescriptorPoolSize[] poolSizes = [new(DescriptorType.SampledImage, (uint)stages.Count), new(DescriptorType.StorageBuffer, (uint)stages.Count * 2), new(DescriptorType.UniformBuffer, (uint)stages.Count)];

            if (!VulkanBcDescriptorSetManager.TryCreateDescriptorPool(this, (uint)stages.Count, poolSizes, out descriptorPool)) return false;
            if (!TryAllocateCommandBuffer(out commandBuffer)) return false;
            if (!TryCreateFence(out fence)) return false;

            DescriptorSet[] descriptorSets = new DescriptorSet[stages.Count];
            VulkanBcBufferAllocation constantAlloc = new((ulong)Marshal.SizeOf<VulkanBcEncodeConstants>(), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit, MemoryPropertyFlags.HostCoherentBit);

            for (int i = 0; i < stages.Count; i++)
            {
                if (!VulkanBcGpuResourceAllocator.TryCreateBuffer(this, in constantAlloc, out constantBuffers[i])) return false;
                if (!VulkanBcGpuResourceAllocator.TryWriteBuffer(this, constantBuffers[i], stages[i].Constants)) return false;
                if (!VulkanBcDescriptorSetManager.TryAllocateDescriptorSet(this, descriptorPool, stages[i].Pipeline.DescriptorSetLayout, out descriptorSets[i])) return false;
                VulkanBcDescriptorSetManager.UpdateEncodeDescriptorSet(this, descriptorSets[i], sourceImage.View, stages[i].InputBuffer, stages[i].OutputBuffer, constantBuffers[i]);
            }

            CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            if (_vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success) return false;

            RecordEncodeCommands(commandBuffer, sourceImage, uploadBuffer, outputBuffer, readbackBuffer, stages, descriptorSets, (uint)request.Width, (uint)request.Height);

            if (_vk.EndCommandBuffer(commandBuffer) != Result.Success || !TrySubmitAndWait(commandBuffer, fence)) return false;
            return VulkanBcGpuResourceAllocator.TryReadBuffer(this, readbackBuffer, request.Destination);
        }
        finally
        {
            foreach (VulkanBcBuffer cb in constantBuffers) VulkanBcDestroyer.DestroyBuffer(this, cb);
            VulkanBcDestroyer.DestroyFence(this, fence);
            VulkanBcDestroyer.FreeCommandBuffer(this, commandBuffer);
            VulkanBcDestroyer.DestroyDescriptorPool(this, descriptorPool);
            VulkanBcDestroyer.DestroyBuffer(this, readbackBuffer);
            VulkanBcDestroyer.DestroyBuffer(this, outputBuffer);
            VulkanBcDestroyer.DestroyBuffer(this, err2Buffer);
            VulkanBcDestroyer.DestroyBuffer(this, err1Buffer);
            VulkanBcDestroyer.DestroyImage(this, sourceImage);
            VulkanBcDestroyer.DestroyBuffer(this, uploadBuffer);
        }
    }

    private void RecordEncodeCommands(CommandBuffer cmd, VulkanBcImage sourceImage, VulkanBcBuffer uploadBuffer, VulkanBcBuffer outputBuffer, VulkanBcBuffer readbackBuffer, List<VulkanBcEncodePass> stages, DescriptorSet[] descriptorSets, uint width, uint height)
    {
        VulkanBcCommandRecorder.TransitionImageLayout(this, cmd, sourceImage.Handle, in UploadTransition);
        VulkanBcCommandRecorder.CopyBufferToImage(this, cmd, uploadBuffer.Handle, sourceImage.Handle, width, height);
        VulkanBcCommandRecorder.TransitionImageLayout(this, cmd, sourceImage.Handle, in ShaderReadTransition);

        for (int i = 0; i < stages.Count; i++)
        {
            VulkanBcEncodePass pass = stages[i];
            DescriptorSet set = descriptorSets[i];
            bool isLast = i == stages.Count - 1;

            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, pass.Pipeline.Pipeline);
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, pass.Pipeline.PipelineLayout, 0, 1, in set, 0, null);
            _vk.CmdDispatch(cmd, Math.Max(pass.GroupCount, 1u), 1, 1);

            AccessFlags nextAccess = isLast ? AccessFlags.TransferReadBit : AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit;
            PipelineStageFlags nextStage = isLast ? PipelineStageFlags.TransferBit : PipelineStageFlags.ComputeShaderBit;
            VulkanBcCommandRecorder.InsertBufferBarrier(this, cmd, pass.OutputBuffer.Handle, AccessFlags.ShaderWriteBit, nextAccess, PipelineStageFlags.ComputeShaderBit, nextStage);
        }

        VulkanBcCommandRecorder.CopyBuffer(this, cmd, outputBuffer.Handle, readbackBuffer.Handle, outputBuffer.Size);
        VulkanBcCommandRecorder.InsertBufferBarrier(this, cmd, readbackBuffer.Handle, AccessFlags.TransferWriteBit, AccessFlags.HostReadBit, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit);
    }

    private List<VulkanBcEncodePass> BuildEncodeStages(bool isBc7, ImageConvertFlags flags, in VulkanBcEncodeStageParameters parameters, VulkanBcBuffer err1Buffer, VulkanBcBuffer err2Buffer, VulkanBcBuffer outputBuffer)
    {
        VulkanBcEncodeStageParameters p = parameters;
        List<VulkanBcEncodePass> stages = [];

        VulkanBcEncodeConstants CreateConstants(uint modeId) => new()
        {
            TextureWidth = p.TextureWidth,
            BlockCountX = p.BlockCountX,
            Format = p.ShaderFormatId,
            ModeId = modeId,
            StartBlockId = 0,
            TotalBlockCount = p.TotalBlockCount,
            AlphaWeight = DefaultBc7AlphaWeight,
            Padding = 0,
        };

        if (isBc7)
        {
            bool quickMode = (flags & ImageConvertFlags.PreferFastBc7Encoding) != 0;
            stages.Add(new(_bc7TryMode456Pipeline, _dummyStorageBuffer, err1Buffer, CreateConstants(0), DivideRoundUp(p.TotalBlockCount, 4)));

            if (!quickMode)
            {
                stages.Add(new(_bc7TryMode137Pipeline, err1Buffer, err2Buffer, CreateConstants(1), p.TotalBlockCount));
                stages.Add(new(_bc7TryMode137Pipeline, err2Buffer, err1Buffer, CreateConstants(3), p.TotalBlockCount));
                stages.Add(new(_bc7TryMode137Pipeline, err1Buffer, err2Buffer, CreateConstants(7), p.TotalBlockCount));
            }

            VulkanBcBuffer finalInput = quickMode ? err1Buffer : err2Buffer;
            stages.Add(new(_bc7EncodeBlockPipeline, finalInput, outputBuffer, CreateConstants(0), DivideRoundUp(p.TotalBlockCount, 4)));
            return stages;
        }

        stages.Add(new(_bc6hTryModeG10Pipeline, _dummyStorageBuffer, err1Buffer, CreateConstants(0), DivideRoundUp(p.TotalBlockCount, 4)));
        for (uint modeId = 0; modeId < 10; modeId++)
        {
            bool writeToErr1 = (modeId & 1u) != 0;
            VulkanBcBuffer input = writeToErr1 ? err2Buffer : err1Buffer;
            VulkanBcBuffer output = writeToErr1 ? err1Buffer : err2Buffer;
            stages.Add(new(_bc6hTryModeLE10Pipeline, input, output, CreateConstants(modeId), DivideRoundUp(p.TotalBlockCount, 2)));
        }

        stages.Add(new(_bc6hEncodeBlockPipeline, err1Buffer, outputBuffer, CreateConstants(0), DivideRoundUp(p.TotalBlockCount, 2)));
        return stages;
    }

    private VulkanBcComputePipeline GetDecodePipeline(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb => _decodeRgba8Pipeline,
            ImageFormat.R16G16B16A16Float => _decodeRgba16Pipeline,
            ImageFormat.R32G32B32A32Float => _decodeRgba32Pipeline,
            _ => default,
        };
    }

    private bool SupportsSampledImage(Format format)
    {
        _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, format, out FormatProperties props);
        return (props.OptimalTilingFeatures & FormatFeatureFlags.SampledImageBit) != 0;
    }

    private bool TryAllocateCommandBuffer(out CommandBuffer commandBuffer)
    {
        CommandBufferAllocateInfo info = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        return _vk.AllocateCommandBuffers(_device, in info, out commandBuffer) == Result.Success;
    }

    private bool TryCreateFence(out Fence fence)
    {
        FenceCreateInfo info = new() { SType = StructureType.FenceCreateInfo };
        return _vk.CreateFence(_device, in info, null, out fence) == Result.Success;
    }

    private bool TrySubmitAndWait(CommandBuffer commandBuffer, Fence fence)
    {
        SubmitInfo info = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &commandBuffer };
        if (_vk.QueueSubmit(_queue, 1, in info, fence) != Result.Success) return false;
        return _vk.WaitForFences(_device, 1, in fence, true, ulong.MaxValue) == Result.Success;
    }

    private byte[] LoadEmbeddedShader(string shaderFileName)
    {
        Assembly assembly = typeof(VulkanBcContext).Assembly;
        string resourceName = $"{typeof(VulkanBcContext).Namespace}.Shaders.{shaderFileName}";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) throw new InvalidOperationException($"Embedded shader resource '{resourceName}' was not found.");

        byte[] bytes = new byte[stream.Length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read <= 0) throw new InvalidOperationException($"Unexpected end of stream while reading '{resourceName}'.");
            offset += read;
        }
        return bytes;
    }

    private static uint DivideRoundUp(uint value, uint divisor) => (value + divisor - 1) / divisor;
}
