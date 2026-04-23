using System;

namespace RedFox.Rendering;

/// <summary>
/// Represents a single stage of work executed by an <see cref="IRenderPipeline"/>.
/// Implementations may produce, transform, or consume per-frame state via the supplied
/// <see cref="RenderFrameContext"/>. Backends typically derive backend-specific marker
/// interfaces (e.g. opaque/transparent/shadow/skinning) from this contract.
/// </summary>
public interface IRenderPass : IDisposable
{
    /// <summary>
    /// Gets the phase in which this pass executes within a frame.
    /// </summary>
    RenderPassPhase Phase { get; }

    /// <summary>
    /// Gets a value indicating whether the pass should execute.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Initializes any GPU resources needed by the pass. Called once before the first frame.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Notifies the pass that the framebuffer has been resized.
    /// </summary>
    /// <param name="width">The new viewport width in pixels.</param>
    /// <param name="height">The new viewport height in pixels.</param>
    void Resize(int width, int height);

    /// <summary>
    /// Executes the pass against the supplied frame context.
    /// </summary>
    /// <param name="context">The frame context.</param>
    void Execute(RenderFrameContext context);
}
