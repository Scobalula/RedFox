using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Shaders;

public sealed class GLShader : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformCache = new();

    public uint ProgramId { get; }

    public GLShader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        uint vs = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fs = CompileShader(ShaderType.FragmentShader, fragmentSource);

        ProgramId = gl.CreateProgram();
        gl.AttachShader(ProgramId, vs);
        gl.AttachShader(ProgramId, fs);
        gl.LinkProgram(ProgramId);

        gl.GetProgram(ProgramId, ProgramPropertyPName.LinkStatus, out int linkStatus);
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

    public void Use() => _gl.UseProgram(ProgramId);

    public int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int location))
            return location;

        location = _gl.GetUniformLocation(ProgramId, name);
        _uniformCache[name] = location;
        return location;
    }

    public void SetUniform(string name, int value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, float value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, bool value)
    {
        SetUniform(name, value ? 1 : 0);
    }

    public void SetUniform(string name, System.Numerics.Vector2 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform2(loc, value.X, value.Y);
    }

    public void SetUniform(string name, System.Numerics.Vector3 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform3(loc, value.X, value.Y, value.Z);
    }

    public void SetUniform(string name, System.Numerics.Vector4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform4(loc, value.X, value.Y, value.Z, value.W);
    }

    public unsafe void SetUniform(string name, System.Numerics.Matrix4x4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.UniformMatrix4(loc, 1, false, (float*)&value);
    }

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

    public void Dispose()
    {
        if (ProgramId != 0)
        {
            _gl.DeleteProgram(ProgramId);
        }
        _uniformCache.Clear();
    }
}
