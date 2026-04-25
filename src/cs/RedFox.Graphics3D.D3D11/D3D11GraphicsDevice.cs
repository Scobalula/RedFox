using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the placeholder D3D11 graphics device for the backend skeleton.
/// </summary>
public sealed class D3D11GraphicsDevice : IGraphicsDevice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11GraphicsDevice"/> class.
    /// </summary>
    public D3D11GraphicsDevice()
    {
    }

    /// <inheritdoc/>
    public bool SupportsCompute => throw D3D11BackendSkeleton.NotImplemented();

    /// <inheritdoc/>
    public IMaterialTypeRegistry MaterialTypes => throw D3D11BackendSkeleton.NotImplemented();

    /// <inheritdoc/>
    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> initialData)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void UpdateBuffer(IGpuBuffer buffer, ReadOnlySpan<byte> data)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuShader CreateShader(ReadOnlySpan<byte> utf8Source, ShaderStage stage)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuPipelineState CreatePipelineState(
        IGpuShader vertexShader,
        IGpuShader fragmentShader,
        ReadOnlySpan<VertexAttribute> vertexAttributes,
        CullMode cullMode,
        FaceWinding faceWinding,
        bool wireframe,
        bool blend,
        BlendFactor sourceBlendFactor,
        BlendFactor destinationBlendFactor,
        BlendOp blendOperation,
        bool depthTest,
        bool depthWrite,
        CompareFunc depthCompareFunc,
        PrimitiveTopology primitiveTopology)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuPipelineState CreatePipelineState(IGpuShader computeShader)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public bool SupportsFormat(ImageFormat format, TextureUsage usage)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public IGpuRenderTarget CreateRenderTarget(IGpuTexture colorTexture, IGpuTexture? depthTexture)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public ICommandList CreateCommandList()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void Submit(ICommandList commandList)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }
}