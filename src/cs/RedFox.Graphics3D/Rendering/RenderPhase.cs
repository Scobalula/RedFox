using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies the logical render phase consumed by render handles and scene traversal.
/// </summary>
public enum RenderPhase
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

/// <summary>
/// Identifies the render phases in which a render handle has work to submit.
/// </summary>
[Flags]
public enum RenderPhaseMask
{
    /// <summary>
    /// The handle has no render work for the current frame.
    /// </summary>
    None = 0,

    /// <summary>
    /// The handle submits compute skinning work.
    /// </summary>
    SkinningCompute = 1 << (int)RenderPhase.SkinningCompute,

    /// <summary>
    /// The handle submits opaque geometry.
    /// </summary>
    Opaque = 1 << (int)RenderPhase.Opaque,

    /// <summary>
    /// The handle submits transparent geometry.
    /// </summary>
    Transparent = 1 << (int)RenderPhase.Transparent,

    /// <summary>
    /// The handle submits overlay geometry.
    /// </summary>
    Overlay = 1 << (int)RenderPhase.Overlay,
}