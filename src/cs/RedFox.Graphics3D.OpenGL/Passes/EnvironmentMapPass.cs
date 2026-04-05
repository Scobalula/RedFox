using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class EnvironmentMapPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _shader = null!;
    private uint _cubeVao;
    private uint _cubeVbo;
    private bool _initialized;

    public string Name => "EnvironmentMap";
    public PassPhase Phase => PassPhase.Pass;
    public bool Enabled { get; set; } = true;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        (string vertexSource, string fragmentSource) = ShaderSource.LoadProgram(_gl, "envmap");
        _shader = new GLShader(_gl, vertexSource, fragmentSource);

        _cubeVao = _gl.GenVertexArray();
        _cubeVbo = _gl.GenBuffer();

        unsafe
        {
            _gl.BindVertexArray(_cubeVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _cubeVbo);
            fixed (float* ptr = CubeGeometry.Vertices)
            {
                _gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(CubeGeometry.Vertices.Length * sizeof(float)),
                    ptr,
                    BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        }

        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        RenderSettings settings = renderer.Settings;
        if (!settings.ShowSkybox)
            return;

        GLEnvironmentResources? environment = renderer.EnvironmentResources;
        if (environment is null || environment.SkyCubemap.TextureId == 0)
            return;

        Camera? camera = settings.ActiveCamera;
        if (camera is null)
            return;

        _gl.DepthMask(false);
        _gl.Disable(GLEnum.DepthTest);
        _gl.Disable(EnableCap.CullFace);

        _shader.Use();

        _shader.SetUniform("uProjection", camera.GetProjectionMatrix());
        _shader.SetUniform("uView", camera.GetViewMatrix());
        _shader.SetUniform("uExposure", settings.EnvironmentMapExposure);
        _shader.SetUniform("uBlurEnabled", settings.EnvironmentMapBlurEnabled);

        uint cubemapTextureId = environment.SkyCubemap.TextureId;
        float blurMip = 0.0f;
        if (settings.EnvironmentMapBlurEnabled)
        {
            if (environment.PrefilterCubemap.TextureId != 0)
            {
                cubemapTextureId = environment.PrefilterCubemap.TextureId;
                blurMip = Math.Clamp(settings.EnvironmentMapBlurRadius, 0.0f, environment.PrefilterMaxMipLevel);
            }
            else
            {
                blurMip = Math.Clamp(settings.EnvironmentMapBlurRadius, 0.0f, environment.SkyMaxMipLevel);
            }
        }

        _shader.SetUniform("uBlurMipLevel", blurMip);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(GLEnum.TextureCubeMap, cubemapTextureId);
        _shader.SetUniform("uSkyCubemap", 0);

        _gl.BindVertexArray(_cubeVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
        _gl.BindVertexArray(0);

        _gl.BindTexture(GLEnum.TextureCubeMap, 0);

        _gl.Enable(GLEnum.DepthTest);
        _gl.DepthMask(true);
    }

    public void Dispose()
    {
        _shader?.Dispose();

        try
        {
            if (_cubeVao != 0)
                _gl.DeleteVertexArray(_cubeVao);

            if (_cubeVbo != 0)
                _gl.DeleteBuffer(_cubeVbo);
        }
        catch
        {
        }
    }
}
