using System;

namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Identifies how a GPU buffer will be bound and updated.
/// </summary>
[Flags]
public enum BufferUsage
{
    /// <summary>
    /// No usage flags are specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// The buffer stores vertex data.
    /// </summary>
    Vertex = 1 << 0,

    /// <summary>
    /// The buffer stores index data.
    /// </summary>
    Index = 1 << 1,

    /// <summary>
    /// The buffer stores uniform or constant data.
    /// </summary>
    Uniform = 1 << 2,

    /// <summary>
    /// The buffer stores structured data.
    /// </summary>
    Structured = 1 << 3,

    /// <summary>
    /// The buffer stores shader-storage data.
    /// </summary>
    ShaderStorage = 1 << 4,

    /// <summary>
    /// The buffer is expected to receive dynamic CPU-driven uploads.
    /// </summary>
    DynamicWrite = 1 << 5,

    /// <summary>
    /// The buffer supports CPU writes.
    /// </summary>
    CpuWrite = 1 << 6,

    /// <summary>
    /// The buffer supports CPU reads.
    /// </summary>
    CpuRead = 1 << 7,
}