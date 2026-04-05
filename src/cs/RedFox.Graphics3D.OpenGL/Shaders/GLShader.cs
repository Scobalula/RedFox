using System.Numerics;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Shaders;

/// <summary>
/// Represents a compiled and linked OpenGL shader program.
/// </summary>
public sealed class GLShader : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformCache = new();
    private readonly Dictionary<string, UniformType> _uniformTypeCache = new();

    /// <summary>
    /// Gets the OpenGL handle for this shader program.
    /// </summary>
    public uint ProgramId { get; }

    /// <summary>
    /// Compiles and links a shader program from the given vertex and fragment source code.
    /// </summary>
    /// <param name="gl">The OpenGL context.</param>
    /// <param name="vertexSource">The GLSL source code for the vertex shader.</param>
    /// <param name="fragmentSource">The GLSL source code for the fragment shader.</param>
    /// <exception cref="InvalidOperationException">Thrown when shader compilation or program linking fails.</exception>
    public GLShader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        uint vs = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fs = CompileShader(ShaderType.FragmentShader, fragmentSource);

        ProgramId = gl.CreateProgram();
        gl.AttachShader(ProgramId, vs);
        gl.AttachShader(ProgramId, fs);
        gl.LinkProgram(ProgramId);

        gl.GetProgram(ProgramId, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            string log = gl.GetProgramInfoLog(ProgramId);
            gl.DeleteProgram(ProgramId);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        gl.DetachShader(ProgramId, vs);
        gl.DetachShader(ProgramId, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);

        CacheUniformMetadata();
    }

    private void CacheUniformMetadata()
    {
        _gl.GetProgram(ProgramId, ProgramPropertyARB.ActiveUniforms, out int count);
        for (uint i = 0; i < (uint)count; i++)
        {
            string name = _gl.GetActiveUniform(ProgramId, i, out _, out UniformType type);
            if (name.EndsWith("[0]"))
                name = name[..^3];
            int loc = _gl.GetUniformLocation(ProgramId, name);
            _uniformCache[name] = loc;
            _uniformTypeCache[name] = type;
        }
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            string typeName = type == ShaderType.VertexShader ? "vertex" : "fragment";
            throw new InvalidOperationException($"{typeName} shader compile failed: {log}");
        }

        return shader;
    }

    /// <summary>
    /// Binds this shader program for use in subsequent draw calls.
    /// </summary>
    public void Use() => _gl.UseProgram(ProgramId);

    /// <summary>
    /// Gets the location of a uniform variable by name, caching the result for subsequent lookups.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <returns>The uniform location, or -1 if the uniform is not found.</returns>
    public int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int location))
            return location;

        location = _gl.GetUniformLocation(ProgramId, name);
        _uniformCache[name] = location;
        return location;
    }

    /// <summary>
    /// Sets an integer uniform variable. Automatically converts to float if the uniform type is float.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The integer value to set.</param>
    public void SetUniform(string name, int value)
    {
        int loc = GetUniformLocation(name);
        if (loc < 0) return;

        if (_uniformTypeCache.TryGetValue(name, out UniformType type) &&
            type == UniformType.Float)
        {
            _gl.Uniform1(loc, (float)value);
        }
        else
        {
            _gl.Uniform1(loc, value);
        }
    }

    /// <summary>
    /// Sets a float uniform variable.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The float value to set.</param>
    public void SetUniform(string name, float value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    /// <summary>
    /// Sets a boolean uniform variable as an integer (1 for true, 0 for false).
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The boolean value to set.</param>
    public void SetUniform(string name, bool value)
    {
        SetUniform(name, value ? 1 : 0);
    }

    /// <summary>
    /// Sets a <see cref="Vector2"/> uniform variable.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The 2-component vector value to set.</param>
    public void SetUniform(string name, System.Numerics.Vector2 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform2(loc, value.X, value.Y);
    }

    /// <summary>
    /// Sets a <see cref="Vector3"/> uniform variable.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The 3-component vector value to set.</param>
    public void SetUniform(string name, System.Numerics.Vector3 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform3(loc, value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Sets a <see cref="Vector4"/> uniform variable.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The 4-component vector value to set.</param>
    public void SetUniform(string name, System.Numerics.Vector4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform4(loc, value.X, value.Y, value.Z, value.W);
    }

    /// <summary>
    /// Sets a <see cref="Matrix4x4"/> uniform variable.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The 4x4 matrix value to set.</param>
    public unsafe void SetUniform(string name, System.Numerics.Matrix4x4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.UniformMatrix4(loc, 1, false, (float*)&value);
    }

    /// <summary>
    /// Sets an array of <see cref="Matrix4x4"/> uniform variables.
    /// </summary>
    /// <param name="name">The name of the uniform variable array.</param>
    /// <param name="matrices">The array of 4x4 matrices to set.</param>
    public unsafe void SetUniformMatrix4Array(string name, System.Numerics.Matrix4x4[] matrices)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0 && matrices.Length > 0)
        {
            fixed (System.Numerics.Matrix4x4* ptr = matrices)
            {
                _gl.UniformMatrix4(loc, (uint)matrices.Length, false, (float*)ptr);
            }
        }
    }

    /// <summary>
    /// Sets a <see cref="Matrix3x3"/> uniform variable.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <param name="value">The 3x3 matrix value to set.</param>
    public unsafe void SetUniform(string name, Matrix3x3 value)
    {
        int loc = GetUniformLocation(name);
        if (loc < 0)
            return;

        Span<float> data =
        [
            value.M11, value.M12, value.M13,
            value.M21, value.M22, value.M23,
            value.M31, value.M32, value.M33,
        ];

        fixed (float* ptr = data)
        {
            _gl.UniformMatrix3(loc, 1, false, ptr);
        }
    }

    /// <summary>
    /// Deletes the OpenGL shader program and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        if (ProgramId != 0)
        {
            try
            {
                _gl.DeleteProgram(ProgramId);
            }
            catch
            {
            }
        }
        _uniformCache.Clear();
    }
}
