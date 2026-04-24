using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.OpenGL.Resources;
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
    internal int SizeBytes { get; }

    /// <summary>
    /// Gets the requested element stride in bytes.
    /// </summary>
    internal int StrideBytes { get; }

    /// <summary>
    /// Gets the declared usage flags for the buffer.
    /// </summary>
    internal BufferUsage Usage { get; }

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
    public OpenGlBuffer(OpenGlContext context, uint handle, int sizeBytes, int strideBytes, BufferUsage usage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Handle = handle;
        SizeBytes = sizeBytes;
        StrideBytes = strideBytes;
        Usage = usage;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _context.DeleteBuffer(Handle);
        Handle = 0;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}