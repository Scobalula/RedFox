using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Represents an opaque GPU buffer resource.
/// </summary>
public interface IGpuBuffer : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the resource has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets the requested buffer size in bytes.
    /// </summary>
    int SizeBytes { get; }

    /// <summary>
    /// Gets the requested element stride in bytes.
    /// </summary>
    int StrideBytes { get; }

    /// <summary>
    /// Gets the declared usage flags for the buffer.
    /// </summary>
    BufferUsage Usage { get; }

    /// <summary>
    /// Gets the scalar or packed storage type carried by the buffer.
    /// </summary>
    GpuBufferElementType ElementType { get; }
}
