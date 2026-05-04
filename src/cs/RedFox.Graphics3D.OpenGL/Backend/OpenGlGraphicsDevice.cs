using RedFox.Graphics2D;
using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents the concrete OpenGL graphics device implementation.
/// </summary>
public sealed class OpenGlGraphicsDevice : IGraphicsDevice
{
    private const string ArbTextureCompressionBptcExtension = "GL_ARB_texture_compression_bptc";
    private const string ArbTextureCompressionRgtcExtension = "GL_ARB_texture_compression_rgtc";
    private const string ExtTextureCompressionBptcExtension = "GL_EXT_texture_compression_bptc";
    private const string ExtTextureCompressionRgtcExtension = "GL_EXT_texture_compression_rgtc";
    private const string ExtTextureCompressionS3tcExtension = "GL_EXT_texture_compression_s3tc";
    private const string ExtTextureCompressionS3tcSrgbExtension = "GL_EXT_texture_compression_s3tc_srgb";
    private const string ExtTextureSrgbExtension = "GL_EXT_texture_sRGB";
    private const uint GlRg = 0x8227;
    private const uint GlRg16f = 0x822F;
    private const uint GlRg32f = 0x8230;

    private readonly List<OpenGlCommandList> _commandLists = [];
    private readonly OpenGlContext _context;
    private readonly bool _supportsBptcTextureCompression;
    private readonly bool _supportsRgtcTextureCompression;
    private readonly bool _supportsS3tcTextureCompression;
    private readonly bool _supportsS3tcSrgbTextureCompression;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether compute workloads are supported.
    /// </summary>
    public bool SupportsCompute => _context.SupportsVersion(3, 1);

    /// <summary>
    /// Gets the backend material-type registry.
    /// </summary>
    public IMaterialTypeRegistry MaterialTypes { get; }

    /// <summary>
    /// Gets or sets the framebuffer used as the default render target.
    /// </summary>
    public uint DefaultFramebufferHandle
    {
        get => _context.DefaultFramebufferHandle;
        set => _context.DefaultFramebufferHandle = value;
    }

    /// <summary>
    /// Gets the sample count of the configured default framebuffer.
    /// </summary>
    /// <returns>The default framebuffer sample count, or 1 when it is single-sampled.</returns>
    public int GetDefaultFramebufferSampleCount()
    {
        ThrowIfDisposed();

        const uint FramebufferBinding = 0x8CA6;
        const uint SampleBuffers = 0x80A8;
        const uint Samples = 0x80A9;

        GL gl = _context.Gl;
        gl.GetInteger((GLEnum)FramebufferBinding, out int previousFramebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _context.DefaultFramebufferHandle);
        gl.GetInteger((GLEnum)SampleBuffers, out int sampleBuffers);
        gl.GetInteger((GLEnum)Samples, out int samples);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, previousFramebuffer < 0 ? 0u : (uint)previousFramebuffer);

