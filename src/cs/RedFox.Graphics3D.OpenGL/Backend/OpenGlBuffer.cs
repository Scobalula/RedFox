using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.OpenGL.Resources;
using Silk.NET.OpenGL;
using System;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a concrete OpenGL buffer resource.
/// </summary>
internal sealed class OpenGlBuffer : IGpuBuffer
{
    private readonly OpenGlContext _context;

    /// <summary>
    /// Gets the GL buffer handle.
    /// </summary>
    internal uint Handle { get; private set; }

    /// <summary>
    /// Gets the requested buffer size in bytes.
    /// </summary>
    public int SizeBytes { get; }

    /// <summary>
    /// Gets the requested element stride in bytes.
    /// </summary>
    public int StrideBytes { get; }

    /// <summary>
    /// Gets the declared usage flags for the buffer.
    /// </summary>
    public BufferUsage Usage { get; }

    /// <summary>
    /// Gets the scalar or packed storage type carried by the buffer.
    /// </summary>
    public GpuBufferElementType ElementType { get; }

    /// <summary>
    /// Gets the GL bind target used by the buffer.
    /// </summary>
    internal BufferTargetARB Target { get; }

    /// <summary>
    /// Gets the optional texture-buffer view used for sampled access.
    /// </summary>
    internal uint SampledTextureHandle { get; private set; }

    /// <summary>
    /// Gets the GL texture target used for sampled access.
    /// </summary>
    internal TextureTarget SampledTextureTarget { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlBuffer"/> class.
    /// </summary>
    /// <param name="context">The owning OpenGL context wrapper.</param>
    /// <param name="handle">The GL buffer handle.</param>
    /// <param name="sizeBytes">The buffer size in bytes.</param>
    /// <param name="strideBytes">The element stride in bytes.</param>
    /// <param name="usage">The usage flags.</param>
    /// <param name="elementType">The scalar or packed storage type carried by the buffer.</param>
    /// <param name="target">The GL bind target used by the buffer.</param>
    /// <param name="sampledTextureHandle">The optional texture-buffer view used for sampled access.</param>
    /// <param name="sampledTextureTarget">The GL texture target used for sampled access.</param>
    public OpenGlBuffer(OpenGlContext context, uint handle, int sizeBytes, int strideBytes, BufferUsage usage, GpuBufferElementType elementType, BufferTargetARB target, uint sampledTextureHandle, TextureTarget sampledTextureTarget)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Handle = handle;
        SizeBytes = sizeBytes;
        StrideBytes = strideBytes;
        Usage = usage;
        ElementType = elementType;
        Target = target;
        SampledTextureHandle = sampledTextureHandle;
        SampledTextureTarget = sampledTextureTarget;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        if (SampledTextureHandle != 0)
        {
            _context.Gl.DeleteTexture(SampledTextureHandle);
            SampledTextureHandle = 0;
        }

        _context.DeleteBuffer(Handle);
        Handle = 0;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}