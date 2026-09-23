using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies how a GPU texture will be bound and used.
/// </summary>
[Flags]
public enum TextureUsage
{
    /// <summary>
    /// No usage flags are specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// The texture will be sampled by shaders.
    /// </summary>
    Sampled = 1 << 0,

    /// <summary>
    /// The texture will be used as a render target.
    /// </summary>
    RenderTarget = 1 << 1,

    /// <summary>
    /// The texture will be used as a depth-stencil target.
    /// </summary>
    DepthStencil = 1 << 2,

    /// <summary>
    /// The texture will be used for unordered or storage access.
    /// </summary>
    Storage = 1 << 3,

    /// <summary>
    /// The texture receives dynamic CPU-driven uploads.
    /// </summary>
    DynamicWrite = 1 << 4,
}