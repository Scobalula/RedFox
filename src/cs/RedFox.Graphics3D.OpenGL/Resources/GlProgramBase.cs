using Silk.NET.OpenGL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace RedFox.Graphics3D.OpenGL.Resources;

/// <summary>
/// Base class for compiled OpenGL programs. Owns shader compilation, program linking,
/// and uniform-location caching. Disposable to release the underlying GL program object.
/// </summary>
internal abstract class GlProgramBase : IDisposable
{
    private const int SamplerBufferBaseSlot = 16;

    private static readonly ConcurrentDictionary<string, GlProgramBinary> ProgramBinaries = new(StringComparer.Ordinal);

    private readonly Dictionary<string, GlUniformState>	_uniforms;
    private readonly Dictionary<string, int>			_attributeLocations;
    private readonly Dictionary<string, int>			_samplerSlots;
    private bool _disposed;

    /// <summary>
    /// Initializes the program by compiling and linking the supplied shader stages.
    /// </summary>
    /// <param name="gl">The owning GL context.</param>
    /// <param name="shaders">The shader sources, one per stage.</param>
    protected GlProgramBase(GL gl, (ShaderType ShaderType, string Source)[] shaders)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(shaders);

        if (shaders.Length == 0)
        {
            throw new ArgumentException("At least one shader source is required.", nameof(shaders));
        }

        Gl = gl;
        _uniforms			= new Dictionary<string, GlUniformState>(StringComparer.Ordinal);
        _attributeLocations	= new Dictionary<string, int>(StringComparer.Ordinal);
        _samplerSlots		= new Dictionary<string, int>(StringComparer.Ordinal);

        string? binaryKey = CreateBinaryKey(shaders);
        if (binaryKey is not null && TryLoadBinary(binaryKey))
        {
            return;
        }

        uint[] shaderHandles = new uint[shaders.Length];
        int shaderCount = 0;

        try
        {
            for (int i = 0; i < shaders.Length; i++)
            {
                (ShaderType shaderType, string source) = shaders[i];
                ArgumentException.ThrowIfNullOrWhiteSpace(source);
                uint shaderHandle = CompileShader(shaderType, source);
                shaderHandles[shaderCount++] = shaderHandle;
            }

            Handle = Gl.CreateProgram();

            if (binaryKey is not null)
            {
                Gl.ProgramParameter(Handle, GLEnum.ProgramBinaryRetrievableHint, 1);
            }

            for (int i = 0; i < shaderCount; i++)
            {
                Gl.AttachShader(Handle, shaderHandles[i]);
            }

            Gl.LinkProgram(Handle);
            Gl.GetProgram(Handle, GLEnum.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
            {
                string infoLog = Gl.GetProgramInfoLog(Handle);
                throw new InvalidOperationException($"OpenGL program link failed: {infoLog}");
            }

            if (binaryKey is not null)
            {
                StoreBinary(binaryKey);
            }
        }
        catch
        {
            if (Handle != 0)
            {
                Gl.DeleteProgram(Handle);
                Handle = 0;
            }

            throw;
        }
        finally
        {
            for (int i = 0; i < shaderCount; i++)
            {
                uint shaderHandle = shaderHandles[i];
                if (shaderHandle == 0)
                {
                    continue;
                }

                if (Handle != 0)
                {
                    Gl.DetachShader(Handle, shaderHandle);
                }

                Gl.DeleteShader(shaderHandle);
            }
        }
    }

    /// <summary>
    /// Gets the owning GL context.
    /// </summary>
    public GL Gl { get; }

    /// <summary>
    /// Gets the underlying GL program handle.
    /// </summary>
    public uint Handle { get; private set; }

    /// <summary>
    /// Binds this program for subsequent draw / dispatch calls.
    /// </summary>
    public void Use()
    {
        ThrowIfDisposed();
        Gl.UseProgram(Handle);
    }

