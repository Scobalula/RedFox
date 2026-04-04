using System.Numerics;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class GeometryPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _meshShader = null!;
    private uint _defaultWhiteTexture;
    private bool _initialized;

    public string Name => "Geometry";
    public bool Enabled { get; set; } = true;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        (string meshVertex, string meshFragment) = ShaderSource.LoadProgram(_gl, "mesh");
        _meshShader = new GLShader(_gl, meshVertex, meshFragment);
        _defaultWhiteTexture = CreateDefault1x1Texture(_gl, 255, 255, 255, 255);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled) return;

        var camera = renderer.ActiveCamera;
        if (camera == null) return;

        _meshShader.Use();
        SetSceneUniforms(renderer, camera);

        foreach (var mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            RenderMesh(renderer, mesh);
        }
    }

    private void SetSceneUniforms(GLRenderer renderer, Camera camera)
    {
        _meshShader.SetUniform("uView", camera.GetViewMatrix());
        _meshShader.SetUniform("uProjection", camera.GetProjectionMatrix());
        _meshShader.SetUniform("uFarPlane", camera.FarPlane);
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
            BindSkinningTextures(renderer, mesh, handle);

        var material = mesh.Materials?.FirstOrDefault();
        ConfigureCullState(renderer, handle, material, model);
        ApplyMaterial(renderer, material);

        _gl.BindVertexArray(handle.VAO);

        if (handle.IsIndexed)
        {
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)handle.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
            }
        }
        else
        {
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)handle.VertexCount);
        }

        _gl.BindVertexArray(0);

        if (handle.HasSkinning)
            UnbindSkinningTextures();
    }

    // ------------------------------------------------------------------
    // Skinning
    // ------------------------------------------------------------------

    private void BindSkinningTextures(GLRenderer renderer, Mesh mesh, GLMeshHandle handle)
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

    private void UnbindSkinningTextures()
    {
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    // ------------------------------------------------------------------
    // Culling
    // ------------------------------------------------------------------

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

    // ------------------------------------------------------------------
    // Material
    // ------------------------------------------------------------------

    private void ApplyMaterial(GLRenderer renderer, Material? material)
    {
        BindDefaultTextures();
        ApplyDiffuse(renderer, material);
        ApplyEnvironment(renderer);
        ApplyPbrFactors(material);
        ApplyMaterialTextures(renderer, material);
    }

    private void BindDefaultTextures()
    {
        // Bind a 1x1 white texture to every material sampler unit so that
        // unbound samplers always reference a valid GL object. This prevents
        // undefined behaviour on drivers that evaluate both ternary branches
        // or that read from default-unit-0 for unset sampler uniforms.
        ReadOnlySpan<uint> units = [0, 7, 8, 9, 10, 11];
        ReadOnlySpan<string> names =
        [
            "uDiffuseTexture",
            "uMetallicRoughnessTexture",
            "uAoTexture",
            "uRoughnessTexture",
            "uGlossTexture",
            "uSpecularTexture"
        ];

        for (int i = 0; i < units.Length; i++)
        {
            _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)units[i]));
            _gl.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);
            _meshShader.SetUniform(names[i], (int)units[i]);
        }
    }

    private void ApplyDiffuse(GLRenderer renderer, Material? material)
    {
        Vector4 diffuseColor = material?.DiffuseColor ?? Vector4.One;
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

    private void ApplyEnvironment(GLRenderer renderer)
    {
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

        bool useIBL = renderer.EnableIBL &&
                      env is not null &&
                      env.IrradianceCubemap.TextureId != 0 &&
                      env.PrefilterCubemap.TextureId != 0 &&
                      env.BrdfLutTexture != 0;
        _meshShader.SetUniform("uUseIBL", useIBL);

        if (useIBL && env is not null)
        {
            _gl.ActiveTexture(TextureUnit.Texture4);
            _gl.BindTexture(TextureTarget.TextureCubeMap, env.IrradianceCubemap.TextureId);
            _meshShader.SetUniform("uIrradianceMap", 4);

            _gl.ActiveTexture(TextureUnit.Texture5);
            _gl.BindTexture(TextureTarget.TextureCubeMap, env.PrefilterCubemap.TextureId);
            _meshShader.SetUniform("uPrefilterMap", 5);
            _meshShader.SetUniform("uPrefilterMaxMipLevel", env.PrefilterMaxMipLevel);

            _gl.ActiveTexture(TextureUnit.Texture6);
            _gl.BindTexture(TextureTarget.Texture2D, env.BrdfLutTexture);
            _meshShader.SetUniform("uBrdfLut", 6);
        }
    }

    private void ApplyPbrFactors(Material? material)
    {
        (float metallicFactor, float roughnessFactor) = PbrMaterialFactors.Resolve(material);
        _meshShader.SetUniform("uMetallicFactor", 0f);
        _meshShader.SetUniform("uRoughnessFactor", 0.5f);
        _meshShader.SetUniform("uDoubleSided", material?.DoubleSided ?? false);

        bool hasLegacySpecular = material?.SpecularColor.HasValue == true || material?.SpecularStrength.HasValue == true;
        _meshShader.SetUniform("uSpecularColor", ExtractRgb(material?.SpecularColor ?? Vector4.One));
        _meshShader.SetUniform("uSpecularStrength", material?.SpecularStrength ?? (hasLegacySpecular ? 1.0f : 0.0f));
    }

    private void ApplyMaterialTextures(GLRenderer renderer, Material? material)
    {
        bool hasMrTexture = TryBindMaterialTexture(renderer, material, (m, out t) => m.TryGetMetallicMap(out t), 7, "uMetallicRoughnessTexture");
        _meshShader.SetUniform("uHasMetallicRoughnessTexture", hasMrTexture);

        bool hasRoughnessTexture = TryBindMaterialTexture(renderer, material, (m, out t) => m.TryGetRoughnessMap(out t), 9, "uRoughnessTexture");
        _meshShader.SetUniform("uHasRoughnessTexture", hasRoughnessTexture);

        bool hasGlossTexture = false;
        if (!hasRoughnessTexture)
            hasGlossTexture = TryBindMaterialTexture(renderer, material, (m, out t) => m.TryGetGlossMap(out t), 10, "uGlossTexture");
        _meshShader.SetUniform("uHasGlossTexture", hasGlossTexture);

        bool hasSpecularTexture = TryBindMaterialTexture(renderer, material, (m, out t) => m.TryGetSpecularMap(out t), 11, "uSpecularTexture");
        _meshShader.SetUniform("uHasSpecularTexture", hasSpecularTexture);
        _meshShader.SetUniform("uUseLegacySpecular",
            hasSpecularTexture || material?.SpecularColor.HasValue == true || material?.SpecularStrength.HasValue == true);

        bool hasAoTexture = TryBindMaterialTexture(renderer, material, (m, out t) => m.TryGetAmbientOcclusionMap(out t), 8, "uAoTexture");
        _meshShader.SetUniform("uHasAoTexture", hasAoTexture);
    }

    private delegate bool TryGetTexture(Material material, out Texture? texture);

    private bool TryBindMaterialTexture(GLRenderer renderer, Material? material, TryGetTexture getter, uint unit, string uniformName)
    {
        if (material is not null && getter(material, out var texture) && texture is not null)
        {
            var texHandle = renderer.GetOrCreateTextureHandle(texture);
            if (texHandle is not null)
            {
                texHandle.Bind(unit);
                _meshShader.SetUniform(uniformName, (int)unit);
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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

    private static unsafe uint CreateDefault1x1Texture(GL gl, byte r, byte g, byte b, byte a)
    {
        uint tex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, tex);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        byte* pixel = stackalloc byte[4] { r, g, b, a };
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }

    public void Dispose()
    {
        _meshShader?.Dispose();
        if (_defaultWhiteTexture != 0)
        {
            try { _gl.DeleteTexture(_defaultWhiteTexture); } catch { }
            _defaultWhiteTexture = 0;
        }
    }
}
