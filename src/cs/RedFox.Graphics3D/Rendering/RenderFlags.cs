using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies the logical render phase consumed by render handles and scene traversal.
/// </summary>
public enum RenderFlags
{
    /// <summary>
    /// GPU compute work for skinning runs in this phase.
    /// </summary>
    SkinningCompute = 0,

    /// <summary>
    /// Opaque geometry renders in this phase.
    /// </summary>
    Opaque = 1,

    /// <summary>
    /// Transparent geometry renders in this phase.
    /// </summary>
    Transparent = 2,

    /// <summary>
    /// Overlay and debug geometry renders in this phase.
    /// </summary>
    Overlay = 3,

    NUMNUMS = 4,
}