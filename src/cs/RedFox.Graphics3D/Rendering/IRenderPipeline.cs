using System;
using System.Collections.Generic;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Ordered, mutable collection of <see cref="IRenderPass"/> instances executed in phase order.
/// </summary>
public interface IRenderPipeline : IDisposable
{
    /// <summary>
    /// Gets a snapshot of the currently registered passes in execution order.
    /// </summary>
    IReadOnlyList<IRenderPass> Passes { get; }

    /// <summary>
    /// Adds a pass to the pipeline.
    /// </summary>
    /// <param name="pass">The pass to add.</param>
    void Add(IRenderPass pass);

    /// <summary>
    /// Removes a pass from the pipeline.
    /// </summary>
    /// <param name="pass">The pass to remove.</param>
    /// <returns><see langword="true"/> if the pass was removed; otherwise <see langword="false"/>.</returns>
    bool Remove(IRenderPass pass);

    /// <summary>
    /// Removes all passes matching the supplied type.
    /// </summary>
    /// <typeparam name="TPass">The pass type to remove.</typeparam>
    /// <returns>The number of passes removed.</returns>
    int RemoveAll<TPass>() where TPass : IRenderPass;

    /// <summary>
    /// Initializes every registered pass.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Notifies every pass of a viewport resize.
    /// </summary>
    /// <param name="width">The new viewport width in pixels.</param>
    /// <param name="height">The new viewport height in pixels.</param>
    void Resize(int width, int height);

    /// <summary>
    /// Executes all enabled passes in phase and insertion order against <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The frame context.</param>
    void Execute(RenderFrameContext context);
}