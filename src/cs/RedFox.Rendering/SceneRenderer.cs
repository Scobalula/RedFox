using RedFox.Graphics3D;
using System;

namespace RedFox.Rendering;

/// <summary>
/// Provides an abstract high-level renderer contract for drawing a scene with a camera.
/// Backends typically implement this in terms of an <see cref="IRenderPipeline"/> of <see cref="IRenderPass"/> instances.
/// </summary>
public abstract class SceneRenderer : IDisposable
{
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
}
