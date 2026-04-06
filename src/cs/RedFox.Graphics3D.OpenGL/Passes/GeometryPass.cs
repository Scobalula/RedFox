using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class GeometryPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _meshShader = null!;
    private uint _defaultWhiteTexture;
    private ShadowMapPass? _activeShadowPass;
    private bool _initialized;

    public string Name => "Geometry";
    public PassPhase Phase => PassPhase.Pass;
    public bool Enabled { get; set; } = true;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        (string meshVertex, string meshFragment) = ShaderSource.LoadProgram(_gl, "mesh");
        _meshShader = new GLShader(_gl, meshVertex, meshFragment);
        _defaultWhiteTexture = GlBufferOperations.CreateDefault1x1Texture(_gl, 255, 255, 255, 255);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        RenderSettings settings = renderer.Settings;
        if (settings.ActiveCamera is not Camera camera)
            return;

        _meshShader.Use();
        SetSceneUniforms(settings, camera);
        BindShadowMap(renderer, settings);

        bool wireframeApplied = ApplyGeometryPolygonMode(settings);
        try
        {
            foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
                RenderMesh(renderer, settings, mesh);
        }
        finally
        {
            RestoreGeometryPolygonMode(wireframeApplied);
        }
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

    private void SetSceneUniforms(RenderSettings settings, Camera camera)
    {
        _meshShader.SetUniform("uView", camera.GetViewMatrix());
        _meshShader.SetUniform("uProjection", camera.GetProjectionMatrix());
        _meshShader.SetUniform("uFarPlane", camera.FarPlane);
        _meshShader.SetUniform("uScene", settings.SceneTransform);
        _meshShader.SetUniform("uSceneNormalMatrix", settings.SceneNormalMatrix);
        _meshShader.SetUniform("uCameraPos", camera.Position);
        _meshShader.SetUniform("uCameraWorldPos", ComputePreSceneCameraPosition(camera.Position, settings.SceneTransform));
        _meshShader.SetUniform("uSkyColor", new Vector3(0.24f, 0.31f, 0.40f));
        _meshShader.SetUniform("uGroundColor", new Vector3(0.09f, 0.08f, 0.07f));
        _meshShader.SetUniform("uAmbientStrength", 0.85f);
        _meshShader.SetUniform("uEnvironmentMapExposure", settings.EnvironmentMapExposure);
        _meshShader.SetUniform("uEnvironmentMapIntensity", settings.EnvironmentMapReflectionIntensity);
        _meshShader.SetUniform("uShadingMode", (int)settings.ShadingMode);
    }

    private void BindShadowMap(GLRenderer renderer, RenderSettings settings)
    {
        bool shadowsEnabled = settings.EnableShadows;
        ShadowMapPass? shadowPass = shadowsEnabled ? renderer.GetPass<ShadowMapPass>() : null;
        bool hasShadowMap = shadowPass is not null && shadowPass.ShadowMapTextureId != 0;

        _activeShadowPass = hasShadowMap ? shadowPass : null;
        _meshShader.SetUniform("uEnableShadows", hasShadowMap);

        if (hasShadowMap)
        {
            _meshShader.SetUniform("uLightDir", shadowPass!.ActiveLightDirection);
            _meshShader.SetUniform("uLightColor", shadowPass.ActiveLightColor);
            _meshShader.SetUniform("uLightSpaceMatrix", shadowPass.LightSpaceMatrix);
            _meshShader.SetUniform("uShadowQuality", (int)settings.ShadowQuality);
            _meshShader.SetUniform("uShadowSoftness", settings.ShadowSoftness);
            _meshShader.SetUniform("uShadowIntensity", settings.ShadowIntensity);
            _meshShader.SetUniform("uShadowMap", 12);
        }
        else
        {
            _meshShader.SetUniform("uLightDir", Vector3.Normalize(new Vector3(0.35f, -0.9f, 0.25f)));
            _meshShader.SetUniform("uLightColor", new Vector3(1.0f, 0.98f, 0.94f));
        }
    }

    private void RenderMesh(GLRenderer renderer, RenderSettings settings, Mesh mesh)
    {
        MeshRenderHandle? handle = renderer.GetOrCreateMeshHandle(mesh);
        if (handle is null)
            return;

        Matrix4x4 model = mesh.GetActiveWorldMatrix();
        _meshShader.SetUniform("uModel", model);
        _meshShader.SetUniform("uNormalMatrix", Matrix3x3.FromModelMatrix(model));
        _meshShader.SetUniform("uHasNormals", handle.HasNormals);

        Material? material = mesh.Materials?.FirstOrDefault();
        ConfigureCullState(settings, handle, material, model);
        ApplyMaterial(renderer, settings, material);

        if (_activeShadowPass is not null)
        {
            _gl.ActiveTexture(TextureUnit.Texture12);
            _gl.BindTexture(TextureTarget.Texture2D, _activeShadowPass.ShadowMapTextureId);
        }

        handle.Draw(_gl);
    }

    private bool ApplyGeometryPolygonMode(RenderSettings settings)
    {
        if (!settings.ShowWireframe || settings.IsOpenGles)
            return false;

        _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
        return true;
    }

    private void RestoreGeometryPolygonMode(bool wireframeApplied)
    {
        if (!wireframeApplied)
            return;

        _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
    }

    private void ConfigureCullState(RenderSettings settings, MeshRenderHandle handle, Material? material, Matrix4x4 model)
    {
        bool flipsWinding = model.GetDeterminant() * settings.SceneTransform.GetDeterminant() < 0.0f;
        bool frontFaceClockwise = handle.FrontFaceClockwise ^ flipsWinding;
        _gl.FrontFace(frontFaceClockwise ? (FrontFaceDirection)0x0900 : FrontFaceDirection.Ccw);

        bool cullBackFaces = settings.EnableBackFaceCulling &&
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

    private void ApplyMaterial(GLRenderer renderer, RenderSettings settings, Material? material)
    {
        BindDefaultTextures();
        ApplyDiffuse(renderer, material);
        ApplyEnvironment(renderer, settings);
        ApplyPbrFactors(material);
        ApplyMaterialTextures(renderer, material);
    }

    private void BindDefaultTextures()
    {
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
        if (material is not null && material.TryGetDiffuseMap(out Texture? texture))
        {
            GLTextureHandle? texHandle = renderer.GetOrCreateTextureHandle(texture);
            if (texHandle is not null)
            {
                texHandle.Bind(0);
                _meshShader.SetUniform("uDiffuseTexture", 0);
                hasDiffuseTex = true;
            }
        }

        _meshShader.SetUniform("uHasDiffuseTexture", hasDiffuseTex);
    }

    private void ApplyEnvironment(GLRenderer renderer, RenderSettings settings)
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

        bool useIBL = settings.EnableIBL &&
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
        (float metallic, float roughness) = PbrMaterialFactors.Resolve(material);
        _meshShader.SetUniform("uMetallicFactor", metallic);
        _meshShader.SetUniform("uRoughnessFactor", roughness);
        _meshShader.SetUniform("uDoubleSided", material?.DoubleSided ?? false);

        bool hasLegacySpecular = material?.SpecularColor.HasValue == true || material?.SpecularStrength.HasValue == true;
        _meshShader.SetUniform("uSpecularColor", ExtractRgb(material?.SpecularColor ?? Vector4.One));
        _meshShader.SetUniform("uSpecularStrength", material?.SpecularStrength ?? (hasLegacySpecular ? 1.0f : 0.0f));
    }

    private void ApplyMaterialTextures(GLRenderer renderer, Material? material)
    {
        bool hasMrTexture = TryBindMaterialTexture(renderer, material, static (Material m, out Texture? t) => m.TryGetMetallicMap(out t), 7, "uMetallicRoughnessTexture");
        _meshShader.SetUniform("uHasMetallicRoughnessTexture", hasMrTexture);

        bool hasRoughnessTexture = TryBindMaterialTexture(renderer, material, static (Material m, out Texture? t) => m.TryGetRoughnessMap(out t), 9, "uRoughnessTexture");
        _meshShader.SetUniform("uHasRoughnessTexture", hasRoughnessTexture);

        bool hasGlossTexture = false;
        if (!hasRoughnessTexture)
            hasGlossTexture = TryBindMaterialTexture(renderer, material, static (Material m, out Texture? t) => m.TryGetGlossMap(out t), 10, "uGlossTexture");
        _meshShader.SetUniform("uHasGlossTexture", hasGlossTexture);

        bool hasSpecularTexture = TryBindMaterialTexture(renderer, material, static (Material m, out Texture? t) => m.TryGetSpecularMap(out t), 11, "uSpecularTexture");
        _meshShader.SetUniform("uHasSpecularTexture", hasSpecularTexture);
        _meshShader.SetUniform(
            "uUseLegacySpecular",
            hasSpecularTexture || material?.SpecularColor.HasValue == true || material?.SpecularStrength.HasValue == true);

        bool hasAoTexture = TryBindMaterialTexture(renderer, material, static (Material m, out Texture? t) => m.TryGetAmbientOcclusionMap(out t), 8, "uAoTexture");
        _meshShader.SetUniform("uHasAoTexture", hasAoTexture);
    }

    private delegate bool TryGetTexture(Material material, out Texture? texture);

    private bool TryBindMaterialTexture(GLRenderer renderer, Material? material, TryGetTexture getter, uint unit, string uniformName)
    {
        if (material is not null && getter(material, out Texture? texture) && texture is not null)
        {
            GLTextureHandle? texHandle = renderer.GetOrCreateTextureHandle(texture);
            if (texHandle is not null)
            {
                texHandle.Bind(unit);
                _meshShader.SetUniform(uniformName, (int)unit);
                return true;
            }
        }

        return false;
    }

    private static Vector3 ExtractRgb(Vector4 color) => new(color.X, color.Y, color.Z);

    private static Vector3 ComputePreSceneCameraPosition(Vector3 sceneSpaceCameraPosition, Matrix4x4 sceneTransform)
    {
        if (!Matrix4x4.Invert(sceneTransform, out Matrix4x4 inverseScene))
            return sceneSpaceCameraPosition;

        return Vector3.Transform(sceneSpaceCameraPosition, inverseScene);
    }
}
