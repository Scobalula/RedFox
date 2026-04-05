namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents an RGBA colour using single-precision floating-point channels in [0, 1] range.
/// Used to specify the renderer's background clear colour.
/// </summary>
public readonly struct RendererColor(float r, float g, float b, float a)
{
    /// <summary>Red channel.</summary>
    public float R { get; } = r;

    /// <summary>Green channel.</summary>
    public float G { get; } = g;

    /// <summary>Blue channel.</summary>
    public float B { get; } = b;

    /// <summary>Alpha channel.</summary>
    public float A { get; } = a;
}
