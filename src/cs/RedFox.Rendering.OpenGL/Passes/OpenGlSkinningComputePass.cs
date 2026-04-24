using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Compute-phase pass that dispatches GPU skinning for every mesh handle that has
/// skinning data and an available compute program.
/// </summary>
internal sealed class OpenGlSkinningComputePass : RenderPass
{
    private readonly OpenGlRenderResources _resources;

    /// <inheritdoc/>
    public override RenderPassPhase Phase => RenderPassPhase.Compute;

    public OpenGlSkinningComputePass(OpenGlRenderResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <inheritdoc/>
    protected override void ExecuteCore(RenderFrameContext context)
    {
        if (!context.TryGet<OpenGlFrameQueues>(out OpenGlFrameQueues? queues) || queues is null)
        {
            return;
        }

        bool dispatched = false;
        for (int i = 0; i < queues.Meshes.Count; i++)
        {
            if (queues.Meshes[i].DispatchSkinning(_resources.SkinningComputeProgram, _resources.Settings.SkinningMode))
            {
                dispatched = true;
            }
        }

        if (dispatched)
        {
            _resources.Context.StorageMemoryBarrier();
        }
    }
}
