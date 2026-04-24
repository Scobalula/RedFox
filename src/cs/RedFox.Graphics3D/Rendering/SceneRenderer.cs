using RedFox.Graphics3D;
using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Provides an abstract high-level renderer contract for drawing a scene with a camera.
/// Backends typically implement this in terms of an <see cref="IRenderPipeline"/> of <see cref="IRenderPass"/> instances.
/// </summary>
public abstract class SceneRenderer : IDisposable
{
    /// <summary>
    /// Gets or sets the ambient color used for renderer lighting.
    /// </summary>
    public Vector3 AmbientColor { get; set; }

    /// <summary>
    /// Gets or sets the fallback light direction used when no enabled scene lights are available.
    /// </summary>
    public Vector3 FallbackLightDirection { get; set; }

    /// <summary>
    /// Gets or sets the fallback light color used when no enabled scene lights are available.
    /// </summary>
    public Vector3 FallbackLightColor { get; set; }

    /// <summary>
    /// Gets or sets the fallback light intensity used when no enabled scene lights are available.
    /// </summary>
    public float FallbackLightIntensity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether view-based lighting is enabled.
    /// </summary>
    public bool UseViewBasedLighting { get; set; }

    /// <summary>
    /// Gets or sets the skinning mode used during rendering.
    /// </summary>
    public SkinningMode SkinningMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneRenderer"/> class.
    /// </summary>
    /// <param name="ambientColor">The ambient light contribution.</param>
    /// <param name="fallbackLightDirection">The fallback light direction.</param>
    /// <param name="fallbackLightColor">The fallback light color.</param>
    /// <param name="fallbackLightIntensity">The fallback light intensity.</param>
    /// <param name="useViewBasedLighting">Whether view-based lighting is enabled.</param>
    /// <param name="skinningMode">The active skinning mode.</param>
    protected SceneRenderer(
        Vector3 ambientColor,
        Vector3 fallbackLightDirection,
        Vector3 fallbackLightColor,
        float fallbackLightIntensity,
        bool useViewBasedLighting,
        SkinningMode skinningMode)
    {
        AmbientColor = ambientColor;
        FallbackLightDirection = fallbackLightDirection;
        FallbackLightColor = fallbackLightColor;
        FallbackLightIntensity = fallbackLightIntensity;
        UseViewBasedLighting = useViewBasedLighting;
        SkinningMode = skinningMode;
    }

    /// <summary>
    /// Initializes renderer state and GPU resources.
    /// </summary>
    public abstract void Initialize();

    /// <summary>
    /// Resizes the active viewport.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public abstract void Resize(int width, int height);

    /// <summary>
    /// Renders the provided scene from the provided camera view.
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="view">The camera view (matrices and position) to render with.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    public abstract void Render(Scene scene, in CameraView view, float deltaTime);

    /// <summary>
    /// Releases renderer resources.
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// Creates a frame context populated with the renderer's per-frame lighting and skinning configuration.
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="view">The active camera view.</param>
    /// <param name="viewportSize">The active viewport size.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    /// <returns>The populated frame context.</returns>
    protected RenderFrameContext CreateFrameContext(Scene scene, in CameraView view, Vector2 viewportSize, float deltaTime)
    {
        RenderFrameContext context = new(scene, view, viewportSize, deltaTime)
        {
            AmbientColor = AmbientColor,
            FallbackLightDirection = FallbackLightDirection,
            FallbackLightColor = FallbackLightColor,
            FallbackLightIntensity = FallbackLightIntensity,
            UseViewBasedLighting = UseViewBasedLighting,
            SkinningMode = SkinningMode,
        };

        return context;
    }
}