    /// <summary>
    /// Attempts to resolve the location of an active vertex attribute by name.
    /// </summary>
    /// <param name="attributeName">The vertex attribute name.</param>
    /// <param name="location">Receives the attribute location when present.</param>
    /// <returns><see langword="true"/> when the attribute is active; otherwise <see langword="false"/>.</returns>
    public bool TryGetAttributeLocation(string attributeName, out int location)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(attributeName))
        {
            location = -1;
            return false;
        }

        if (!_attributeLocations.TryGetValue(attributeName, out location))
        {
            location = Gl.GetAttribLocation(Handle, attributeName);
            _attributeLocations.Add(attributeName, location);
        }

        return location >= 0;
    }

    /// <summary>
    /// Attempts to resolve a texture unit for an active sampler uniform by name.
    /// </summary>
    /// <param name="samplerName">The sampler uniform name.</param>
    /// <param name="slot">Receives the assigned texture unit when present.</param>
    /// <returns><see langword="true"/> when the sampler is active; otherwise <see langword="false"/>.</returns>
    public bool TryGetSamplerSlot(string samplerName, out int slot)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(samplerName) || GetUniformLocation(samplerName) < 0)
        {
            slot = -1;
            return false;
        }

        if (_samplerSlots.TryGetValue(samplerName, out slot))
        {
            return true;
        }

        slot = SamplerBufferBaseSlot + _samplerSlots.Count;
        _samplerSlots.Add(samplerName, slot);
        return true;
    }

    /// <summary>
    /// Sets an integer uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The integer value.</param>
    public void SetInt(string uniformName, int value)
    {
        GlUniformState uniform = GetUniformState(uniformName);
        if (uniform.Location < 0 || (uniform.HasValue && uniform.IntValue == value))
        {
            return;
        }

        uniform.IntValue = value;
        uniform.HasValue = true;
        Gl.Uniform1(uniform.Location, value);
    }

    /// <summary>
    /// Sets a single-precision float uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The float value.</param>
    public void SetFloat(string uniformName, float value)
    {
        GlUniformState uniform = GetUniformState(uniformName);
        if (uniform.Location < 0 || !TryStoreVector(uniform, new Vector4(value, 0.0f, 0.0f, 0.0f)))
        {
            return;
        }

        Gl.Uniform1(uniform.Location, value);
    }

    /// <summary>
    /// Sets a 2-component vector uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The vector value.</param>
    public void SetVector2(string uniformName, Vector2 value)
    {
        GlUniformState uniform = GetUniformState(uniformName);
        if (uniform.Location < 0 || !TryStoreVector(uniform, new Vector4(value, 0.0f, 0.0f)))
        {
            return;
        }

        Gl.Uniform2(uniform.Location, value.X, value.Y);
    }

    /// <summary>
    /// Sets a 3-component vector uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The vector value.</param>
    public void SetVector3(string uniformName, Vector3 value)
    {
        GlUniformState uniform = GetUniformState(uniformName);
        if (uniform.Location < 0 || !TryStoreVector(uniform, new Vector4(value, 0.0f)))
        {
            return;
        }

        Gl.Uniform3(uniform.Location, value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Sets a 4-component vector uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The vector value.</param>
    public void SetVector4(string uniformName, Vector4 value)
    {
        GlUniformState uniform = GetUniformState(uniformName);
        if (uniform.Location < 0 || !TryStoreVector(uniform, value))
        {
            return;
        }

        Gl.Uniform4(uniform.Location, value.X, value.Y, value.Z, value.W);
    }

    /// <summary>
    /// Sets a 4x4 matrix uniform value by name.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <param name="value">The matrix value.</param>
    public unsafe void SetMatrix4(string uniformName, Matrix4x4 value)
    {
        GlUniformState uniform = GetUniformState(uniformName);
        if (uniform.Location < 0 || (uniform.HasValue && uniform.MatrixValue == value))
        {
            return;
        }

        uniform.MatrixValue = value;
        uniform.HasValue = true;
        float* pointer = (float*)Unsafe.AsPointer(ref value.M11);
        Gl.UniformMatrix4(uniform.Location, 1, false, pointer);
    }

    public bool HasUniform(string uniformName)
    {
        return GetUniformLocation(uniformName) >= 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (Handle != 0)
        {
            Gl.DeleteProgram(Handle);
            Handle = 0;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throws when the program has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Resolves and caches the location of a named uniform.
    /// </summary>
    /// <param name="uniformName">The uniform name.</param>
    /// <returns>The uniform location, or a negative value when the uniform is not active.</returns>
    protected int GetUniformLocation(string uniformName)
    {
        return GetUniformState(uniformName).Location;
    }

    private GlUniformState GetUniformState(string uniformName)
    {
        ThrowIfDisposed();

        if (!_uniforms.TryGetValue(uniformName, out GlUniformState? uniform))
        {
            uniform = new GlUniformState(Gl.GetUniformLocation(Handle, uniformName));
            _uniforms.Add(uniformName, uniform);
        }

        return uniform;
    }

    private static bool TryStoreVector(GlUniformState uniform, Vector4 value)
    {
        if (uniform.HasValue && uniform.VectorValue == value)
        {
            return false;
        }

        uniform.VectorValue = value;
        uniform.HasValue = true;
        return true;
    }

    private uint CompileShader(ShaderType shaderType, string source)
    {
        uint shader = Gl.CreateShader(shaderType);
        Gl.ShaderSource(shader, source);
        Gl.CompileShader(shader);
        Gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compileStatus);
        if (compileStatus == 0)
        {
            string infoLog = Gl.GetShaderInfoLog(shader);
            Gl.DeleteShader(shader);
            throw new InvalidOperationException($"OpenGL shader compile failed ({shaderType}): {infoLog}");
        }

        return shader;
    }

    private string? CreateBinaryKey((ShaderType ShaderType, string Source)[] shaders)
    {
        Gl.GetInteger(GLEnum.NumProgramBinaryFormats, out int formatCount);
        if (formatCount <= 0)
        {
            return null;
        }

        StringBuilder key = new();
        key.Append(Gl.GetStringS(StringName.Renderer)).Append('|').Append(Gl.GetStringS(StringName.Version));

        foreach ((ShaderType shaderType, string source) in shaders)
        {
            key.Append('|').Append(shaderType).Append(':').Append(source);
        }

        return key.ToString();
    }

    private unsafe bool TryLoadBinary(string binaryKey)
    {
        if (!ProgramBinaries.TryGetValue(binaryKey, out GlProgramBinary binary))
        {
            return false;
        }

        uint handle = Gl.CreateProgram();

        fixed (byte* data = binary.Data)
        {
            Gl.ProgramBinary(handle, binary.Format, data, (uint)binary.Data.Length);
        }

        Gl.GetProgram(handle, GLEnum.LinkStatus, out int linkStatus);
        if (linkStatus != 0)
        {
            Handle = handle;
            return true;
        }

        Gl.DeleteProgram(handle);
        ProgramBinaries.TryRemove(binaryKey, out _);
        return false;
    }

    private unsafe void StoreBinary(string binaryKey)
    {
        Gl.GetProgram(Handle, GLEnum.ProgramBinaryLength, out int length);
        if (length <= 0)
        {
            return;
        }

        byte[] data = new byte[length];
        uint written;
        GLEnum format;

        fixed (byte* destination = data)
        {
            Gl.GetProgramBinary(Handle, (uint)length, &written, &format, destination);
        }

        if (written > 0)
        {
            ProgramBinaries[binaryKey] = new GlProgramBinary(format, written == length ? data : data[..(int)written]);
        }
    }
}
