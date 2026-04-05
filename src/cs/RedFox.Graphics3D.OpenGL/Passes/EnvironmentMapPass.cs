using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

/// <summary>
/// A render pass that renders the precomputed environment cubemap as a background skybox.
/// Optional blur is implemented as cubemap LOD selection.
/// </summary>
public sealed class EnvironmentMapPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _shader = null!;
    private uint _cubeVao;
    private uint _cubeVbo;
    private bool _initialized;

    /// <inheritdoc/>
    public string Name => "EnvironmentMap";

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        if (!renderer.ShowSkybox)
            return;

        GLEnvironmentResources? environment = renderer.EnvironmentResources;
        if (environment is null || environment.SkyCubemap.TextureId == 0)
            return;

        Camera? camera = renderer.ActiveCamera;
        if (camera is null)
            return;

        _gl.DepthMask(false);
        _gl.Disable(GLEnum.DepthTest);
        _gl.Disable(EnableCap.CullFace);

        _shader.Use();

        _shader.SetUniform("uProjection", camera.GetProjectionMatrix());
        _shader.SetUniform("uView", camera.GetViewMatrix());
        _shader.SetUniform("uExposure", renderer.EnvironmentMapExposure);
        _shader.SetUniform("uBlurEnabled", renderer.EnvironmentMapBlurEnabled);
        uint cubemapTextureId = environment.SkyCubemap.TextureId;
        float blurMip = 0.0f;
        if (renderer.EnvironmentMapBlurEnabled)
        {
            if (environment.PrefilterCubemap.TextureId != 0)
            {
                cubemapTextureId = environment.PrefilterCubemap.TextureId;
                blurMip = Math.Clamp(renderer.EnvironmentMapBlurRadius, 0.0f, environment.PrefilterMaxMipLevel);
            }
            else
            {
                blurMip = Math.Clamp(renderer.EnvironmentMapBlurRadius, 0.0f, environment.SkyMaxMipLevel);
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

    /// <inheritdoc/>
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
