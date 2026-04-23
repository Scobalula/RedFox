using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Rendering.Passes;
using System;
using System.Numerics;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Opaque-phase pass that draws every collected mesh handle using the shared mesh shader.
/// Per-frame uniforms (view/projection/lights) are bound here; per-mesh uniforms are bound
/// inside <see cref="Handles.OpenGlMeshHandle.DrawOpaque"/>.
/// </summary>
internal sealed class OpenGlOpaqueGeometryPass : RenderPass, IOpaqueGeometryPass
{
    private readonly OpenGlRenderResources _resources;

    public OpenGlOpaqueGeometryPass(OpenGlRenderResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public override RenderPassPhase Phase => RenderPassPhase.Opaque;

    protected override void ExecuteCore(RenderFrameContext context)
    {
        if (!context.TryGet<OpenGlFrameQueues>(out OpenGlFrameQueues? queues) || queues is null || queues.Meshes.Count == 0)
        {
            return;
        }

        OpenGlRenderSettings settings = _resources.Settings;
        GlShaderProgram meshShaderProgram = _resources.MeshShaderProgram;

        meshShaderProgram.Use();
        meshShaderProgram.SetMatrix4("uSceneAxis", queues.SceneAxisMatrix);
        meshShaderProgram.SetMatrix4("uView", context.View.ViewMatrix);
        meshShaderProgram.SetMatrix4("uProjection", context.View.ProjectionMatrix);
        meshShaderProgram.SetVector3("uAmbientColor", settings.AmbientColor);
        meshShaderProgram.SetVector3("uCameraPosition", context.View.Position);
        meshShaderProgram.SetInt("uUseViewBasedLighting", settings.UseViewBasedLighting ? 1 : 0);
        meshShaderProgram.SetInt("uLightCount", queues.Lights.Count);

        ReadOnlySpan<Vector4> directionsAndIntensity = queues.Lights.DirectionsAndIntensity;
        ReadOnlySpan<Vector3> colors = queues.Lights.Colors;
        for (int i = 0; i < OpenGlLightFrameData.MaxSceneLights; i++)
        {
            meshShaderProgram.SetVector4($"uLightDirectionsAndIntensity[{i}]", directionsAndIntensity[i]);
            meshShaderProgram.SetVector3($"uLightColors[{i}]", colors[i]);
        }

        bool skinningEnabled = _resources.SkinningComputeProgram is not null;
        for (int i = 0; i < queues.Meshes.Count; i++)
        {
            queues.Meshes[i].DrawOpaque(meshShaderProgram, settings, queues.SceneAxisMatrix, context.View, skinningEnabled);
        }
    }
}
