using RedFox.Graphics3D.Rendering;
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
    /// <summary>
    /// Gets the mesh handles collected for the current frame.
    /// </summary>
    public List<OpenGlMeshHandle> Meshes { get; } = new();

    /// <summary>
    /// Gets the transparent grid handles collected for the current frame.
    /// </summary>
    public List<OpenGlGridHandle> Grids { get; } = new();

    /// <summary>
    /// Gets the skeleton bone handles collected for the current frame.
    /// </summary>
    public List<OpenGlSkeletonBoneHandle> Bones { get; } = new();

    /// <summary>
    /// Gets the active light snapshot for the current frame.
    /// </summary>
    public OpenGlLightFrameData Lights { get; } = new();

    /// <summary>
    /// Gets or sets the scene-axis transform matrix for the current frame.
    /// </summary>
    public Matrix4x4 SceneAxisMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// Clears all collected frame data.
    /// </summary>
    public void Reset()
    {
        Meshes.Clear();
        Grids.Clear();
        Bones.Clear();
        Lights.Reset();
        SceneAxisMatrix = Matrix4x4.Identity;
    }
}
