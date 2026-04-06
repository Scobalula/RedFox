using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class ShadowMapPass : IRenderPass
{
    private const int DefaultShadowMapResolution = 2048;

    private static readonly IReadOnlyDictionary<ShadowQuality, int> ShadowResolutionByQuality = new Dictionary<ShadowQuality, int>
    {
        [ShadowQuality.Low] = 1024,
        [ShadowQuality.Medium] = 2048,
        [ShadowQuality.High] = 2048,
        [ShadowQuality.Ultra] = 4096,
    };

    private static readonly Vector3 DefaultLightDir = Vector3.Normalize(new Vector3(0.35f, -0.9f, 0.25f));

    private GL _gl = null!;
    private GLShader _shadowShader = null!;
    private ShadowFramebufferObject? _shadowFbo;
    private ShadowQuality _currentQuality;
    private bool _initialized;

    public string Name => "ShadowMap";
    public PassPhase Phase => PassPhase.Prepass;
    public bool Enabled { get; set; } = true;

    public uint ShadowMapTextureId => _shadowFbo?.DepthTextureId ?? 0;
    public Matrix4x4 LightSpaceMatrix { get; private set; } = Matrix4x4.Identity;
    public Vector3 ActiveLightDirection { get; private set; } = DefaultLightDir;
    public Vector3 ActiveLightColor { get; private set; } = new(1.0f, 0.98f, 0.94f);

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        (string vertSrc, string fragSrc) = ShaderSource.LoadProgram(_gl, "shadow");
        _shadowShader = new GLShader(_gl, vertSrc, fragSrc);

        _currentQuality = renderer.Settings.ShadowQuality;
        int resolution = GetResolutionForQuality(_currentQuality);
        _shadowFbo = new ShadowFramebufferObject(_gl);
        _shadowFbo.Initialize(resolution, resolution);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        RenderSettings settings = renderer.Settings;
        if (!settings.EnableShadows || settings.ActiveCamera is not Camera camera)
            return;

        EnsureShadowMapResolution(settings.ShadowQuality);

        Vector3 lightDir;
        if (settings.AutoDetectShadowLight && renderer.DominantLightDirection.HasValue)
        {
            lightDir = -renderer.DominantLightDirection.Value;
            ActiveLightColor = renderer.DominantLightColor;
        }
        else
        {
            lightDir = DefaultLightDir;
            ActiveLightColor = new Vector3(1.0f, 0.98f, 0.94f);
        }
        ActiveLightDirection = lightDir;
        (Vector3 sceneCenter, float sceneRadius) = ComputeSceneBounds(scene, settings);

        if (sceneRadius <= 0.0f)
            return;

        Matrix4x4 lightView = ComputeLightViewMatrix(lightDir, sceneCenter, sceneRadius);
        Matrix4x4 lightProj = ComputeLightProjectionMatrix(sceneCenter, sceneRadius, lightView);
        LightSpaceMatrix = lightView * lightProj;

        _gl.GetInteger(GLEnum.Viewport, out int savedVpX);
        int[] savedVp = new int[4];
        unsafe
        {
            fixed (int* ptr = savedVp)
                _gl.GetInteger(GLEnum.Viewport, ptr);
        }
        _gl.GetInteger(GLEnum.DrawFramebufferBinding, out int savedFbo);
        bool savedCullEnabled = _gl.IsEnabled(EnableCap.CullFace);
        _gl.GetInteger(GLEnum.CullFaceMode, out int savedCullFace);

        _shadowFbo!.BindForRendering();
        _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);

        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(GLEnum.Front);

        _shadowShader.Use();
        _shadowShader.SetUniform("uScene", settings.SceneTransform);
        _shadowShader.SetUniform("uLightViewProjection", LightSpaceMatrix);

        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
            RenderMeshShadow(renderer, mesh);

        _gl.CullFace((GLEnum)savedCullFace);
        if (!savedCullEnabled)
            _gl.Disable(EnableCap.CullFace);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)savedFbo);
        unsafe
        {
            _gl.Viewport(savedVp[0], savedVp[1], (uint)savedVp[2], (uint)savedVp[3]);
        }
    }

    public void Dispose()
    {
        _shadowShader?.Dispose();
        _shadowFbo?.Dispose();
    }

    private void EnsureShadowMapResolution(ShadowQuality quality)
    {
        if (_currentQuality == quality || _shadowFbo is null)
            return;

        int resolution = GetResolutionForQuality(quality);
        if (_shadowFbo.Width == resolution && _shadowFbo.Height == resolution)
        {
            _currentQuality = quality;
            return;
        }

        _shadowFbo.Dispose();
        _shadowFbo = new ShadowFramebufferObject(_gl);
        _shadowFbo.Initialize(resolution, resolution);
        _currentQuality = quality;
    }

    private static int GetResolutionForQuality(ShadowQuality quality) =>
        ShadowResolutionByQuality.TryGetValue(quality, out int res) ? res : DefaultShadowMapResolution;

    private void RenderMeshShadow(GLRenderer renderer, Mesh mesh)
    {
        MeshRenderHandle? handle = renderer.GetOrCreateMeshHandle(mesh);
        if (handle is null)
            return;

        handle.Update(renderer.GL, 0);

        Matrix4x4 model = mesh.GetActiveWorldMatrix();
        _shadowShader.SetUniform("uModel", model);
        _shadowShader.SetUniform("uHasSkinning", handle.HasSkinning);

        if (handle.HasSkinning)
        {
            handle.BindSkinningTextures(_gl);
            _shadowShader.SetUniform("uInfluenceTexture", 1);
            _shadowShader.SetUniform("uInfluenceTextureSize", new Vector2(handle.InfluenceTextureWidth, handle.InfluenceTextureHeight));
            _shadowShader.SetUniform("uBoneMatrixTexture", 2);
            _shadowShader.SetUniform("uBoneMatrixTextureSize", new Vector2(handle.BoneMatrixTextureWidth, handle.BoneMatrixTextureHeight));
        }

        handle.Draw(_gl);

        if (handle.HasSkinning)
            handle.UnbindSkinningTextures(_gl);
    }

    private static (Vector3 center, float radius) ComputeSceneBounds(Scene scene, RenderSettings settings)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        int count = 0;

        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            Matrix4x4 world = mesh.GetActiveWorldMatrix();
            Matrix4x4 fullTransform = world * settings.SceneTransform;
            Vector3 origin = Vector3.Transform(Vector3.Zero, fullTransform);
            min = Vector3.Min(min, origin);
            max = Vector3.Max(max, origin);
            count++;

            if (mesh.Positions is null)
                continue;

            int vertexCount = mesh.Positions.ElementCount;
            int step = Math.Max(1, vertexCount / 64);
            for (int i = 0; i < vertexCount; i += step)
            {
                float x = mesh.Positions.Get<float>(i, 0, 0);
                float y = mesh.Positions.Get<float>(i, 0, 1);
                float z = mesh.Positions.Get<float>(i, 0, 2);
                Vector3 wp = Vector3.Transform(new Vector3(x, y, z), fullTransform);
                min = Vector3.Min(min, wp);
                max = Vector3.Max(max, wp);
            }
        }

        if (count == 0)
            return (Vector3.Zero, 0.0f);

        Vector3 center = (min + max) * 0.5f;
        float radius = (max - min).Length() * 0.5f;
        return (center, MathF.Max(radius, 0.1f));
    }

    private static Matrix4x4 ComputeLightViewMatrix(Vector3 lightDir, Vector3 sceneCenter, float sceneRadius)
    {
        Vector3 lightPos = sceneCenter - lightDir * sceneRadius * 2.0f;

        Vector3 up = MathF.Abs(Vector3.Dot(lightDir, Vector3.UnitY)) > 0.99f
            ? Vector3.UnitZ
            : Vector3.UnitY;

        return Matrix4x4.CreateLookAt(lightPos, sceneCenter, up);
    }

    private static Matrix4x4 ComputeLightProjectionMatrix(Vector3 sceneCenter, float sceneRadius, Matrix4x4 lightView)
    {
        float extent = sceneRadius * 1.5f;
        float near = 0.01f;
        float far = sceneRadius * 5.0f;
        return CreateOrthographicGL(extent * 2.0f, extent * 2.0f, near, far);
    }

    /// <summary>
    /// Creates an orthographic projection that maps depth to [-1, 1] (OpenGL convention)
    /// instead of [0, 1] (System.Numerics / DirectX convention).
    /// </summary>
    private static Matrix4x4 CreateOrthographicGL(float width, float height, float near, float far)
    {
        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = 2.0f / width;
        result.M22 = 2.0f / height;
        result.M33 = 2.0f / (near - far);
        result.M43 = (near + far) / (near - far);
        return result;
    }
}
