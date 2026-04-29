using RedFox.Graphics3D.Rendering;
using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Shared setup pass that resets command-list state for a new frame and clears the active render target.
/// </summary>
public sealed class ClearAndStateResetPass : RenderPass
{
    /// <inheritdoc/>
    public override RenderPassPhase Phase => RenderPassPhase.Setup;

    /// <summary>
    /// Gets or sets the clear color applied at frame start.
    /// </summary>
    public Vector4 ClearColor { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearAndStateResetPass"/> class.
    /// </summary>
    /// <param name="clearColor">The clear color applied at frame start.</param>
    public ClearAndStateResetPass(Vector4 clearColor)
    {
        ClearColor = clearColor;
    }

    /// <inheritdoc/>
    protected override void ExecuteCore(RenderFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ICommandList commandList = context.GetRequired<ICommandList>();
        context.TryGet(out IGpuRenderTarget? renderTarget);
        commandList.SetViewport((int)context.ViewportSize.X, (int)context.ViewportSize.Y);
        commandList.SetRenderTarget(renderTarget);
        commandList.ClearRenderTarget(ClearColor.X, ClearColor.Y, ClearColor.Z, ClearColor.W, depth: 1.0f);
    }
}