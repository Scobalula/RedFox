using System.Numerics;
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
        (string meshVertex, string meshFragment) = ShaderSource.LoadProgram(_gl, "mesh");
        _meshShader = new GLShader(_gl, meshVertex, meshFragment);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled) return;

        var camera = renderer.ActiveCamera;
        if (camera == null) return;

        _meshShader.Use();

        var view = camera.GetViewMatrix();
        var proj = camera.GetProjectionMatrix();

        _meshShader.SetUniform("uView", view);
        _meshShader.SetUniform("uProjection", proj);
        _meshShader.SetUniform("uScene", renderer.SceneTransform);
        _meshShader.SetUniform("uSceneNormalMatrix", renderer.SceneNormalMatrix);
        _meshShader.SetUniform("uCameraPos", camera.Position);
        _meshShader.SetUniform("uLightDir", Vector3.Normalize(new Vector3(0.35f, -0.9f, 0.25f)));
        _meshShader.SetUniform("uLightColor", new Vector3(1.0f, 0.98f, 0.94f));
        _meshShader.SetUniform("uSkyColor", new Vector3(0.24f, 0.31f, 0.40f));
        _meshShader.SetUniform("uGroundColor", new Vector3(0.09f, 0.08f, 0.07f));
        _meshShader.SetUniform("uAmbientStrength", 0.85f);

        foreach (var mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            RenderMesh(renderer, mesh);
        }
    }

    private void RenderMesh(GLRenderer renderer, Mesh mesh)
    {
        var handle = renderer.GetOrCreateMeshHandle(mesh);
        if (handle == null) return;

        renderer.UpdateDynamicMeshData(mesh, handle);

        var model = mesh.GetActiveWorldMatrix();
        _meshShader.SetUniform("uModel", model);
        _meshShader.SetUniform("uNormalMatrix", ComputeNormalMatrix(model));
        _meshShader.SetUniform("uHasNormals", handle.HasNormals);
        _meshShader.SetUniform("uHasSkinning", handle.HasSkinning);

        if (handle.HasSkinning)
        {
            renderer.UpdateSkinningData(mesh, handle);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, handle.InfluenceTexture);
            _meshShader.SetUniform("uInfluenceTexture", 1);
            _meshShader.SetUniform("uInfluenceTextureSize", new Vector2(handle.InfluenceTextureWidth, handle.InfluenceTextureHeight));

            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, handle.BoneMatrixTexture);
            _meshShader.SetUniform("uBoneMatrixTexture", 2);
            _meshShader.SetUniform("uBoneMatrixTextureSize", new Vector2(handle.BoneMatrixTextureWidth, handle.BoneMatrixTextureHeight));
        }

        var material = mesh.Materials?.FirstOrDefault();
        ApplyMaterial(renderer, material);

        _gl.BindVertexArray(handle.VAO);

        if (handle.IsIndexed)
        {
            unsafe
            {
                // With an element buffer bound, OpenGL expects a byte offset here.
                _gl.DrawElements(PrimitiveType.Triangles, (uint)handle.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
            }
        }
        else
        {
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)handle.VertexCount);
        }

        _gl.BindVertexArray(0);
        if (handle.HasSkinning)
        {
            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.ActiveTexture(TextureUnit.Texture0);
        }
    }

    private void ApplyMaterial(GLRenderer renderer, Material? material)
    {
        Vector4 diffuseColor = material?.DiffuseColor ?? new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
        Vector4 specularColor = material?.SpecularColor ?? Vector4.One;
        float specularStrength = MathF.Max(material?.SpecularStrength ?? 0.28f, 0.0f);
        float shininess = MathF.Max(material?.Shininess ?? 32.0f, 1.0f);

        _meshShader.SetUniform("uDiffuseColor", diffuseColor);
        _meshShader.SetUniform("uSpecularColor", new Vector3(specularColor.X, specularColor.Y, specularColor.Z));
        _meshShader.SetUniform("uSpecularStrength", specularStrength);
        _meshShader.SetUniform("uShininess", shininess);

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

        bool hasEnvMap = renderer.EnvironmentMap?.TextureHandle is not null;
        if (hasEnvMap)
        {
            renderer.EnvironmentMap!.TextureHandle!.Bind(3);
            _meshShader.SetUniform("uEnvironmentMap", 3);
        }

        _meshShader.SetUniform("uHasEnvironmentMap", hasEnvMap);
        _meshShader.SetUniform("uEnvironmentMapIntensity", renderer.EnvironmentMapReflectionIntensity);

        // IBL textures and settings
        bool useIBL = hasEnvMap && renderer.IblPrecomputePass is not null && renderer.IblPrecomputePass.Computed;
        _meshShader.SetUniform("uUseIBL", useIBL);

        if (useIBL && renderer.IblPrecomputePass is not null)
        {
            // Bind irradiance cubemap (texture unit 4)
            _gl.ActiveTexture(TextureUnit.Texture4);
            _gl.BindTexture(TextureTarget.TextureCubeMap, renderer.IblPrecomputePass.IrradianceCubemap);
            _meshShader.SetUniform("uIrradianceMap", 4);

            // Bind prefiltered environment cubemap (texture unit 5)
            _gl.ActiveTexture(TextureUnit.Texture5);
            _gl.BindTexture(TextureTarget.TextureCubeMap, renderer.IblPrecomputePass.PrefilterCubemap);
            _meshShader.SetUniform("uPrefilterMap", 5);
            _meshShader.SetUniform("uPrefilterMaxMipLevel", renderer.IblPrecomputePass.PrefilterMaxMipLevel);

            // Bind BRDF LUT (texture unit 6)
            _gl.ActiveTexture(TextureUnit.Texture6);
            _gl.BindTexture(TextureTarget.Texture2D, renderer.IblPrecomputePass.BrdfLutTexture);
            _meshShader.SetUniform("uBrdfLut", 6);
        }

        // Material IBL properties (defaults for now, could come from material later)
        float metallic = material?.Metallic ?? 0.0f;
        float roughness = material?.Roughness ?? 0.5f;
        _meshShader.SetUniform("uMetallic", metallic);
        _meshShader.SetUniform("uRoughness", roughness);
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
