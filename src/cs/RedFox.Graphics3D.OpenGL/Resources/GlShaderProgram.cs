using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Resources;

/// <summary>
/// Compiled OpenGL graphics shader program (vertex + fragment).
/// </summary>
internal sealed class GlShaderProgram : GlProgramBase
{
    /// <summary>
    /// Compiles and links a vertex/fragment shader pair into a program.
    /// </summary>
    /// <param name="gl">The owning GL context.</param>
    /// <param name="vertexSource">The vertex-shader GLSL source.</param>
    /// <param name="fragmentSource">The fragment-shader GLSL source.</param>
    public GlShaderProgram(GL gl, string vertexSource, string fragmentSource)
        : base(gl, new[]
        {
            (ShaderType.VertexShader, vertexSource),
            (ShaderType.FragmentShader, fragmentSource)
        })
    {
    }
}
