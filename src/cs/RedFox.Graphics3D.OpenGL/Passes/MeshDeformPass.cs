using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

/// <summary>
/// Prepass that performs GPU-accelerated mesh deformation (skinning + morph targets)
/// via a compute shader. Falls back to CPU morph evaluation when compute is unavailable.
/// </summary>
public sealed class MeshDeformPass : IRenderPass
{
    private GL _gl = null!;
    private GLComputeShader? _computeShader;
    private bool _initialized;

    public string Name => "MeshDeform";
    public PassPhase Phase => PassPhase.Prepass;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether compute-based deformation is available on this context.
    /// </summary>
    public bool ComputeAvailable => _computeShader is not null;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        _computeShader = TryCreateComputeShader(renderer);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            MeshRenderHandle? handle = renderer.GetOrCreateMeshHandle(mesh);
            if (handle is null || !handle.UseComputeDeform)
                continue;

            DispatchComputeDeform(handle);
        }
    }

    public void Dispose()
    {
        _computeShader?.Dispose();
        _computeShader = null;
    }

    private void DispatchComputeDeform(MeshRenderHandle handle)
    {
        if (_computeShader is null || handle.VertexCount == 0)
            return;

        _computeShader.Use();
        _computeShader.SetUniform("uVertexCount", handle.VertexCount);
        _computeShader.SetUniform("uMorphTargetCount", handle.MorphTargetCount);
        _computeShader.SetUniform("uHasMorphTargets", handle.HasMorphTargets && handle.MorphTargetCount > 0);
        _computeShader.SetUniform("uHasSkinning", handle.HasSkinning && handle.BoneCount > 0);

        handle.UploadComputeData(_gl);
        handle.BindComputeBuffers(_gl);

        uint groups = (uint)((handle.VertexCount + 63) / 64);
        _computeShader.Dispatch(groups, 1, 1);

        _gl.MemoryBarrier(MemoryBarrierMask.VertexAttribArrayBarrierBit);

        handle.UnbindComputeBuffers(_gl);
        _gl.UseProgram(0);
    }

    private static GLComputeShader? TryCreateComputeShader(GLRenderer renderer)
    {
        if (renderer.Settings.IsOpenGles)
            return null;

        GL gl = renderer.GL;
        gl.GetInteger(GLEnum.MajorVersion, out int major);
        gl.GetInteger(GLEnum.MinorVersion, out int minor);

        // Not a reliable check - Avalonia reports 4.1 on Windows but compute works?
        //if (major < 4 || (major == 4 && minor < 3))
        //    return null;

        try
        {
            string source = ShaderSource.LoadCompute("mesh_deform");
            return new GLComputeShader(gl, source);
        }
        catch
        {
            return null;
        }
    }
}
