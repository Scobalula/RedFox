using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;
using RedFox.Rendering.OpenGL.Handles;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Collect-phase pass that walks the scene graph, refreshes per-node render handles,
/// and publishes typed <see cref="OpenGlFrameQueues"/> for downstream passes.
/// </summary>
internal sealed class OpenGlSceneCollectionPass : RenderPass
{
    private readonly OpenGlRenderResources _resources;
    private readonly OpenGlFrameQueues _queues = new();
    private readonly HashSet<SceneNode> _trackedNodes = new();

    /// <inheritdoc/>
    public override RenderPassPhase Phase => RenderPassPhase.Collect;

    public OpenGlSceneCollectionPass(OpenGlRenderResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <inheritdoc/>
    protected override void ExecuteCore(RenderFrameContext context)
    {
        OpenGlRenderSettings settings = _resources.Settings;
        Matrix4x4 sceneAxisMatrix = GetSceneAxisMatrix(settings.UpAxis);

        _queues.Reset();
        _queues.SceneAxisMatrix = sceneAxisMatrix;
        ResolveDefaultLights(settings, sceneAxisMatrix, _queues.Lights);

        foreach (SceneNode node in context.Scene.EnumerateDescendants())
        {
            if (node.Flags.HasFlag(SceneNodeFlags.NoDraw))
            {
                continue;
            }

            ISceneNodeRenderHandle? handle = GetOrCreateNodeRenderHandle(node);
            if (handle is null)
            {
                continue;
            }

            handle.Update();

            switch (handle)
            {
                case OpenGlLightHandle lightHandle:
                    AppendLight(lightHandle, sceneAxisMatrix, _queues.Lights);
                    break;
                case OpenGlMeshHandle meshHandle:
                    _queues.Meshes.Add(meshHandle);
                    break;
                case OpenGlGridHandle gridHandle:
                    _queues.Grids.Add(gridHandle);
                    break;
                case OpenGlSkeletonBoneHandle boneHandle:
                    _queues.Bones.Add(boneHandle);
                    break;
            }
        }

        context.Set(_queues);
    }

    /// <summary>
    /// Releases all handles previously tracked by this pass. Called by the renderer
    /// when GPU resources should be rebuilt or when the renderer is disposed.
    /// </summary>
    public void ReleaseTrackedHandles()
    {
        foreach (SceneNode node in _trackedNodes)
        {
            if (node.GraphicsHandle is { } handle)
            {
                handle.Release();
                node.GraphicsHandle = null;
            }
        }

        _trackedNodes.Clear();
    }

    /// <summary>
    /// Disposes all handles previously tracked by this pass without performing GL teardown.
    /// </summary>
    public void DisposeTrackedHandles()
    {
        foreach (SceneNode node in _trackedNodes)
        {
            node.GraphicsHandle?.Dispose();
            node.GraphicsHandle = null;
        }

        _trackedNodes.Clear();
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        DisposeTrackedHandles();
    }

    private ISceneNodeRenderHandle? GetOrCreateNodeRenderHandle(SceneNode node)
    {
        if (node is Light light)
        {
            if (node.GraphicsHandle is OpenGlLightHandle existingLight && existingLight.IsOwnedBy(_resources.Settings))
            {
                return existingLight;
            }

            OpenGlLightHandle lightHandle = new(light, _resources.Settings);
            node.GraphicsHandle = lightHandle;
            _trackedNodes.Add(node);
            return lightHandle;
        }

        if (node.GraphicsHandle is OpenGlMeshHandle existingMesh && existingMesh.IsOwnedBy(_resources.Context))
        {
            return existingMesh;
        }

        if (node.GraphicsHandle is OpenGlGridHandle existingGrid && existingGrid.IsOwnedBy(_resources.Context))
        {
            return existingGrid;
        }

        if (node.GraphicsHandle is OpenGlSkeletonBoneHandle existingBone && existingBone.IsOwnedBy(_resources.Context))
        {
            return existingBone;
        }

        ISceneNodeRenderHandle? newHandle = node switch
        {
            Mesh mesh => new OpenGlMeshHandle(_resources.Context, mesh),
            Grid grid => new OpenGlGridHandle(_resources.Context, grid),
            SkeletonBone bone => new OpenGlSkeletonBoneHandle(_resources.Context, bone, _resources.Settings),
            _ => null,
        };

        if (newHandle is null)
        {
            return null;
        }

        node.GraphicsHandle = newHandle;
        _trackedNodes.Add(node);
        return newHandle;
    }

    private static void ResolveDefaultLights(OpenGlRenderSettings settings, Matrix4x4 sceneAxisMatrix, OpenGlLightFrameData lights)
    {
        Vector3 fallback = settings.FallbackLightDirection;
        if (fallback.LengthSquared() < 1e-10f)
        {
            fallback = -Vector3.UnitY;
        }

        Vector3 direction = TransformDirection(Vector3.Normalize(fallback), sceneAxisMatrix);
        lights.Add(direction, settings.FallbackLightIntensity, settings.FallbackLightColor);
    }

    private static void AppendLight(OpenGlLightHandle handle, Matrix4x4 sceneAxisMatrix, OpenGlLightFrameData lights)
    {
        if (!handle.Enabled)
        {
            return;
        }

        // The default fallback occupies index 0; replace it with the first real scene light.
        if (lights.Count == 1)
        {
            lights.Reset();
        }

        Vector3 direction = TransformDirection(handle.Direction, sceneAxisMatrix);
        lights.Add(direction, handle.Intensity, handle.Color);
    }

    private static Matrix4x4 GetSceneAxisMatrix(OpenGlUpAxis upAxis) => upAxis switch
    {
        OpenGlUpAxis.X => Matrix4x4.CreateRotationZ(MathF.PI / 2.0f),
        OpenGlUpAxis.Z => Matrix4x4.CreateRotationX(-MathF.PI / 2.0f),
        _ => Matrix4x4.Identity,
    };

    private static Vector3 TransformDirection(Vector3 direction, Matrix4x4 matrix)
    {
        Vector3 transformed = Vector3.TransformNormal(direction, matrix);
        if (transformed.LengthSquared() < 1e-10f)
        {
            return -Vector3.UnitY;
        }

        return Vector3.Normalize(transformed);
    }
}
