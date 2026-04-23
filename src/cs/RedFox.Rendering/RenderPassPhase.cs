namespace RedFox.Rendering;

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
    /// Scene-graph traversal and per-frame node updates / draw-list collection.
    /// </summary>
    Collect = 100,

    /// <summary>
    /// Compute work that must complete before geometry passes (e.g. skinning).
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
    /// Transparent / blended geometry rendering (back-to-front sorted).
    /// </summary>
    Transparent = 500,

    /// <summary>
    /// Overlay/debug rendering drawn on top of (or in place of) regular geometry.
    /// </summary>
    Overlay = 600,

    /// <summary>
    /// Post-processing passes (SSAO composite, tone-mapping, etc.).
    /// </summary>
    PostProcess = 700,

    /// <summary>
    /// Final present / framebuffer blit.
    /// </summary>
    Present = 800
}
