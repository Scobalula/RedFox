using System.Numerics;
using RedFox.Graphics3D.OpenGL;
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
        _meshShader.SetUniform("uCameraWorldPos", ComputePreSceneCameraPosition(camera.Position, renderer.SceneTransform));
        _meshShader.SetUniform("uLightDir", Vector3.Normalize(new Vector3(0.35f, -0.9f, 0.25f)));
        _meshShader.SetUniform("uLightColor", new Vector3(1.0f, 0.98f, 0.94f));
        _meshShader.SetUniform("uSkyColor", new Vector3(0.24f, 0.31f, 0.40f));
        _meshShader.SetUniform("uGroundColor", new Vector3(0.09f, 0.08f, 0.07f));
        _meshShader.SetUniform("uAmbientStrength", 0.85f);
        _meshShader.SetUniform("uEnvironmentMapExposure", renderer.EnvironmentMapExposure);
        _meshShader.SetUniform("uEnvironmentMapIntensity", renderer.EnvironmentMapReflectionIntensity);

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
        ConfigureCullState(renderer, handle, material, model);
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

    private void ConfigureCullState(GLRenderer renderer, GLMeshHandle handle, Material? material, Matrix4x4 model)
    {
        bool flipsWinding = model.GetDeterminant() * renderer.SceneTransform.GetDeterminant() < 0.0f;
        bool frontFaceClockwise = handle.FrontFaceClockwise ^ flipsWinding;
        _gl.FrontFace(frontFaceClockwise ? (FrontFaceDirection)0x0900 : FrontFaceDirection.Ccw);

        bool cullBackFaces = renderer.EnableBackFaceCulling &&
                            handle.HasConsistentWinding &&
                            !handle.HasSkinning &&
                            !(material?.DoubleSided ?? false);
        if (cullBackFaces)
        {
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(GLEnum.Back);
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }
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

        GLEnvironmentResources? env = renderer.EnvironmentResources;
        bool hasSkyMap = env is not null && env.SkyCubemap.TextureId != 0;
        _meshShader.SetUniform("uHasSkyMap", hasSkyMap);
        if (hasSkyMap && env is not null)
        {
            _gl.ActiveTexture(TextureUnit.Texture3);
            _gl.BindTexture(TextureTarget.TextureCubeMap, env.SkyCubemap.TextureId);
            _meshShader.SetUniform("uSkyMap", 3);
            _meshShader.SetUniform("uSkyMaxMipLevel", env.SkyMaxMipLevel);
        }

        // IBL textures and settings
        bool useIBL = renderer.EnableIBL &&
                      env is not null &&
                      env.IrradianceCubemap.TextureId != 0 &&
                      env.PrefilterCubemap.TextureId != 0 &&
                      env.BrdfLutTexture != 0;
        _meshShader.SetUniform("uUseIBL", useIBL);

        if (useIBL && env is not null)
        {
            // Bind irradiance cubemap (texture unit 4)
            _gl.ActiveTexture(TextureUnit.Texture4);
            _gl.BindTexture(TextureTarget.TextureCubeMap, env.IrradianceCubemap.TextureId);
            _meshShader.SetUniform("uIrradianceMap", 4);

            // Bind prefiltered environment cubemap (texture unit 5)
            _gl.ActiveTexture(TextureUnit.Texture5);
            _gl.BindTexture(TextureTarget.TextureCubeMap, env.PrefilterCubemap.TextureId);
            _meshShader.SetUniform("uPrefilterMap", 5);
            _meshShader.SetUniform("uPrefilterMaxMipLevel", env.PrefilterMaxMipLevel);

            // Bind BRDF LUT (texture unit 6)
            _gl.ActiveTexture(TextureUnit.Texture6);
            _gl.BindTexture(TextureTarget.Texture2D, env.BrdfLutTexture);
            _meshShader.SetUniform("uBrdfLut", 6);
        }

        (float metallicFactor, float roughnessFactor) = PbrMaterialFactors.Resolve(material);
        _meshShader.SetUniform("uMetallicFactor", metallicFactor);
        _meshShader.SetUniform("uRoughnessFactor", roughnessFactor);
        _meshShader.SetUniform("uDoubleSided", material?.DoubleSided ?? false);
        _meshShader.SetUniform("uSpecularColor", ExtractRgb(material?.SpecularColor ?? new Vector4(1.0f)));
        _meshShader.SetUniform("uSpecularStrength", material?.SpecularStrength ?? 1.0f);

        bool hasMrTexture = false;
        if (material is not null && material.TryGetMetallicMap(out var metallicRoughnessTexture))
        {
            var texHandle = renderer.GetOrCreateTextureHandle(metallicRoughnessTexture);
            if (texHandle is not null)
            {
                texHandle.Bind(7);
                _meshShader.SetUniform("uMetallicRoughnessTexture", 7);
                hasMrTexture = true;
            }
        }
        _meshShader.SetUniform("uHasMetallicRoughnessTexture", hasMrTexture);

        bool hasRoughnessTexture = false;
        if (material is not null && material.TryGetRoughnessMap(out var roughnessTexture))
        {
            var texHandle = renderer.GetOrCreateTextureHandle(roughnessTexture);
            if (texHandle is not null)
            {
                texHandle.Bind(9);
                _meshShader.SetUniform("uRoughnessTexture", 9);
                hasRoughnessTexture = true;
            }
        }
        _meshShader.SetUniform("uHasRoughnessTexture", hasRoughnessTexture);

        bool hasGlossTexture = false;
        if (!hasRoughnessTexture && material is not null && material.TryGetGlossMap(out var glossTexture))
        {
            var texHandle = renderer.GetOrCreateTextureHandle(glossTexture);
            if (texHandle is not null)
            {
                texHandle.Bind(10);
                _meshShader.SetUniform("uGlossTexture", 10);
                hasGlossTexture = true;
            }
        }
        _meshShader.SetUniform("uHasGlossTexture", hasGlossTexture);

        bool hasSpecularTexture = false;
        if (material is not null && material.TryGetSpecularMap(out var specularTexture))
        {
            var texHandle = renderer.GetOrCreateTextureHandle(specularTexture);
            if (texHandle is not null)
            {
                texHandle.Bind(11);
                _meshShader.SetUniform("uSpecularTexture", 11);
                hasSpecularTexture = true;
            }
        }
        _meshShader.SetUniform("uHasSpecularTexture", hasSpecularTexture);
        _meshShader.SetUniform(
            "uUseLegacySpecular",
            hasSpecularTexture || material?.SpecularColor.HasValue == true || material?.SpecularStrength.HasValue == true);

        bool hasAoTexture = false;
        if (material is not null && material.TryGetAmbientOcclusionMap(out var aoTexture))
        {
            var texHandle = renderer.GetOrCreateTextureHandle(aoTexture);
            if (texHandle is not null)
            {
                texHandle.Bind(8);
                _meshShader.SetUniform("uAoTexture", 8);
                hasAoTexture = true;
            }
        }
        _meshShader.SetUniform("uHasAoTexture", hasAoTexture);
    }

    private static Vector3 ExtractRgb(Vector4 color) => new(color.X, color.Y, color.Z);

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

    private static Vector3 ComputePreSceneCameraPosition(Vector3 sceneSpaceCameraPosition, Matrix4x4 sceneTransform)
    {
        if (!Matrix4x4.Invert(sceneTransform, out Matrix4x4 inverseScene))
            return sceneSpaceCameraPosition;

        return Vector3.Transform(sceneSpaceCameraPosition, inverseScene);
    }

    public void Dispose()
    {
        _meshShader?.Dispose();
    }
}
