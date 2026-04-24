using System;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Setup-phase pass that resets framebuffer state at the start of every frame:
/// clears color/depth, applies the configured front-face winding, and ensures depth
/// test/cull face are enabled.
/// </summary>
internal sealed class OpenGlClearAndStateResetPass : RenderPass
{
    private readonly OpenGlRenderResources _resources;

    /// <inheritdoc/>
    public override RenderPassPhase Phase => RenderPassPhase.Setup;

    public OpenGlClearAndStateResetPass(OpenGlRenderResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <inheritdoc/>
    protected override void ExecuteCore(RenderFrameContext context)
    {
        _resources.ViewportSize = context.ViewportSize;
        _resources.Context.ClearColorAndDepth(_resources.Settings.ClearColor);
        _resources.Context.SetFrontFace(_resources.Settings.FaceWinding == OpenGlFaceWinding.Ccw);
    }
}
