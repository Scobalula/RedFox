using RedFox.Graphics2D;
using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Graphics3D.Rendering.Backend;
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
    public bool SupportsCompute => _context.SupportsVersion(4, 3);

    /// <summary>
    /// Gets the backend material-type registry.
    /// </summary>
    public IMaterialTypeRegistry MaterialTypes { get; }

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
    public unsafe IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels)
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

        if (!TryGetTextureFormat(format, usage, out SizedInternalFormat internalFormat, out Silk.NET.OpenGL.PixelFormat pixelFormat, out PixelType pixelType))
        {
            throw new NotSupportedException($"OpenGL does not support texture format '{format}' for usage '{usage}'.");
        }

        GL gl = _context.Gl;
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
    public bool SupportsFormat(ImageFormat format, TextureUsage usage)
    {
        return TryGetTextureFormat(format, usage, out _, out _, out _);
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
        gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            openGlColorTexture.Handle,
            0);

        if (openGlDepthTexture is not null)
        {
            FramebufferAttachment depthAttachment = openGlDepthTexture.Usage.HasFlag(TextureUsage.DepthStencil)
                ? FramebufferAttachment.DepthStencilAttachment
                : FramebufferAttachment.DepthAttachment;
            gl.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                depthAttachment,
                TextureTarget.Texture2D,
                openGlDepthTexture.Handle,
                0);
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
        out Silk.NET.OpenGL.PixelFormat pixelFormat,
        out PixelType pixelType)
    {
        switch (format)
        {
            case ImageFormat.R8G8B8A8Unorm:
                internalFormat = SizedInternalFormat.Rgba8;
                pixelFormat = Silk.NET.OpenGL.PixelFormat.Rgba;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.R8G8B8A8UnormSrgb:
                internalFormat = SizedInternalFormat.Srgb8Alpha8;
                pixelFormat = Silk.NET.OpenGL.PixelFormat.Rgba;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.B8G8R8A8Unorm:
                internalFormat = SizedInternalFormat.Rgba8;
                pixelFormat = Silk.NET.OpenGL.PixelFormat.Bgra;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.B8G8R8A8UnormSrgb:
                internalFormat = SizedInternalFormat.Srgb8Alpha8;
                pixelFormat = Silk.NET.OpenGL.PixelFormat.Bgra;
                pixelType = PixelType.UnsignedByte;
                return true;

            case ImageFormat.D32Float when usage.HasFlag(TextureUsage.DepthStencil):
                internalFormat = SizedInternalFormat.DepthComponent32f;
                pixelFormat = Silk.NET.OpenGL.PixelFormat.DepthComponent;
                pixelType = PixelType.Float;
                return true;

            case ImageFormat.D24UnormS8Uint when usage.HasFlag(TextureUsage.DepthStencil):
                internalFormat = SizedInternalFormat.Depth24Stencil8;
                pixelFormat = Silk.NET.OpenGL.PixelFormat.DepthStencil;
                pixelType = PixelType.UnsignedInt248;
                return true;

            default:
                internalFormat = 0;
                pixelFormat = 0;
                pixelType = 0;
                return false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}