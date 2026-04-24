namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies the logical phase of the frame in which a render pass executes.
/// Pipelines execute passes in ascending phase order, then by insertion order within a phase.
/// </summary>
public enum RenderPassPhase
{
    /// <summary>
    /// Frame setup: clear, viewport, default state. Runs first.
    /// </summary>
    Setup = 0,

    /// <summary>
    /// Scene-graph traversal and per-frame node updates and draw-list collection.
    /// </summary>
    Collect = 100,

    /// <summary>
    /// Compute work that must complete before geometry passes (for example skinning).
    /// </summary>
    Compute = 200,

    /// <summary>
    /// Shadow-map generation passes.
    /// </summary>
    Shadow = 300,

    /// <summary>
    /// Opaque geometry rendering.
    /// </summary>
    Opaque = 400,

    /// <summary>
    /// Transparent and blended geometry rendering.
    /// </summary>
    Transparent = 500,

    /// <summary>
    /// Overlay and debug rendering drawn on top of regular geometry.
    /// </summary>
    Overlay = 600,

    /// <summary>
    /// Post-processing passes.
    /// </summary>
    PostProcess = 700,

    /// <summary>
    /// Final present and framebuffer blit.
    /// </summary>
    Present = 800,
}