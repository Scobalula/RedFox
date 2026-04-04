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
    private uint _emptyVao;
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
        _emptyVao = _gl.GenVertexArray();

        _initialized = true;
    }

    /// <inheritdoc/>
    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        GLEnvironmentResources? environment = renderer.EnvironmentResources;
        if (environment is null || environment.SkyCubemap.TextureId == 0)
            return;

        Camera? camera = renderer.ActiveCamera;
        if (camera is null)
            return;

        Matrix4x4 viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();
        if (!Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection))
            return;

        _gl.DepthMask(false);
        _gl.Disable(GLEnum.DepthTest);
        _gl.Disable(EnableCap.CullFace);

        _shader.Use();

        _shader.SetUniform("uInverseViewProjection", inverseViewProjection);
        _shader.SetUniform("uCameraPos", camera.Position);
        _shader.SetUniform("uExposure", renderer.EnvironmentMapExposure);
        _shader.SetUniform("uBlurEnabled", renderer.EnvironmentMapBlurEnabled);
        float blurMip = renderer.EnvironmentMapBlurEnabled
            ? Math.Clamp(renderer.EnvironmentMapBlurRadius, 0.0f, environment.SkyMaxMipLevel)
            : 0.0f;
        _shader.SetUniform("uBlurMipLevel", blurMip);

        // Bind precomputed sky cubemap
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(GLEnum.TextureCubeMap, environment.SkyCubemap.TextureId);
        _shader.SetUniform("uSkyCubemap", 0);

        _gl.BindVertexArray(_emptyVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
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
            if (_emptyVao != 0)
                _gl.DeleteVertexArray(_emptyVao);
        }
        catch
        {
        }
    }
}
