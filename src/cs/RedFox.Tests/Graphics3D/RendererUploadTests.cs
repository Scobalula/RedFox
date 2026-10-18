using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Buffers.PackedVector;
using RedFox.Graphics3D.Rendering;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Tests.Graphics3D;

public class RendererUploadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedHalfBuffer_UploadsAllValuesAcrossFrames(bool shaderResource)
    {
        PackedBuffer<Half2> source = new(2, 2);
        for (int element = 0; element < 2; element++)
        {
            for (int value = 0; value < 2; value++)
            {
                source.Add(element, value, 0, element + value + 0.25f);
                source.Add(element, value, 1, element - value - 0.5f);
            }
        }

        MeshGpuBufferBinding binding;
        if (shaderResource)
        {
            binding = MeshGpuBufferBinding.CreateShaderResource(source, "TexCoords", BufferUsage.Sampled, false);
        }
        else
        {
            binding = MeshGpuBufferBinding.CreateVertex(source, "TexCoords");
        }

        using UploadTestDevice device = new();
        device.BeginFrameBufferUploads();
        Assert.False(binding.Update(device));
        Assert.True(binding.HasPendingUpload);
        device.BeginFrameBufferUploads();
        Assert.True(binding.Update(device));
        Assert.False(binding.HasPendingUpload);

        UploadTestBuffer buffer = Assert.IsType<UploadTestBuffer>(binding.GpuBuffer);
        Assert.Equal(GpuBufferElementType.Float32, buffer.ElementType);
        Assert.Equal(shaderResource ? 8 : 16, buffer.StrideBytes);
        Assert.Equal(source.TotalComponentCount, binding.TotalComponentCount);
        Assert.Equal(source.ToArray<float>(), MemoryMarshal.Cast<byte, float>(buffer.Bytes).ToArray());
        Assert.Same(source, binding.Data);
        binding.Release();
    }

    [Fact]
    public void PackedNormalizedBuffer_PreservesScaleAndOffsetAndRefreshesAfterRelease()
    {
        PackedBuffer<UByteN4> source = new(1, 1, new Vector4(2, 3, 4, 5), new Vector4(-1, -2, -3, -4));
        source.Add(new Vector4(0.5f, -0.5f, 0.25f, 0.75f));
        MeshGpuBufferBinding binding = MeshGpuBufferBinding.CreateVertex(source, "Color");
        using UploadTestDevice device = new();
        device.BeginFrameBufferUploads();
        Assert.True(binding.Update(device));
        UploadTestBuffer buffer = Assert.IsType<UploadTestBuffer>(binding.GpuBuffer);
        Assert.Equal(source.ToArray<float>(), MemoryMarshal.Cast<byte, float>(buffer.Bytes).ToArray());

        binding.Release();
        source.SetVector4(0, 0, new Vector4(1, 0, 0, 1));
        device.BeginFrameBufferUploads();
        Assert.True(binding.Update(device));
        buffer = Assert.IsType<UploadTestBuffer>(binding.GpuBuffer);
        Assert.Equal(source.ToArray<float>(), MemoryMarshal.Cast<byte, float>(buffer.Bytes).ToArray());
        binding.Release();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedIndices_UploadAsUnsignedIntegers(bool shaderResource)
    {
        PackedBuffer<UByte4> source = new(1);
        source.Add(new Vector4(0, 1, 2, 255));
        MeshGpuBufferBinding binding;
        if (shaderResource)
        {
            binding = MeshGpuBufferBinding.CreateShaderResource(source, "BoneIndices", BufferUsage.Sampled, true);
        }
        else
        {
            binding = MeshGpuBufferBinding.CreateIndex(source, "Indices");
        }

        using UploadTestDevice device = new();
        device.BeginFrameBufferUploads();
        Assert.True(binding.Update(device));
        UploadTestBuffer buffer = Assert.IsType<UploadTestBuffer>(binding.GpuBuffer);
        Assert.Equal(GpuBufferElementType.UInt32, buffer.ElementType);
        Assert.Equal(shaderResource ? 16 : 4, buffer.StrideBytes);
        Assert.Equal(new uint[] { 0, 1, 2, 255 }, MemoryMarshal.Cast<byte, uint>(buffer.Bytes).ToArray());
        binding.Release();
    }

    [Fact]
    public void UnpackedBuffer_PreservesNativeElementType()
    {
        DataBuffer<ushort> source = new(new ushort[] { 0, 1, 2 }, 1, 3);
        MeshGpuBufferBinding binding = MeshGpuBufferBinding.CreateIndex(source, "Indices");
        using UploadTestDevice device = new();
        device.BeginFrameBufferUploads();
        Assert.True(binding.Update(device));
        UploadTestBuffer buffer = Assert.IsType<UploadTestBuffer>(binding.GpuBuffer);
        Assert.Equal(GpuBufferElementType.UInt16, buffer.ElementType);
        Assert.Equal(source.ToArray<ushort>(), MemoryMarshal.Cast<byte, ushort>(buffer.Bytes).ToArray());
        binding.Release();
    }

    [Theory]
    [InlineData(1, 6, true)]
    [InlineData(1, 12, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, false)]
    public void MaterialTextures_SkipUnsupportedDimensionsAndRecover(int depth, int arraySize, bool cubemap)
    {
        using UploadTestDevice device = new();
        Texture texture = new("test") { Data = new Image(1, 1, depth, arraySize, 1, ImageFormat.R8G8B8A8Unorm, cubemap) };
        Type handleType = typeof(MeshGpuBufferBinding).Assembly.GetType("RedFox.Graphics3D.Rendering.Handles.TextureRenderHandle", true)!;
        using IRenderHandle handle = (IRenderHandle)Activator.CreateInstance(handleType, device, texture)!;
        ICommandList commands = DispatchProxy.Create<ICommandList, UploadTestProxy>();
        device.BeginFrameBufferUploads();
        handle.Update(commands);
        Assert.Equal(0, device.TextureAllocations);
        Assert.Equal(0, device.TextureUploads);
        Assert.False(handle.RequiresPerFrameUpdate);

        texture.Data = new Image(1, 1, ImageFormat.R8G8B8A8Unorm);
        ((UploadTestProxy)(object)commands).FrameIndex++;
        Assert.True(handle.RequiresPerFrameUpdate);
        handle.Update(commands);
        Assert.Equal(1, device.TextureAllocations);
        Assert.Equal(1, device.TextureUploads);
        Assert.False(handle.RequiresPerFrameUpdate);
    }
}
