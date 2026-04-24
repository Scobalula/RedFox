using RedFox.Graphics3D.Rendering.Backend;
using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Shared setup pass that resets command-list state for a new frame and clears the default render target.
/// </summary>
public sealed class ClearAndStateResetPass : RenderPass
{
    private readonly Vector4 _clearColor;

    /// <inheritdoc/>
    public override RenderPassPhase Phase => RenderPassPhase.Setup;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearAndStateResetPass"/> class.
    /// </summary>
    /// <param name="clearColor">The clear color applied at frame start.</param>
    public ClearAndStateResetPass(Vector4 clearColor)
    {
        _clearColor = clearColor;
    }

    /// <inheritdoc/>
    protected override void ExecuteCore(RenderFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ICommandList commandList = context.GetRequired<ICommandList>();
        commandList.SetViewport((int)context.ViewportSize.X, (int)context.ViewportSize.Y);
        commandList.SetRenderTarget(null);
        commandList.ClearRenderTarget(_clearColor.X, _clearColor.Y, _clearColor.Z, _clearColor.W, depth: 1.0f);
    }
}