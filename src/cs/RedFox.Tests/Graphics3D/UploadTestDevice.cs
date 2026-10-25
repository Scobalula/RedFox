using System.Reflection;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Tests.Graphics3D;

internal sealed class UploadTestDevice : IGraphicsDevice, IFrameTextureUploadBudget
{
    public bool SupportsCompute => false;
    public IMaterialTypeRegistry MaterialTypes => throw new NotSupportedException();
    public int RemainingBufferUploadBytes { get; private set; }
    public int TextureAllocations { get; private set; }
    public int TextureUploads { get; private set; }

    public void BeginFrameBufferUploads() => RemainingBufferUploadBytes = 16;

    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, GpuBufferElementType elementType) => new UploadTestBuffer(sizeBytes, stride, usage, elementType);

    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, GpuBufferElementType elementType, ReadOnlySpan<byte> initialData)
    {
        UploadTestBuffer buffer = new(sizeBytes, stride, usage, elementType);
        initialData.CopyTo(buffer.Bytes);
        return buffer;
    }

    public void UpdateBuffer(IGpuBuffer buffer, ReadOnlySpan<byte> data) => data.CopyTo(((UploadTestBuffer)buffer).Bytes);

    public int UploadBufferRange(IGpuBuffer buffer, int byteOffset, ReadOnlySpan<byte> data)
    {
        int count = Math.Min(data.Length, RemainingBufferUploadBytes);
        count -= count % buffer.StrideBytes;
        data[..count].CopyTo(((UploadTestBuffer)buffer).Bytes.AsSpan(byteOffset));
        RemainingBufferUploadBytes -= count;
        return count;
    }

    public bool SupportsFormat(ImageFormat format, TextureUsage usage) => true;

    public IGpuTexture CreateTextureStorage(Image image, TextureUsage usage)
    {
        TextureAllocations++;
        return DispatchProxy.Create<IGpuTexture, UploadTestProxy>();
    }

    public int UploadTextureRange(IGpuTexture texture, Image image, int sliceIndex, int byteOffset)
    {
        TextureUploads++;
        int count = image.Slices[sliceIndex].SlicePitch - byteOffset;
        RemainingBufferUploadBytes -= count;
        return count;
    }

    public void Dispose() { }

    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage) => throw new NotSupportedException();

    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> initialData) => throw new NotSupportedException();

    public IGpuShader CreateShader(ReadOnlySpan<byte> utf8Source, ShaderStage stage) => throw new NotSupportedException();

    public IGpuPipelineState CreatePipelineState(IGpuShader vertexShader, IGpuShader fragmentShader, ReadOnlySpan<VertexAttribute> vertexAttributes, CullMode cullMode, FaceWinding faceWinding, bool wireframe, bool blend, BlendFactor sourceBlendFactor, BlendFactor destinationBlendFactor, BlendOp blendOperation, bool depthTest, bool depthWrite, CompareFunc depthCompareFunc, PrimitiveTopology primitiveTopology, bool alphaToCoverage) => throw new NotSupportedException();

    public IGpuPipelineState CreatePipelineState(IGpuShader computeShader) => throw new NotSupportedException();

    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage) => throw new NotSupportedException();

    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, int sampleCount) => throw new NotSupportedException();

    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels) => throw new NotSupportedException();

    public IGpuTexture CreateTexture(Image image, TextureUsage usage) => throw new NotSupportedException();

    public IGpuTexture CreateTexture(int width, int height, int mipLevels, ImageFormat format, TextureUsage usage) => throw new NotSupportedException();

    public int GetSupportedTextureSampleCount(ImageFormat format, TextureUsage usage, int requestedSampleCount) => throw new NotSupportedException();

    public IGpuRenderTarget CreateRenderTarget(IGpuTexture? colorTexture, IGpuTexture? depthTexture) => throw new NotSupportedException();

    public IGpuRenderTarget CreateRenderTarget(IGpuTexture colorTexture, int mipLevel) => throw new NotSupportedException();

    public ICommandList CreateCommandList() => throw new NotSupportedException();

    public void Submit(ICommandList commandList) => throw new NotSupportedException();
}
