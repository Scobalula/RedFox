using System.Numerics;

namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Contains frame-level rendering settings for the OpenGL scene renderer.
/// </summary>
public sealed class OpenGlRenderSettings
{
    /// <summary>Gets or sets the framebuffer clear color.</summary>
    public Vector4 ClearColor { get; set; }

    /// <summary>Gets or sets the ambient light contribution.</summary>
    public Vector3 AmbientColor { get; set; }

    /// <summary>Gets or sets the fallback light direction used when no enabled scene lights are available.</summary>
    public Vector3 FallbackLightDirection { get; set; }

    /// <summary>Gets or sets the fallback light color used when no enabled scene lights are available.</summary>
    public Vector3 FallbackLightColor { get; set; }

    /// <summary>Gets or sets the fallback light intensity used when no enabled scene lights are available.</summary>
    public float FallbackLightIntensity { get; set; }

    /// <summary>Gets or sets the scene up-axis used for mesh/light rendering conversion.</summary>
    public OpenGlUpAxis UpAxis { get; set; } = OpenGlUpAxis.Y;

    /// <summary>Gets or sets the front-face winding used for culling.</summary>
    public OpenGlFaceWinding FaceWinding { get; set; } = OpenGlFaceWinding.Ccw;

    /// <summary>Gets or sets the Phong specular strength.</summary>
    public float SpecularStrength { get; set; } = 0.28f;

    /// <summary>Gets or sets the Phong shininess power.</summary>
    public float SpecularPower { get; set; } = 32.0f;

    /// <summary>
    /// Gets or sets a value indicating whether view-based lighting should be used.
    /// When enabled, surfaces facing the camera are bright and back-facing surfaces are dark.
    /// </summary>
    public bool UseViewBasedLighting { get; set; }

    /// <summary>Gets or sets the skinning algorithm used by GPU skinning.</summary>
    public SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    /// <summary>Gets or sets a value indicating whether skeleton bones should be rendered.</summary>
    public bool ShowSkeletonBones { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether skeleton bones should render on top of scene geometry.</summary>
    public bool BonesRenderOnTop { get; set; } = true;

    /// <summary>
    /// Gets or sets the local axis length rendered at each skeleton bone.
    /// This also acts as the minimum axis size when auto-scaling is enabled.
    /// </summary>
    public float BoneAxisSize { get; set; } = 0.2f;

    /// <summary>Gets or sets the parent-length multiplier used to auto-scale bone axis size.</summary>
    public float BoneAxisScaleFromParent { get; set; } = 0.2f;

    /// <summary>Gets or sets the maximum axis size used by auto-scaling. Set to 0 to disable clamping.</summary>
    public float BoneAxisMaxSize { get; set; } = 2.0f;

    /// <summary>Gets or sets the pixel width of rendered skeleton bone lines.</summary>
    public float BoneLineWidth { get; set; } = 1.15f;

    /// <summary>Gets or sets the color of the rendered X axis at each bone.</summary>
    public Vector4 BoneAxisXColor { get; set; } = new(0.9f, 0.25f, 0.25f, 0.95f);

    /// <summary>Gets or sets the color of the rendered Y axis at each bone.</summary>
    public Vector4 BoneAxisYColor { get; set; } = new(0.25f, 0.9f, 0.25f, 0.95f);

    /// <summary>Gets or sets the color of the rendered Z axis at each bone.</summary>
    public Vector4 BoneAxisZColor { get; set; } = new(0.25f, 0.45f, 0.95f, 0.95f);

    /// <summary>Gets or sets the color of the parent-to-child bone connection line.</summary>
    public Vector4 BoneConnectionColor { get; set; } = new(0.88f, 0.88f, 0.76f, 0.9f);

    /// <summary>Gets or sets a value indicating whether each bone connection line should use a deterministic color derived from the bone name.</summary>
    public bool UseBoneNameHashColor { get; set; } = true;

    /// <summary>Gets or sets the saturation used for deterministic bone name colors.</summary>
    public float BoneNameColorSaturation { get; set; } = 0.62f;

    /// <summary>Gets or sets the value used for deterministic bone name colors.</summary>
    public float BoneNameColorValue { get; set; } = 0.95f;

    /// <summary>
    /// Initializes a new render settings instance.
    /// </summary>
    /// <param name="clearColor">The clear color.</param>
    /// <param name="ambientColor">The ambient term.</param>
    /// <param name="fallbackLightDirection">The fallback light direction.</param>
    /// <param name="fallbackLightColor">The fallback light color.</param>
    /// <param name="fallbackLightIntensity">The fallback light intensity.</param>
    public OpenGlRenderSettings(
        Vector4 clearColor,
        Vector3 ambientColor,
        Vector3 fallbackLightDirection,
        Vector3 fallbackLightColor,
        float fallbackLightIntensity)
    {
        ClearColor = clearColor;
        AmbientColor = ambientColor;
        FallbackLightDirection = fallbackLightDirection;
        FallbackLightColor = fallbackLightColor;
        FallbackLightIntensity = fallbackLightIntensity;
    }
}
