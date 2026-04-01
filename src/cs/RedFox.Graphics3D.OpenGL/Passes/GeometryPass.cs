using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class GeometryPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _meshShader = null!;
    private bool _initialized;

    public string Name => "Geometry";
    public bool Enabled { get; set; } = true;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        _meshShader = new GLShader(_gl, ShaderSource.MeshVertex, ShaderSource.MeshFragment);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled) return;

        _meshShader.Use();

        var camera = renderer.ActiveCamera;
        if (camera == null) return;

        var view = camera.GetViewMatrix();
        var proj = camera.GetProjectionMatrix();

        _meshShader.SetUniform("uView", view);
        _meshShader.SetUniform("uProjection", proj);
        _meshShader.SetUniform("uLightDir", new Vector3(0.5f, 0.8f, 0.3f));
        _meshShader.SetUniform("uLightColor", new Vector3(1.0f, 1.0f, 0.95f));
        _meshShader.SetUniform("uAmbientColor", new Vector3(0.15f, 0.15f, 0.18f));

        foreach (var mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            RenderMesh(renderer, mesh);
        }
    }

    private void RenderMesh(GLRenderer renderer, Mesh mesh)
    {
        var handle = renderer.GetOrCreateMeshHandle(mesh);
        if (handle == null) return;

        var model = mesh.GetActiveWorldMatrix();
        _meshShader.SetUniform("uModel", model);

        var normalMatrix = ComputeNormalMatrix(model);
        _meshShader.SetUniform("uNormalMatrix", normalMatrix);

        _meshShader.SetUniform("uHasSkinning", handle.HasSkinning);

        if (handle.HasSkinning && mesh.SkinnedBones != null)
        {
            var boneMatrices = ComputeBoneMatrices(mesh);
            _meshShader.SetUniformMatrix4Array("uBoneMatrices", boneMatrices);
        }

        var material = mesh.Materials?.FirstOrDefault();
        ApplyMaterial(renderer, material);

        _gl.BindVertexArray(handle.VAO);

        if (handle.IsIndexed)
        {
            _gl.DrawElements(PrimitiveType.Triangles, (uint)handle.IndexCount, DrawElementsType.UnsignedInt, 0);
        }
        else
        {
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)handle.VertexCount);
        }

        _gl.BindVertexArray(0);
    }

    private void ApplyMaterial(GLRenderer renderer, Material? material)
    {
        Vector4 diffuseColor = material?.DiffuseColor ?? new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
        _meshShader.SetUniform("uDiffuseColor", diffuseColor);

        bool hasDiffuseTex = false;
        if (material != null && material.TryGetDiffuseMap(out var texture))
        {
            var texHandle = renderer.GetOrCreateTextureHandle(texture);
            if (texHandle != null)
            {
                texHandle.Bind(0);
                _meshShader.SetUniform("uDiffuseTexture", 0);
                hasDiffuseTex = true;
            }
        }

        _meshShader.SetUniform("uHasDiffuseTexture", hasDiffuseTex);
    }

    private static Matrix4x4[] ComputeBoneMatrices(Mesh mesh)
    {
        if (mesh.SkinnedBones == null) return [];

        var count = Math.Min(mesh.SkinnedBones.Count, 128);
        var matrices = new Matrix4x4[128];

        for (int i = 0; i < count; i++)
        {
            var bone = mesh.SkinnedBones[i];
            mesh.EnsureInverseBindMatrices();
            var ibm = mesh.InverseBindMatrices?[i] ?? Matrix4x4.Identity;
            matrices[i] = ibm * bone.GetActiveWorldMatrix();
        }

        return matrices;
    }

    private static Matrix3x3 ComputeNormalMatrix(Matrix4x4 model)
    {
        if (Matrix4x4.Invert(model, out var inverseModel))
        {
            var transposed = Matrix4x4.Transpose(inverseModel);
            return new Matrix3x3(
                transposed.M11, transposed.M12, transposed.M13,
                transposed.M21, transposed.M22, transposed.M23,
                transposed.M31, transposed.M32, transposed.M33);
        }
        return Matrix3x3.Identity;
    }

    public void Dispose()
    {
        _meshShader?.Dispose();
    }
}

public readonly struct Matrix3x3(
    float m11, float m12, float m13,
    float m21, float m22, float m23,
    float m31, float m32, float m33)
{
    public static Matrix3x3 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);
}
