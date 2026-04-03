using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

/// <summary>
/// A render pass that renders the equirectangular environment map as a background skybox.
/// Supports optional Gaussian blur applied in the fragment shader.
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

        GLEquirectangularEnvironmentMap? envMap = renderer.EnvironmentMap;
        if (envMap is null)
            return;

        GLHdrTextureHandle? handle = envMap.TextureHandle;
        if (handle is null || handle.TextureId == 0)
            return;

        Camera? camera = renderer.ActiveCamera;
        if (camera is null)
            return;

        Matrix4x4 viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();
        if (!Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection))
            return;

        _gl.DepthMask(false);
        _gl.Disable(GLEnum.DepthTest);

        _shader.Use();

        _shader.SetUniform("uInverseViewProjection", inverseViewProjection);
        _shader.SetUniform("uCameraPos", camera.Position);
        _shader.SetUniform("uExposure", renderer.EnvironmentMapExposure);
        _shader.SetUniform("uBlurEnabled", renderer.EnvironmentMapBlurEnabled);
        _shader.SetUniform("uBlurRadius", renderer.EnvironmentMapBlurRadius);
        _shader.SetUniform("uResolution", new Vector2(handle.Width, handle.Height));

        // Bind environment map texture
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(GLEnum.Texture2D, handle.TextureId);
        _shader.SetUniform("uEnvironmentMap", 0);

        _gl.BindVertexArray(_emptyVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);

        _gl.BindTexture(GLEnum.Texture2D, 0);

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
