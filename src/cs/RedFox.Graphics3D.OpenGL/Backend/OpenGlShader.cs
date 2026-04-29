using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a concrete OpenGL shader description used to build pipeline state.
/// </summary>
internal sealed class OpenGlShader : IGpuShader
{
    private string _source;

    /// <summary>
    /// Gets the shader source text.
    /// </summary>
    internal string Source
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _source;
        }
    }

    /// <summary>
    /// Gets the shader stage.
    /// </summary>
    internal ShaderStage Stage { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlShader"/> class.
    /// </summary>
    /// <param name="source">The UTF-16 shader source text.</param>
    /// <param name="stage">The shader stage.</param>
    public OpenGlShader(string source, ShaderStage stage)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Stage = stage;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _source = string.Empty;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}