        return sampleBuffers > 0 ? Math.Max(1, samples) : 1;
    }

    /// <summary>
    /// Attempts to get the dimensions of the configured default framebuffer color attachment.
    /// </summary>
    /// <param name="width">Receives the framebuffer width in pixels.</param>
    /// <param name="height">Receives the framebuffer height in pixels.</param>
    /// <returns><see langword="true"/> when dimensions were found; otherwise <see langword="false"/>.</returns>
    public bool TryGetDefaultFramebufferSize(out int width, out int height)
    {
        ThrowIfDisposed();

        const uint FramebufferBinding = 0x8CA6;
        const uint FramebufferAttachmentObjectType = 0x8CD0;
        const uint FramebufferAttachmentObjectName = 0x8CD1;
        const uint Renderbuffer = 0x8D41;
        const uint RenderbufferBinding = 0x8CA7;
        const uint RenderbufferWidth = 0x8D42;
        const uint RenderbufferHeight = 0x8D43;

        width = 0;
        height = 0;

        GL gl = _context.Gl;
        gl.GetInteger((GLEnum)FramebufferBinding, out int previousFramebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _context.DefaultFramebufferHandle);
        gl.GetFramebufferAttachmentParameter(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            (GLEnum)FramebufferAttachmentObjectType,
            out int objectType);
        gl.GetFramebufferAttachmentParameter(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            (GLEnum)FramebufferAttachmentObjectName,
            out int objectName);

        if (objectType == Renderbuffer && objectName > 0)
        {
            gl.GetInteger((GLEnum)RenderbufferBinding, out int previousRenderbuffer);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, (uint)objectName);
            gl.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, (GLEnum)RenderbufferWidth, out width);
            gl.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, (GLEnum)RenderbufferHeight, out height);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, previousRenderbuffer < 0 ? 0u : (uint)previousRenderbuffer);
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, previousFramebuffer < 0 ? 0u : (uint)previousFramebuffer);
        return width > 0 && height > 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlGraphicsDevice"/> class.
    /// </summary>
    /// <param name="gl">The active GL instance.</param>
    public OpenGlGraphicsDevice(GL gl)
        : this(new OpenGlContext(gl ?? throw new ArgumentNullException(nameof(gl))))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlGraphicsDevice"/> class.
    /// </summary>
    /// <param name="context">The active OpenGL context wrapper.</param>
    internal OpenGlGraphicsDevice(OpenGlContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ValidateContext();
        _supportsBptcTextureCompression = SupportsBptcTextureCompression(_context.Gl);
        _supportsRgtcTextureCompression = SupportsRgtcTextureCompression(_context.Gl);
        _supportsS3tcTextureCompression = SupportsS3tcTextureCompression(_context.Gl);
        _supportsS3tcSrgbTextureCompression = SupportsS3tcSrgbTextureCompression(_context.Gl);
        MaterialTypes = new OpenGlMaterialTypeRegistry();
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage)
    {
        return CreateBuffer(sizeBytes, stride, usage, GpuBufferElementType.Unknown, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, GpuBufferElementType elementType)
    {
        return CreateBuffer(sizeBytes, stride, usage, elementType, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> initialData)
    {
        return CreateBuffer(sizeBytes, stride, usage, GpuBufferElementType.Unknown, initialData);
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, GpuBufferElementType elementType, ReadOnlySpan<byte> initialData)
    {
        ThrowIfDisposed();

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }

        if (stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        int allocatedSizeBytes = GetAllocatedBufferSizeBytes(sizeBytes, usage);

        uint handle = _context.CreateBuffer();
        GL gl = _context.Gl;
        BufferTargetARB target = GetAllocationTarget(usage);
        BufferUsageARB storageUsage = GetBufferUsage(usage);
        gl.BindBuffer(target, handle);
        if (initialData.IsEmpty)
        {
            gl.BufferData(target, (nuint)allocatedSizeBytes, (void*)null, storageUsage);
        }
        else
        {
            if (initialData.Length > sizeBytes)
            {
                throw new ArgumentException("Initial buffer data exceeds the requested buffer size.", nameof(initialData));
            }

            gl.BufferData(target, (nuint)allocatedSizeBytes, (void*)null, storageUsage);
            fixed (byte* initialDataPointer = initialData)
            {
                gl.BufferSubData(target, 0, (nuint)initialData.Length, initialDataPointer);
            }
        }

        TextureTarget sampledTextureTarget = 0;
        uint sampledTextureHandle = usage.HasFlag(BufferUsage.Sampled)
            ? CreateSampledBufferTexture(handle, sizeBytes, stride, elementType, initialData, out sampledTextureTarget)
            : 0;

        gl.BindBuffer(target, 0);
        return new OpenGlBuffer(_context, handle, sizeBytes, stride, usage, elementType, target, sampledTextureHandle, sampledTextureTarget);
    }

    /// <inheritdoc/>
    public unsafe void UpdateBuffer(IGpuBuffer buffer, ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        OpenGlBuffer openGlBuffer = buffer as OpenGlBuffer
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlBuffer)}.");

        if (data.Length > openGlBuffer.SizeBytes)
        {
            throw new ArgumentException("Buffer update data exceeds the allocated buffer size.", nameof(data));
        }

        GL gl = _context.Gl;
        gl.BindBuffer(openGlBuffer.Target, openGlBuffer.Handle);
        if (data.IsEmpty)
        {
            gl.BindBuffer(openGlBuffer.Target, 0);
            return;
        }

        fixed (byte* dataPointer = data)
        {
            gl.BufferSubData(openGlBuffer.Target, 0, (nuint)data.Length, dataPointer);
        }

        if (openGlBuffer.Usage.HasFlag(BufferUsage.Sampled) && openGlBuffer.SampledTextureTarget == TextureTarget.Texture2D)
        {
            if (data.Length != openGlBuffer.SizeBytes)
            {
                throw new NotSupportedException("OpenGL ES sampled-buffer updates require the full buffer payload.");
            }

            UpdateEmbeddedSampledTexture(openGlBuffer, data);
        }

        gl.BindBuffer(openGlBuffer.Target, 0);
    }

    /// <inheritdoc/>
    public IGpuShader CreateShader(ReadOnlySpan<byte> utf8Source, ShaderStage stage)
    {
        ThrowIfDisposed();

        if (utf8Source.IsEmpty)
        {
            throw new ArgumentException("Shader source cannot be empty.", nameof(utf8Source));
        }

        string source = Encoding.UTF8.GetString(utf8Source);
        return new OpenGlShader(source, stage);
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
        ThrowIfDisposed();

        OpenGlShader openGlVertexShader = vertexShader as OpenGlShader
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlShader)} for vertex shader.");
        OpenGlShader openGlFragmentShader = fragmentShader as OpenGlShader
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlShader)} for fragment shader.");

        if (openGlVertexShader.Stage != ShaderStage.Vertex)
        {
            throw new InvalidOperationException("The supplied vertex shader is not a vertex-stage shader.");
        }

        if (openGlFragmentShader.Stage != ShaderStage.Fragment)
        {
            throw new InvalidOperationException("The supplied fragment shader is not a fragment-stage shader.");
        }

        GlShaderProgram graphicsProgram = _context.CreateShaderProgram(openGlVertexShader.Source, openGlFragmentShader.Source);
        return new OpenGlPipelineState(
            graphicsProgram,
            vertexAttributes,
            cullMode,
            faceWinding,
            wireframe,
            blend,
            sourceBlendFactor,
            destinationBlendFactor,
            blendOperation,
            depthTest,
            depthWrite,
            depthCompareFunc,
            primitiveTopology);
    }

    /// <inheritdoc/>
    public IGpuPipelineState CreatePipelineState(IGpuShader computeShader)
    {
        ThrowIfDisposed();

        if (!SupportsCompute)
        {
            throw new NotSupportedException($"OpenGL compute shaders are not supported by the active context ({_context.VersionString}).");
        }

        OpenGlShader openGlComputeShader = computeShader as OpenGlShader
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlShader)} for compute shader.");

        if (openGlComputeShader.Stage != ShaderStage.Compute)
        {
            throw new InvalidOperationException("The supplied compute shader is not a compute-stage shader.");
        }

        GlComputeProgram computeProgram = _context.CreateComputeProgram(openGlComputeShader.Source);
        return new OpenGlPipelineState(computeProgram);
    }

    /// <inheritdoc/>
    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage)
    {
        return CreateTexture(width, height, format, usage, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, int sampleCount)
    {
        return CreateTexture(width, height, format, usage, ReadOnlySpan<byte>.Empty, sampleCount);
    }

    /// <inheritdoc/>
    public unsafe IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels)
    {
        return CreateTexture(width, height, format, usage, pixels, 1);
    }

    private unsafe IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels, int sampleCount)
    {
        ThrowIfDisposed();

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (sampleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        if (sampleCount > 1 && !pixels.IsEmpty)
        {
            throw new NotSupportedException("Multisampled OpenGL textures cannot be created with initial pixel data.");
        }

        if (sampleCount > 1 && usage.HasFlag(TextureUsage.Sampled))
        {
            throw new NotSupportedException("Multisampled OpenGL textures are supported for render targets only.");
        }

        ValidateTexturePixelData(format, width, height, pixels);

        if (!TryGetTextureFormat(format, usage, out SizedInternalFormat internalFormat, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType, out bool isCompressed))
        {
            throw new NotSupportedException($"OpenGL does not support texture format '{format}' for usage '{usage}'.");
        }

        GL gl = _context.Gl;
        if (sampleCount > 1)
        {
            uint renderbufferHandle = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderbufferHandle);
            gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)sampleCount, (InternalFormat)internalFormat, (uint)width, (uint)height);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            return new OpenGlTexture(gl, renderbufferHandle, width, height, format, usage, sampleCount, TextureTarget.Texture2D, true);
        }

        uint handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, handle);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        UploadTextureLevel(gl, TextureTarget.Texture2D, 0, width, height, format, internalFormat, pixelFormat, pixelType, isCompressed, pixels);

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return new OpenGlTexture(gl, handle, width, height, format, usage);
    }

    /// <inheritdoc/>
    public unsafe IGpuTexture CreateTexture(Image image, TextureUsage usage)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);

        if (image.Depth != 1)
        {
            throw new NotSupportedException("OpenGL texture upload currently supports 2D image slices only.");
        }

        if (image.IsCubemap)
        {
            return CreateCubemapTexture(image, usage);
        }

        if (image.ArraySize != 1)
        {
            throw new NotSupportedException("OpenGL texture arrays are not supported by the current renderer abstraction.");
        }

        return CreateTexture2D(image, usage);
    }

    private unsafe IGpuTexture CreateTexture2D(Image image, TextureUsage usage)
    {
        if (!TryGetTextureFormat(image.Format, usage, out SizedInternalFormat internalFormat, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType, out bool isCompressed))
        {
            throw new NotSupportedException($"OpenGL does not support texture format '{image.Format}' for usage '{usage}'.");
        }

        GL gl = _context.Gl;
        uint handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, handle);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, image.MipLevels > 1 ? (int)GLEnum.LinearMipmapLinear : (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        for (int mipLevel = 0; mipLevel < image.MipLevels; mipLevel++)
        {
            ref readonly ImageSlice slice = ref image.GetSlice(mipLevel);
            UploadTextureLevel(gl, TextureTarget.Texture2D, mipLevel, slice.Width, slice.Height, image.Format, internalFormat, pixelFormat, pixelType, isCompressed, slice.PixelSpan);
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return new OpenGlTexture(gl, handle, image.Width, image.Height, image.Format, usage);
    }

    private unsafe IGpuTexture CreateCubemapTexture(Image image, TextureUsage usage)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Width != image.Height)
        {
            throw new ArgumentException("Cubemap images must be square and greater than zero in size.", nameof(image));
        }

        if (image.ArraySize < 6 || image.ArraySize % 6 != 0)
        {
            throw new ArgumentException("Cubemap images must contain a multiple of six array slices.", nameof(image));
        }

        if (!TryGetTextureFormat(image.Format, usage, out SizedInternalFormat internalFormat, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType, out bool isCompressed))
        {
            throw new NotSupportedException($"OpenGL does not support texture format '{image.Format}' for usage '{usage}'.");
        }

        GL gl = _context.Gl;
        uint handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.TextureCubeMap, handle);
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, image.MipLevels > 1 ? (int)GLEnum.LinearMipmapLinear : (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);

        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
        {
            TextureTarget faceTarget = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + faceIndex);
            for (int mipLevel = 0; mipLevel < image.MipLevels; mipLevel++)
            {
                ref readonly ImageSlice slice = ref image.GetSlice(mipLevel, faceIndex);
                UploadTextureLevel(gl, faceTarget, mipLevel, slice.Width, slice.Height, image.Format, internalFormat, pixelFormat, pixelType, isCompressed, slice.PixelSpan);
            }
        }

        gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        return new OpenGlTexture(gl, handle, image.Width, image.Height, image.Format, usage, TextureTarget.TextureCubeMap);
    }

    /// <inheritdoc/>
    public bool SupportsFormat(ImageFormat format, TextureUsage usage)
    {
        return TryGetTextureFormat(format, usage, out _, out _, out _, out _);
    }

    /// <inheritdoc/>
    public int GetSupportedTextureSampleCount(ImageFormat format, TextureUsage usage, int requestedSampleCount)
    {
        ThrowIfDisposed();
        if (requestedSampleCount <= 1 || !SupportsFormat(format, usage))
        {
            return 1;
        }

        if (!_context.SupportsVersion(3, 0))
        {
            return 1;
        }

        _context.Gl.GetInteger(GLEnum.MaxSamples, out int maximumSampleCount);
        if (maximumSampleCount <= 1)
        {
            return 1;
        }

        int cappedSampleCount = Math.Min(requestedSampleCount, maximumSampleCount);
        int supportedSampleCount = 1;
        for (int sampleCount = 2; sampleCount <= cappedSampleCount; sampleCount <<= 1)
        {
            supportedSampleCount = sampleCount;
        }

        return supportedSampleCount;
    }

    /// <inheritdoc/>
    public IGpuRenderTarget CreateRenderTarget(IGpuTexture colorTexture, IGpuTexture? depthTexture)
    {
        ThrowIfDisposed();

        OpenGlTexture openGlColorTexture = colorTexture as OpenGlTexture
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlTexture)} for color texture.");
        OpenGlTexture? openGlDepthTexture = depthTexture as OpenGlTexture;

        GL gl = _context.Gl;
        uint handle = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle);
        AttachFramebufferResource(gl, FramebufferAttachment.ColorAttachment0, openGlColorTexture);
        if (openGlDepthTexture is not null)
        {
            AttachFramebufferResource(gl, GetDepthFramebufferAttachment(openGlDepthTexture.Format), openGlDepthTexture);
        }

        GLEnum framebufferStatus = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (framebufferStatus != GLEnum.FramebufferComplete)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.DeleteFramebuffer(handle);
            throw new InvalidOperationException($"OpenGL framebuffer is incomplete: {framebufferStatus}.");
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return new OpenGlRenderTarget(gl, handle, openGlColorTexture, openGlDepthTexture);
    }

    /// <inheritdoc/>
    public ICommandList CreateCommandList()
    {
        ThrowIfDisposed();
        OpenGlCommandList commandList = new(_context);
        _commandLists.Add(commandList);
        return commandList;
    }

    /// <inheritdoc/>
    public void Submit(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (int i = 0; i < _commandLists.Count; i++)
        {
            _commandLists[i].Dispose();
        }

        _commandLists.Clear();
        _context.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static BufferTargetARB GetAllocationTarget(BufferUsage usage)
    {
        if (usage.HasFlag(BufferUsage.Index))
        {
            return BufferTargetARB.ElementArrayBuffer;
        }

        if (usage.HasFlag(BufferUsage.ShaderStorage) || usage.HasFlag(BufferUsage.Structured))
        {
            return BufferTargetARB.ShaderStorageBuffer;
        }

        if (usage.HasFlag(BufferUsage.Uniform))
        {
            return BufferTargetARB.UniformBuffer;
        }

        return BufferTargetARB.ArrayBuffer;
    }

    private static BufferUsageARB GetBufferUsage(BufferUsage usage)
    {
        return usage.HasFlag(BufferUsage.DynamicWrite) || usage.HasFlag(BufferUsage.CpuWrite)
            ? BufferUsageARB.DynamicDraw
            : BufferUsageARB.StaticDraw;
    }

    private uint CreateSampledBufferTexture(
        uint bufferHandle,
        int sizeBytes,
        int stride,
        GpuBufferElementType elementType,
        ReadOnlySpan<byte> initialData,
        out TextureTarget textureTarget)
    {
        textureTarget = TextureTarget.Texture2D;
        return CreateEmbeddedSampledBufferTexture(sizeBytes, stride, elementType, initialData);
    }

    private unsafe uint CreateEmbeddedSampledBufferTexture(int sizeBytes, int stride, GpuBufferElementType elementType, ReadOnlySpan<byte> initialData)
    {
        GL gl = _context.Gl;
        GetEmbeddedSampledTextureDimensions(sizeBytes, stride, out int width, out int height);
        GetEmbeddedSampledTextureFormat(stride, elementType, out SizedInternalFormat internalFormat, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType);

        uint textureHandle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, textureHandle);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        gl.GetInteger(GLEnum.UnpackAlignment, out int previousUnpackAlignment);
        int rowPitch = checked(width * stride);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, GetTextureUploadAlignment(rowPitch));

        if (initialData.IsEmpty)
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)internalFormat, (uint)width, (uint)height, 0, (GLEnum)pixelFormat, (GLEnum)pixelType, null);
        }
        else
        {
            ReadOnlySpan<byte> uploadData = GetPaddedSampledTextureData(initialData, rowPitch, height);
            fixed (byte* uploadPointer = uploadData)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, (int)internalFormat, (uint)width, (uint)height, 0, (GLEnum)pixelFormat, (GLEnum)pixelType, uploadPointer);
            }
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, previousUnpackAlignment);
        return textureHandle;
    }

    private unsafe void UpdateEmbeddedSampledTexture(OpenGlBuffer buffer, ReadOnlySpan<byte> data)
    {
        GL gl = _context.Gl;
        GetEmbeddedSampledTextureDimensions(buffer.SizeBytes, buffer.StrideBytes, out int width, out int height);
        GetEmbeddedSampledTextureFormat(buffer.StrideBytes, buffer.ElementType, out _, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType);

        int rowPitch = checked(width * buffer.StrideBytes);
        ReadOnlySpan<byte> uploadData = GetPaddedSampledTextureData(data, rowPitch, height);
        gl.GetInteger(GLEnum.UnpackAlignment, out int previousUnpackAlignment);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, GetTextureUploadAlignment(rowPitch));
        gl.BindTexture(TextureTarget.Texture2D, buffer.SampledTextureHandle);
        fixed (byte* uploadPointer = uploadData)
        {
            gl.TexSubImage2D(GLEnum.Texture2D, 0, 0, 0, (uint)width, (uint)height, (GLEnum)pixelFormat, (GLEnum)pixelType, uploadPointer);
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, previousUnpackAlignment);
    }

    private void GetEmbeddedSampledTextureDimensions(int sizeBytes, int stride, out int width, out int height)
    {
        if (stride <= 0 || (sizeBytes % stride) != 0)
        {
            throw new NotSupportedException($"OpenGL ES sampled buffers require payload sizes aligned to the sampled stride ({stride} bytes).");
        }

        int elementCount = sizeBytes / stride;
        if (elementCount <= 0)
        {
            throw new NotSupportedException("OpenGL ES sampled buffers require at least one sampled element.");
        }

        _context.Gl.GetInteger(GLEnum.MaxTextureSize, out int maxTextureSize);
        width = Math.Min(Math.Max(1, maxTextureSize), elementCount);
        height = (elementCount + width - 1) / width;
    }

    private static ReadOnlySpan<byte> GetPaddedSampledTextureData(ReadOnlySpan<byte> source, int rowPitch, int height)
    {
        int expectedSizeBytes = checked(rowPitch * height);
        if (source.Length == expectedSizeBytes)
        {
            return source;
        }

        if (source.Length > expectedSizeBytes)
        {
            throw new ArgumentException("Sampled texture upload data exceeds the target texture footprint.", nameof(source));
        }

        byte[] paddedData = new byte[expectedSizeBytes];
        source.CopyTo(paddedData);
        return paddedData;
    }

    private static SizedInternalFormat GetSampledBufferInternalFormat(int stride, GpuBufferElementType elementType)
    {
        int componentSizeBytes = GetElementSizeBytes(elementType);
        if (componentSizeBytes <= 0 || stride % componentSizeBytes != 0)
        {
            throw new NotSupportedException($"OpenGL sampled buffers do not support '{elementType}' with a stride of {stride} bytes.");
        }

        int componentCount = stride / componentSizeBytes;
        return (elementType, componentCount) switch
        {
            (GpuBufferElementType.Float16, 1) => SizedInternalFormat.R16f,
            (GpuBufferElementType.Float16, 2) => (SizedInternalFormat)GlRg16f,
            (GpuBufferElementType.Float16, 4) => SizedInternalFormat.Rgba16f,
            (GpuBufferElementType.Float32, 1) => SizedInternalFormat.R32f,
            (GpuBufferElementType.Float32, 2) => (SizedInternalFormat)GlRg32f,
            (GpuBufferElementType.Float32, 4) => SizedInternalFormat.Rgba32f,
            (GpuBufferElementType.Int8, 1) => SizedInternalFormat.R8i,
            (GpuBufferElementType.Int8, 4) => SizedInternalFormat.Rgba8i,
            (GpuBufferElementType.UInt8, 1) => SizedInternalFormat.R8ui,
            (GpuBufferElementType.UInt8, 4) => SizedInternalFormat.Rgba8ui,
            (GpuBufferElementType.Int16, 1) => SizedInternalFormat.R16i,
            (GpuBufferElementType.Int16, 4) => SizedInternalFormat.Rgba16i,
            (GpuBufferElementType.UInt16, 1) => SizedInternalFormat.R16ui,
            (GpuBufferElementType.UInt16, 4) => SizedInternalFormat.Rgba16ui,
            (GpuBufferElementType.Int32, 1) => SizedInternalFormat.R32i,
            (GpuBufferElementType.Int32, 4) => SizedInternalFormat.Rgba32i,
            (GpuBufferElementType.UInt32, 1) => SizedInternalFormat.R32ui,
            (GpuBufferElementType.UInt32, 4) => SizedInternalFormat.Rgba32ui,
            _ => throw new NotSupportedException($"OpenGL sampled buffers do not support '{elementType}' with {componentCount} components per sampled element."),
        };
    }

    private static void GetEmbeddedSampledTextureFormat(
        int stride,
        GpuBufferElementType elementType,
        out SizedInternalFormat internalFormat,
        out global::Silk.NET.OpenGL.PixelFormat pixelFormat,
        out PixelType pixelType)
    {
        int componentSizeBytes = GetElementSizeBytes(elementType);
        if (componentSizeBytes <= 0 || stride % componentSizeBytes != 0)
        {
            throw new NotSupportedException($"OpenGL ES sampled buffers do not support '{elementType}' with a stride of {stride} bytes.");
        }

        int componentCount = stride / componentSizeBytes;
        (internalFormat, pixelFormat, pixelType) = (elementType, componentCount) switch
        {
            (GpuBufferElementType.Float16, 1) => (SizedInternalFormat.R16f, global::Silk.NET.OpenGL.PixelFormat.Red, PixelType.HalfFloat),
            (GpuBufferElementType.Float16, 2) => ((SizedInternalFormat)GlRg16f, (global::Silk.NET.OpenGL.PixelFormat)GlRg, PixelType.HalfFloat),
            (GpuBufferElementType.Float16, 4) => (SizedInternalFormat.Rgba16f, global::Silk.NET.OpenGL.PixelFormat.Rgba, PixelType.HalfFloat),
            (GpuBufferElementType.Float32, 1) => (SizedInternalFormat.R32f, global::Silk.NET.OpenGL.PixelFormat.Red, PixelType.Float),
            (GpuBufferElementType.Float32, 2) => ((SizedInternalFormat)GlRg32f, (global::Silk.NET.OpenGL.PixelFormat)GlRg, PixelType.Float),
            (GpuBufferElementType.Float32, 4) => (SizedInternalFormat.Rgba32f, global::Silk.NET.OpenGL.PixelFormat.Rgba, PixelType.Float),
            (GpuBufferElementType.Int8, 1) => (SizedInternalFormat.R8i, global::Silk.NET.OpenGL.PixelFormat.RedInteger, PixelType.Byte),
            (GpuBufferElementType.Int8, 4) => (SizedInternalFormat.Rgba8i, global::Silk.NET.OpenGL.PixelFormat.RgbaInteger, PixelType.Byte),
            (GpuBufferElementType.UInt8, 1) => (SizedInternalFormat.R8ui, global::Silk.NET.OpenGL.PixelFormat.RedInteger, PixelType.UnsignedByte),
            (GpuBufferElementType.UInt8, 4) => (SizedInternalFormat.Rgba8ui, global::Silk.NET.OpenGL.PixelFormat.RgbaInteger, PixelType.UnsignedByte),
            (GpuBufferElementType.Int16, 1) => (SizedInternalFormat.R16i, global::Silk.NET.OpenGL.PixelFormat.RedInteger, PixelType.Short),
            (GpuBufferElementType.Int16, 4) => (SizedInternalFormat.Rgba16i, global::Silk.NET.OpenGL.PixelFormat.RgbaInteger, PixelType.Short),
            (GpuBufferElementType.UInt16, 1) => (SizedInternalFormat.R16ui, global::Silk.NET.OpenGL.PixelFormat.RedInteger, PixelType.UnsignedShort),
            (GpuBufferElementType.UInt16, 4) => (SizedInternalFormat.Rgba16ui, global::Silk.NET.OpenGL.PixelFormat.RgbaInteger, PixelType.UnsignedShort),
            (GpuBufferElementType.Int32, 1) => (SizedInternalFormat.R32i, global::Silk.NET.OpenGL.PixelFormat.RedInteger, PixelType.Int),
            (GpuBufferElementType.Int32, 4) => (SizedInternalFormat.Rgba32i, global::Silk.NET.OpenGL.PixelFormat.RgbaInteger, PixelType.Int),
            (GpuBufferElementType.UInt32, 1) => (SizedInternalFormat.R32ui, global::Silk.NET.OpenGL.PixelFormat.RedInteger, PixelType.UnsignedInt),
            (GpuBufferElementType.UInt32, 4) => (SizedInternalFormat.Rgba32ui, global::Silk.NET.OpenGL.PixelFormat.RgbaInteger, PixelType.UnsignedInt),
            _ => throw new NotSupportedException($"OpenGL ES sampled buffers do not support '{elementType}' with {componentCount} components per sampled element."),
        };
    }

    private static int GetElementSizeBytes(GpuBufferElementType elementType)
    {
        return elementType switch
        {
            GpuBufferElementType.Float16 or GpuBufferElementType.Int16 or GpuBufferElementType.UInt16 => sizeof(ushort),
            GpuBufferElementType.Float32 or GpuBufferElementType.Int32 or GpuBufferElementType.UInt32 => sizeof(uint),
            GpuBufferElementType.Int8 or GpuBufferElementType.UInt8 => sizeof(byte),
            _ => 0,
        };
    }

    private static int GetAllocatedBufferSizeBytes(int sizeBytes, BufferUsage usage)
    {
        if (!(usage.HasFlag(BufferUsage.ShaderStorage) || usage.HasFlag(BufferUsage.Structured)) || (sizeBytes % sizeof(uint)) == 0)
        {
            return sizeBytes;
        }

        return checked(sizeBytes + (sizeof(uint) - (sizeBytes % sizeof(uint))));
    }

    private void ValidateContext()
    {
        _context.RequireVersion(3, 0);
    }

    private static int GetTextureUploadAlignment(int rowPitch)
    {
        if (rowPitch <= 0)
        {
            return 4;
        }

        if ((rowPitch % 8) == 0)
        {
            return 8;
        }

        if ((rowPitch % 4) == 0)
        {
            return 4;
        }

        return (rowPitch % 2) == 0 ? 2 : 1;
    }

    private bool TryGetTextureFormat(
        ImageFormat format,
        TextureUsage usage,
        out SizedInternalFormat internalFormat,
        out global::Silk.NET.OpenGL.PixelFormat pixelFormat,
        out PixelType pixelType,
        out bool isCompressed)
    {
        switch (format)
        {
            case ImageFormat.R8G8B8A8Unorm:
                internalFormat = SizedInternalFormat.Rgba8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Rgba;
                pixelType = PixelType.UnsignedByte;
                isCompressed = false;
                return true;

            case ImageFormat.R8G8B8A8UnormSrgb:
                internalFormat = SizedInternalFormat.Srgb8Alpha8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Rgba;
                pixelType = PixelType.UnsignedByte;
                isCompressed = false;
                return true;

            case ImageFormat.B8G8R8A8Unorm:
                internalFormat = SizedInternalFormat.Rgba8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Bgra;
                pixelType = PixelType.UnsignedByte;
                isCompressed = false;
                return true;

            case ImageFormat.B8G8R8A8UnormSrgb:
                internalFormat = SizedInternalFormat.Srgb8Alpha8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Bgra;
                pixelType = PixelType.UnsignedByte;
                isCompressed = false;
                return true;

            case ImageFormat.BC1Typeless or ImageFormat.BC1Unorm when SupportsCompressedSampling(usage) && _supportsS3tcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRgbaS3TCDxt1Ext;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC1UnormSrgb when SupportsCompressedSampling(usage) && _supportsS3tcSrgbTextureCompression:
                internalFormat = SizedInternalFormat.CompressedSrgbAlphaS3TCDxt1Ext;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC2Typeless or ImageFormat.BC2Unorm when SupportsCompressedSampling(usage) && _supportsS3tcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRgbaS3TCDxt3Ext;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC2UnormSrgb when SupportsCompressedSampling(usage) && _supportsS3tcSrgbTextureCompression:
                internalFormat = SizedInternalFormat.CompressedSrgbAlphaS3TCDxt3Ext;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC3Typeless or ImageFormat.BC3Unorm when SupportsCompressedSampling(usage) && _supportsS3tcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRgbaS3TCDxt5Ext;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC3UnormSrgb when SupportsCompressedSampling(usage) && _supportsS3tcSrgbTextureCompression:
                internalFormat = SizedInternalFormat.CompressedSrgbAlphaS3TCDxt5Ext;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC4Typeless or ImageFormat.BC4Unorm when SupportsCompressedSampling(usage) && _supportsRgtcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRedRgtc1;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC4Snorm when SupportsCompressedSampling(usage) && _supportsRgtcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedSignedRedRgtc1;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC5Typeless or ImageFormat.BC5Unorm when SupportsCompressedSampling(usage) && _supportsRgtcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRGRgtc2;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC5Snorm when SupportsCompressedSampling(usage) && _supportsRgtcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedSignedRGRgtc2;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC6HUF16 when SupportsCompressedSampling(usage) && _supportsBptcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRgbBptcUnsignedFloat;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC6HSF16 when SupportsCompressedSampling(usage) && _supportsBptcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRgbBptcSignedFloat;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC7Unorm when SupportsCompressedSampling(usage) && _supportsBptcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedRgbaBptcUnorm;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.BC7UnormSrgb when SupportsCompressedSampling(usage) && _supportsBptcTextureCompression:
                internalFormat = SizedInternalFormat.CompressedSrgbAlphaBptcUnorm;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = true;
                return true;

            case ImageFormat.D32Float when usage.HasFlag(TextureUsage.DepthStencil):
                internalFormat = SizedInternalFormat.DepthComponent32f;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.DepthComponent;
                pixelType = PixelType.Float;
                isCompressed = false;
                return true;

            case ImageFormat.D24UnormS8Uint when usage.HasFlag(TextureUsage.DepthStencil):
                internalFormat = SizedInternalFormat.Depth24Stencil8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.DepthStencil;
                pixelType = PixelType.UnsignedInt248;
                isCompressed = false;
                return true;

            default:
                internalFormat = 0;
                pixelFormat = 0;
                pixelType = 0;
                isCompressed = false;
                return false;
        }
    }

    private static bool SupportsBptcTextureCompression(GL gl)
    {
        return gl.IsExtensionPresent(ExtTextureCompressionBptcExtension)
            || gl.IsExtensionPresent(ArbTextureCompressionBptcExtension);
    }

    private static bool SupportsRgtcTextureCompression(GL gl)
    {
        return gl.IsExtensionPresent(ExtTextureCompressionRgtcExtension)
            || gl.IsExtensionPresent(ArbTextureCompressionRgtcExtension);
    }

    private static bool SupportsS3tcTextureCompression(GL gl)
    {
        return gl.IsExtensionPresent(ExtTextureCompressionS3tcExtension);
    }

    private static bool SupportsS3tcSrgbTextureCompression(GL gl)
    {
        return gl.IsExtensionPresent(ExtTextureCompressionS3tcSrgbExtension)
            || gl.IsExtensionPresent(ExtTextureSrgbExtension);
    }

    private static bool SupportsCompressedSampling(TextureUsage usage)
    {
        return usage.HasFlag(TextureUsage.Sampled)
            && !usage.HasFlag(TextureUsage.RenderTarget)
            && !usage.HasFlag(TextureUsage.DepthStencil)
            && !usage.HasFlag(TextureUsage.Storage);
    }

    private static void ValidateTexturePixelData(ImageFormat format, int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (pixels.IsEmpty)
        {
            return;
        }

        (_, int slicePitch) = ImageFormatInfo.CalculatePitch(format, width, height);
        if (pixels.Length < slicePitch)
        {
            throw new ArgumentException("Initial texture data is smaller than the requested texture size.", nameof(pixels));
        }
    }

    private static int GetTextureSlicePitch(ImageFormat format, int width, int height)
    {
        (_, int slicePitch) = ImageFormatInfo.CalculatePitch(format, width, height);
        return slicePitch;
    }

    private static unsafe void UploadTextureLevel(
        GL gl,
        TextureTarget target,
        int mipLevel,
        int width,
        int height,
        ImageFormat format,
        SizedInternalFormat internalFormat,
        global::Silk.NET.OpenGL.PixelFormat pixelFormat,
        PixelType pixelType,
        bool isCompressed,
        ReadOnlySpan<byte> pixels)
    {
        if (isCompressed)
        {
            uint imageSize = checked((uint)GetTextureSlicePitch(format, width, height));
            if (pixels.IsEmpty)
            {
                gl.CompressedTexImage2D(target, mipLevel, (InternalFormat)internalFormat, (uint)width, (uint)height, 0, imageSize, null);
                return;
            }

            fixed (byte* pixelPointer = pixels)
            {
                gl.CompressedTexImage2D(target, mipLevel, (InternalFormat)internalFormat, (uint)width, (uint)height, 0, imageSize, pixelPointer);
            }

            return;
        }

        if (pixels.IsEmpty)
        {
            gl.TexImage2D((GLEnum)target, mipLevel, (int)internalFormat, (uint)width, (uint)height, 0, (GLEnum)pixelFormat, (GLEnum)pixelType, null);
            return;
        }

        fixed (byte* pixelPointer = pixels)
        {
            gl.TexImage2D((GLEnum)target, mipLevel, (int)internalFormat, (uint)width, (uint)height, 0, (GLEnum)pixelFormat, (GLEnum)pixelType, pixelPointer);
        }
    }

    private static FramebufferAttachment GetDepthFramebufferAttachment(ImageFormat format)
    {
        return format == ImageFormat.D24UnormS8Uint
            ? FramebufferAttachment.DepthStencilAttachment
            : FramebufferAttachment.DepthAttachment;
    }

    private static void AttachFramebufferResource(GL gl, FramebufferAttachment attachment, OpenGlTexture texture)
    {
        if (texture.IsRenderbuffer)
        {
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, attachment, RenderbufferTarget.Renderbuffer, texture.Handle);
            return;
        }

        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, texture.Target, texture.Handle, 0);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}