using RedFox.Rendering.OpenGL.Handles;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Scene-frame queues built by <see cref="Passes.OpenGlSceneCollectionPass"/> and
/// consumed by subsequent passes. Published as a frame service via
/// <see cref="RenderFrameContext.Set{TService}"/>.
/// </summary>
internal sealed class OpenGlFrameQueues
{
    public List<OpenGlMeshHandle> Meshes { get; } = new();

    public List<OpenGlGridHandle> Grids { get; } = new();

    public List<OpenGlSkeletonBoneHandle> Bones { get; } = new();

    public OpenGlLightFrameData Lights { get; } = new();

    public Matrix4x4 SceneAxisMatrix { get; set; } = Matrix4x4.Identity;

    public void Reset()
    {
        Meshes.Clear();
        Grids.Clear();
        Bones.Clear();
        Lights.Reset();
        SceneAxisMatrix = Matrix4x4.Identity;
    }
}
