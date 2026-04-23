using RedFox.Rendering.Passes;
using Silk.NET.OpenGL;
using System;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Overlay-phase pass that draws skeleton bone axes on top of the scene. When
/// <see cref="OpenGlRenderSettings.BonesRenderOnTop"/> is enabled, depth testing is
/// temporarily disabled so bones remain visible through geometry.
/// </summary>
internal sealed class OpenGlSkeletonOverlayPass : RenderPass, IOverlayPass
{
    private readonly OpenGlRenderResources _resources;

    public OpenGlSkeletonOverlayPass(OpenGlRenderResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public override RenderPassPhase Phase => RenderPassPhase.Overlay;

    protected override void ExecuteCore(RenderFrameContext context)
    {
        OpenGlRenderSettings settings = _resources.Settings;
        if (!settings.ShowSkeletonBones)
        {
            return;
        }

        if (!context.TryGet<OpenGlFrameQueues>(out OpenGlFrameQueues? queues) || queues is null || queues.Bones.Count == 0)
        {
            return;
        }

        bool depthTestWasEnabled = _resources.Context.IsEnabled(EnableCap.DepthTest);
        bool cullFaceWasEnabled = _resources.Context.IsEnabled(EnableCap.CullFace);

        _resources.Context.SetAlphaBlend(true);
        _resources.Context.SetCullFace(false);
        _resources.Context.SetDepthMask(false);

        if (settings.BonesRenderOnTop && depthTestWasEnabled)
        {
            _resources.Context.SetDepthTest(false);
        }

        try
        {
            for (int i = 0; i < queues.Bones.Count; i++)
            {
                queues.Bones[i].DrawOverlay(_resources.LineShaderProgram, queues.SceneAxisMatrix, _resources.ViewportSize, context.View);
            }
        }
        finally
        {
            if (settings.BonesRenderOnTop && depthTestWasEnabled)
            {
                _resources.Context.SetDepthTest(true);
            }

            _resources.Context.SetDepthMask(true);
            _resources.Context.SetCullFace(cullFaceWasEnabled);
            _resources.Context.SetAlphaBlend(false);
        }
    }
}
