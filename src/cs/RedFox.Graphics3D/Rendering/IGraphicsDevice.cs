using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Materials;
using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Represents a rendering backend capable of creating GPU resources and submitting command lists.
/// </summary>
public interface IGraphicsDevice : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether compute workloads are supported by the graphics device.
    /// </summary>
    bool SupportsCompute { get; }

    /// <summary>
    /// Gets the material-type registry exposed by the graphics device.
    /// </summary>
    IMaterialTypeRegistry MaterialTypes { get; }

    /// <summary>
    /// Creates a GPU buffer.
    /// </summary>
    /// <param name="sizeBytes">The buffer size in bytes.</param>
    /// <param name="stride">The element stride in bytes.</param>
    /// <param name="usage">The intended buffer usage.</param>
    /// <returns>The created GPU buffer.</returns>
    IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage);

    /// <summary>
    /// Creates a GPU buffer with initial data.
    /// </summary>
    /// <param name="sizeBytes">The buffer size in bytes.</param>
    /// <param name="stride">The element stride in bytes.</param>
    /// <param name="usage">The intended buffer usage.</param>
    /// <param name="initialData">The initial buffer payload to upload.</param>
    /// <returns>The created GPU buffer.</returns>
    IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> initialData);

    /// <summary>
    /// Updates an existing GPU buffer with new data.
    /// </summary>
    /// <param name="buffer">The buffer to update.</param>
    /// <param name="data">The new payload to upload.</param>
    void UpdateBuffer(IGpuBuffer buffer, ReadOnlySpan<byte> data);

    /// <summary>
    /// Creates a GPU shader from UTF-8 source bytes.
    /// </summary>
    /// <param name="utf8Source">The UTF-8 encoded shader source.</param>
    /// <param name="stage">The shader stage.</param>
    /// <returns>The created GPU shader.</returns>
    IGpuShader CreateShader(ReadOnlySpan<byte> utf8Source, ShaderStage stage);

    /// <summary>
    /// Creates a graphics pipeline state.
    /// </summary>
    /// <param name="vertexShader">The vertex shader.</param>
    /// <param name="fragmentShader">The fragment shader.</param>
    /// <param name="vertexAttributes">The vertex attribute layout.</param>
    /// <param name="cullMode">The triangle cull mode.</param>
    /// <param name="faceWinding">The front-face winding.</param>
    /// <param name="wireframe">Whether wireframe rasterization is enabled.</param>
    /// <param name="blend">Whether color blending is enabled.</param>
    /// <param name="sourceBlendFactor">The source blend factor.</param>
    /// <param name="destinationBlendFactor">The destination blend factor.</param>
    /// <param name="blendOperation">The color blend operation.</param>
    /// <param name="depthTest">Whether depth testing is enabled.</param>
    /// <param name="depthWrite">Whether depth writes are enabled.</param>
    /// <param name="depthCompareFunc">The depth comparison function.</param>
    /// <param name="primitiveTopology">The primitive topology.</param>
    /// <returns>The created pipeline state.</returns>
    IGpuPipelineState CreatePipelineState(
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
        PrimitiveTopology primitiveTopology);

    /// <summary>
    /// Creates a compute pipeline state.
    /// </summary>
    /// <param name="computeShader">The compute shader.</param>
    /// <returns>The created pipeline state.</returns>
    IGpuPipelineState CreatePipelineState(IGpuShader computeShader);

    /// <summary>
    /// Creates a GPU texture without initial pixel data.
    /// </summary>
    /// <param name="width">The texture width in pixels.</param>
    /// <param name="height">The texture height in pixels.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <returns>The created GPU texture.</returns>
    IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage);

    /// <summary>
    /// Creates a GPU texture without initial pixel data and with the requested sample count.
    /// </summary>
    /// <param name="width">The texture width in pixels.</param>
    /// <param name="height">The texture height in pixels.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <param name="sampleCount">The number of samples per pixel.</param>
    /// <returns>The created GPU texture.</returns>
    IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, int sampleCount);

    /// <summary>
    /// Creates a GPU texture with initial pixel data.
    /// </summary>
    /// <param name="width">The texture width in pixels.</param>
    /// <param name="height">The texture height in pixels.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <param name="pixels">The already-encoded GPU payload to upload.</param>
    /// <returns>The created GPU texture.</returns>
    IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels);

    /// <summary>
    /// Creates a GPU texture from an image payload, preserving mip levels, array slices, and cubemap metadata.
    /// </summary>
    /// <param name="image">The image payload to upload.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <returns>The created GPU texture.</returns>
    IGpuTexture CreateTexture(Image image, TextureUsage usage);

    /// <summary>
    /// Returns whether the backend supports a texture format for the supplied usage.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The requested usage flags.</param>
    /// <returns><see langword="true"/> when the format is supported; otherwise <see langword="false"/>.</returns>
    bool SupportsFormat(ImageFormat format, TextureUsage usage);

    /// <summary>
    /// Returns the supported texture sample count closest to the supplied request without exceeding it.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The requested usage flags.</param>
    /// <param name="requestedSampleCount">The desired number of samples per pixel.</param>
    /// <returns>A supported sample count. Returns 1 when multisampling is unavailable.</returns>
    int GetSupportedTextureSampleCount(ImageFormat format, TextureUsage usage, int requestedSampleCount);

    /// <summary>
    /// Creates a render target.
    /// </summary>
    /// <param name="colorTexture">The color texture attachment.</param>
    /// <param name="depthTexture">The optional depth texture attachment.</param>
    /// <returns>The created render target.</returns>
    IGpuRenderTarget CreateRenderTarget(IGpuTexture colorTexture, IGpuTexture? depthTexture);

    /// <summary>
    /// Creates a command list owned by this device.
    /// </summary>
    /// <returns>The created command list.</returns>
    ICommandList CreateCommandList();

    /// <summary>
    /// Submits a command list for execution.
    /// </summary>
    /// <param name="commandList">The command list to submit.</param>
    void Submit(ICommandList commandList);
}