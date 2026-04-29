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
    private readonly List<OpenGlCommandList> _commandLists = [];
    private readonly OpenGlContext _context;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether compute workloads are supported.
    /// </summary>
    public bool SupportsCompute => _context.SupportsCompute;

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
        MaterialTypes = new OpenGlMaterialTypeRegistry();
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage)
    {
        return CreateBuffer(sizeBytes, stride, usage, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> initialData)
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

        uint handle = _context.CreateBuffer();
        GL gl = _context.Gl;
        BufferTargetARB target = GetAllocationTarget(usage);
        BufferUsageARB storageUsage = GetBufferUsage(usage);
        gl.BindBuffer(target, handle);
        if (initialData.IsEmpty)
        {
            gl.BufferData(target, (nuint)sizeBytes, (void*)null, storageUsage);
        }
        else
        {
            if (initialData.Length > sizeBytes)
            {
                throw new ArgumentException("Initial buffer data exceeds the requested buffer size.", nameof(initialData));
            }

            fixed (byte* initialDataPointer = initialData)
            {
                gl.BufferData(target, (nuint)sizeBytes, initialDataPointer, storageUsage);
            }
        }

        gl.BindBuffer(target, 0);
        return new OpenGlBuffer(_context, handle, sizeBytes, stride, usage, target);
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

        string source = TranslateShaderSource(Encoding.UTF8.GetString(utf8Source), stage);
        return new OpenGlShader(source, stage);
    }

    private string TranslateShaderSource(string source, ShaderStage stage)
    {
        if (!_context.IsEmbeddedProfile)
        {
            return source;
        }

        return stage == ShaderStage.Compute
            ? source.Replace("#version 430 core", "#version 310 es\nprecision highp float;\nprecision highp int;", StringComparison.Ordinal)
            : source.Replace("#version 330 core", "#version 300 es\nprecision highp float;\nprecision highp int;", StringComparison.Ordinal);
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

        if (!TryGetTextureFormat(format, usage, out SizedInternalFormat internalFormat, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType))
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
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        if (pixels.IsEmpty)
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)internalFormat, (uint)width, (uint)height, 0, (GLEnum)pixelFormat, (GLEnum)pixelType, null);
        }
        else
        {
            fixed (byte* pixelPointer = pixels)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, (int)internalFormat, (uint)width, (uint)height, 0, (GLEnum)pixelFormat, (GLEnum)pixelType, pixelPointer);
            }
        }

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

        return CreateTexture(image.Width, image.Height, image.Format, usage, image.GetSlice().PixelSpan);
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

        if (!TryGetTextureFormat(image.Format, usage, out SizedInternalFormat internalFormat, out global::Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType))
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
                fixed (byte* pixelPointer = slice.PixelSpan)
                {
                    gl.TexImage2D(
                        (GLEnum)faceTarget,
                        mipLevel,
                        (int)internalFormat,
                        (uint)slice.Width,
                        (uint)slice.Height,
                        0,
                        (GLEnum)pixelFormat,
                        (GLEnum)pixelType,
                        pixelPointer);
                }
            }
        }

        gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        return new OpenGlTexture(gl, handle, image.Width, image.Height, image.Format, usage, TextureTarget.TextureCubeMap);
    }

    /// <inheritdoc/>
    public bool SupportsFormat(ImageFormat format, TextureUsage usage)
    {
        return TryGetTextureFormat(format, usage, out _, out _, out _);
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
        if (!_context.IsEmbeddedProfile)
        {
            gl.DrawBuffer(GLEnum.ColorAttachment0);
            gl.ReadBuffer(GLEnum.ColorAttachment0);
        }

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

    private static bool TryGetTextureFormat(
        ImageFormat format,
        TextureUsage usage,
        out SizedInternalFormat internalFormat,
        out global::Silk.NET.OpenGL.PixelFormat pixelFormat,
        out PixelType pixelType)
    {
        switch (format)
        {
            case ImageFormat.R8G8B8A8Unorm:
                internalFormat = SizedInternalFormat.Rgba8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Rgba;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.R8G8B8A8UnormSrgb:
                internalFormat = SizedInternalFormat.Srgb8Alpha8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Rgba;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.B8G8R8A8Unorm:
                internalFormat = SizedInternalFormat.Rgba8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Bgra;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.B8G8R8A8UnormSrgb:
                internalFormat = SizedInternalFormat.Srgb8Alpha8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.Bgra;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.D32Float when usage.HasFlag(TextureUsage.DepthStencil):
                internalFormat = SizedInternalFormat.DepthComponent32f;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.DepthComponent;
                pixelType = PixelType.Float;
                return true;

            case ImageFormat.D24UnormS8Uint when usage.HasFlag(TextureUsage.DepthStencil):
                internalFormat = SizedInternalFormat.Depth24Stencil8;
                pixelFormat = global::Silk.NET.OpenGL.PixelFormat.DepthStencil;
                pixelType = PixelType.UnsignedInt248;
                return true;

            default:
                internalFormat = 0;
                pixelFormat = 0;
                pixelType = 0;
                return false;
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