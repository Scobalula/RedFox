using Silk.NET.OpenGL;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics3D.OpenGL.Resources;

/// <summary>
/// Compiled OpenGL graphics shader program (vertex + fragment).
/// </summary>
public sealed class GlShaderProgram : GlProgramBase
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

    /// <summary>
    /// Sets a 4x4 matrix uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The matrix value.</param>
    public unsafe void SetMatrix4(string uniformName, Matrix4x4 value)
    {
        int location = GetUniformLocation(uniformName);
        if (location < 0)
        {
            return;
        }

        float* pointer = (float*)Unsafe.AsPointer(ref value.M11);
        Gl.UniformMatrix4(location, 1, false, pointer);
    }

    /// <summary>
    /// Sets a 3-component vector uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The vector value.</param>
    public void SetVector3(string uniformName, Vector3 value)
    {
        int location = GetUniformLocation(uniformName);
        if (location < 0)
        {
            return;
        }

        Gl.Uniform3(location, value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Sets a 2-component vector uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The vector value.</param>
    public void SetVector2(string uniformName, Vector2 value)
    {
        int location = GetUniformLocation(uniformName);
        if (location < 0)
        {
            return;
        }

        Gl.Uniform2(location, value.X, value.Y);
    }

    /// <summary>
    /// Sets a 4-component vector uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The vector value.</param>
    public void SetVector4(string uniformName, Vector4 value)
    {
        int location = GetUniformLocation(uniformName);
        if (location < 0)
        {
            return;
        }

        Gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    /// <summary>
    /// Sets a single-precision float uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The float value.</param>
    public void SetFloat(string uniformName, float value)
    {
        int location = GetUniformLocation(uniformName);
        if (location < 0)
        {
            return;
        }

        Gl.Uniform1(location, value);
    }
}
