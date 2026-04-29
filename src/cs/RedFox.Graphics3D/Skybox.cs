using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents scene-owned skybox rendering settings.
/// </summary>
public sealed class Skybox
{
    /// <summary>
    /// Gets or sets a value indicating whether the skybox is rendered.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the cube-map texture rendered behind the scene.
    /// DDS cube maps should provide six faces in the loaded image payload.
    /// </summary>
    public Texture? Texture { get; set; }

    /// <summary>
    /// Gets or sets the multiplier applied to sampled skybox color.
    /// </summary>
    public float Intensity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the color tint applied to sampled skybox color.
    /// </summary>
    public Vector4 Tint { get; set; } = Vector4.One;

    internal IRenderHandle? GraphicsHandle { get; set; }
}