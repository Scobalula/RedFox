using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics3D.OpenGL.Resources;

/// <summary>
/// Base class for compiled OpenGL programs. Owns shader compilation, program linking,
/// and uniform-location caching. Disposable to release the underlying GL program object.
/// </summary>
internal abstract class GlProgramBase : IDisposable
{
    private readonly Dictionary<string, int> _uniformLocations;
    private readonly Dictionary<string, int> _samplerSlots;
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
        _uniformLocations = new Dictionary<string, int>(StringComparer.Ordinal);
        _samplerSlots = new Dictionary<string, int>(StringComparer.Ordinal);

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

        location = Gl.GetAttribLocation(Handle, attributeName);
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

        slot = _samplerSlots.Count;
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
        int location = GetUniformLocation(uniformName);
        if (location < 0)
        {
            return;
        }

        Gl.Uniform1(location, value);
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
        ThrowIfDisposed();

        if (_uniformLocations.TryGetValue(uniformName, out int location))
        {
            return location;
        }

        location = Gl.GetUniformLocation(Handle, uniformName);
        _uniformLocations.Add(uniformName, location);
        return location;
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
}
