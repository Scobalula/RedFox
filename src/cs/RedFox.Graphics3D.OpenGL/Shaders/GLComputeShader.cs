using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Shaders;

/// <summary>
/// Represents a compiled and linked OpenGL compute shader program.
/// </summary>
internal sealed class GLComputeShader : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformCache = new();

    public uint ProgramId { get; }

    public GLComputeShader(GL gl, string computeSource)
    {
        _gl = gl;
        uint cs = CompileShader(gl, computeSource);

        ProgramId = gl.CreateProgram();
        gl.AttachShader(ProgramId, cs);
        gl.LinkProgram(ProgramId);

        gl.GetProgram(ProgramId, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            string log = gl.GetProgramInfoLog(ProgramId);
            gl.DeleteProgram(ProgramId);
            throw new InvalidOperationException($"Compute shader link failed: {log}");
        }

        gl.DetachShader(ProgramId, cs);
        gl.DeleteShader(cs);
    }

    public void Use() => _gl.UseProgram(ProgramId);

    public int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int loc))
            return loc;

        loc = _gl.GetUniformLocation(ProgramId, name);
        _uniformCache[name] = loc;
        return loc;
    }

    public void SetUniform(string name, int value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, bool value) => SetUniform(name, value ? 1 : 0);

    public void Dispatch(uint groupsX, uint groupsY, uint groupsZ)
    {
        _gl.DispatchCompute(groupsX, groupsY, groupsZ);
    }

    public void Dispose()
    {
        if (ProgramId != 0)
        {
            try { _gl.DeleteProgram(ProgramId); }
            catch { }
        }

        _uniformCache.Clear();
    }

    private static uint CompileShader(GL gl, string source)
    {
        uint shader = gl.CreateShader(ShaderType.ComputeShader);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Compute shader compile failed: {log}");
        }

        return shader;
    }
}
