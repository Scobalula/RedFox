using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Resources;

/// <summary>
/// Thin facade over an active <see cref="GL"/> context. Centralizes resource creation,
/// state management, and version queries so backends can share a consistent
/// API surface independent of how the underlying GL context was acquired.
/// </summary>
/// <remarks>
/// <see cref="OpenGlContext"/> does not own the underlying GL context lifetime;
/// disposal here only releases resources allocated through this facade.
/// </remarks>
internal sealed class OpenGlContext : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a new context wrapper around an existing <see cref="GL"/> instance.
    /// </summary>
    /// <param name="gl">The active GL instance.</param>
    public OpenGlContext(GL gl)
    {
        Gl = gl ?? throw new ArgumentNullException(nameof(gl));
        Gl.GetInteger(GLEnum.MajorVersion, out int major);
        Gl.GetInteger(GLEnum.MinorVersion, out int minor);
        MajorVersion = major;
        MinorVersion = minor;
        VersionString = Gl.GetStringS(StringName.Version) ?? string.Empty;
        IsEmbeddedProfile = VersionString.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the underlying GL instance. Provided as an escape hatch for advanced consumers;
    /// prefer the helper methods on this class when possible.
    /// </summary>
    internal GL Gl { get; }

    /// <summary>
    /// Gets or sets the framebuffer used when no explicit render target is bound.
    /// </summary>
    public uint DefaultFramebufferHandle { get; set; }

    /// <summary>
    /// Gets the GL major version reported by the active context.
    /// </summary>
    public int MajorVersion { get; }

    /// <summary>
    /// Gets the GL minor version reported by the active context.
    /// </summary>
    public int MinorVersion { get; }

    /// <summary>
    /// Gets the raw GL version string reported by the active context.
    /// </summary>
    public string VersionString { get; }

    /// <summary>
    /// Gets a value indicating whether the active context is an OpenGL ES profile.
    /// </summary>
    public bool IsEmbeddedProfile { get; }

    /// <summary>
    /// Gets a value indicating whether compute shaders are supported by the active context profile.
    /// </summary>
    public bool SupportsCompute => IsEmbeddedProfile ? SupportsVersion(3, 1) : SupportsVersion(4, 3);

    /// <summary>
    /// Returns whether the active context meets or exceeds the supplied GL version.
    /// </summary>
    /// <param name="major">Required major version.</param>
    /// <param name="minor">Required minor version.</param>
    /// <returns><see langword="true"/> when the active context is at least the supplied version.</returns>
    public bool SupportsVersion(int major, int minor) =>
        MajorVersion > major || (MajorVersion == major && MinorVersion >= minor);

    /// <summary>
    /// Throws when the active context does not meet the supplied GL version.
    /// </summary>
    /// <param name="major">Required major version.</param>
    /// <param name="minor">Required minor version.</param>
    public void RequireVersion(int major, int minor)
    {
        if (!SupportsVersion(major, minor))
        {
            throw new InvalidOperationException(
                $"OpenGL {major}.{minor}+ is required, but the active context is {MajorVersion}.{MinorVersion}.");
        }
    }

    /// <summary>
    /// Compiles and links a graphics shader program.
    /// </summary>
    /// <param name="vertexSource">The vertex-shader GLSL source.</param>
    /// <param name="fragmentSource">The fragment-shader GLSL source.</param>
    /// <returns>The compiled program. Caller owns disposal.</returns>
    public GlShaderProgram CreateShaderProgram(string vertexSource, string fragmentSource) =>
        new(Gl, vertexSource, fragmentSource);

    /// <summary>
    /// Compiles and links a compute-shader program.
    /// </summary>
    /// <param name="computeSource">The compute-shader GLSL source.</param>
    /// <returns>The compiled program. Caller owns disposal.</returns>
    public GlComputeProgram CreateComputeProgram(string computeSource) =>
        new(Gl, computeSource);

    /// <summary>
    /// Creates a new GL buffer object.
    /// </summary>
    /// <returns>The buffer handle.</returns>
    public uint CreateBuffer() => Gl.GenBuffer();

    /// <summary>
    /// Creates a new GL vertex-array object.
    /// </summary>
    /// <returns>The vertex-array handle.</returns>
    public uint CreateVertexArray() => Gl.GenVertexArray();

    /// <summary>
    /// Deletes a GL buffer when the handle is non-zero.
    /// </summary>
    /// <param name="handle">The buffer handle. Ignored when zero.</param>
    public void DeleteBuffer(uint handle)
    {
        if (handle != 0)
        {
            Gl.DeleteBuffer(handle);
        }
    }

    /// <summary>
    /// Deletes a GL vertex-array when the handle is non-zero.
    /// </summary>
    /// <param name="handle">The vertex-array handle. Ignored when zero.</param>
    public void DeleteVertexArray(uint handle)
    {
        if (handle != 0)
        {
            Gl.DeleteVertexArray(handle);
        }
    }

    /// <summary>
    /// Sets the active viewport.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public void SetViewport(int width, int height)
    {
        Gl.Viewport(0, 0, (uint)Math.Max(1, width), (uint)Math.Max(1, height));
    }

    /// <summary>
    /// Clears the color and depth buffers using the supplied clear color.
    /// </summary>
    /// <param name="clearColor">The framebuffer clear color.</param>
    public void ClearColorAndDepth(Vector4 clearColor)
    {
        Gl.DepthMask(true);
        Gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        Gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    /// <summary>
    /// Enables or disables depth testing.
    /// </summary>
    /// <param name="enabled">Whether depth testing should be enabled.</param>
    public void SetDepthTest(bool enabled)
    {
        if (enabled)
        {
            Gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            Gl.Disable(EnableCap.DepthTest);
        }
    }

    /// <summary>
    /// Enables or disables depth writes.
    /// </summary>
    /// <param name="enabled">Whether the depth buffer should be writable.</param>
    public void SetDepthMask(bool enabled) => Gl.DepthMask(enabled);

    /// <summary>
    /// Enables or disables face culling.
    /// </summary>
    /// <param name="enabled">Whether face culling should be enabled.</param>
    public void SetCullFace(bool enabled)
    {
        if (enabled)
        {
            Gl.Enable(EnableCap.CullFace);
        }
        else
        {
            Gl.Disable(EnableCap.CullFace);
        }
    }

    /// <summary>
    /// Returns whether the supplied capability is currently enabled.
    /// </summary>
    /// <param name="capability">The capability to query.</param>
    /// <returns><see langword="true"/> when enabled; otherwise <see langword="false"/>.</returns>
    public bool IsEnabled(EnableCap capability) => Gl.IsEnabled(capability);

    /// <summary>
    /// Sets the front-face winding mode.
    /// </summary>
    /// <param name="counterClockwise">Counter-clockwise when <see langword="true"/>; clockwise otherwise.</param>
    public void SetFrontFace(bool counterClockwise)
    {
        const int Clockwise = 0x0900;
        const int CounterClockwise = 0x0901;
        Gl.FrontFace((GLEnum)(counterClockwise ? CounterClockwise : Clockwise));
    }

    /// <summary>
    /// Configures alpha blending using the standard "src alpha, one minus src alpha" function.
    /// </summary>
    /// <param name="enabled">Whether blending should be enabled.</param>
    public void SetAlphaBlend(bool enabled)
    {
        if (enabled)
        {
            Gl.Enable(EnableCap.Blend);
            Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        else
        {
            Gl.Disable(EnableCap.Blend);
        }
    }

    /// <summary>
    /// Issues a memory barrier on shader-storage and vertex-attribute reads after a compute dispatch.
    /// </summary>
    public void StorageMemoryBarrier()
    {
        Gl.MemoryBarrier((uint)(MemoryBarrierMask.ShaderStorageBarrierBit | MemoryBarrierMask.VertexAttribArrayBarrierBit));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
