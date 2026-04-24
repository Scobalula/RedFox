using RedFox.Graphics3D;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Per-frame state passed through every <see cref="IRenderPass"/> in an <see cref="IRenderPipeline"/>.
/// Carries the immutable inputs (scene, view, viewport, delta time) plus a typed services bag
/// that backends use to publish and consume frame-scoped data.
/// </summary>
public sealed class RenderFrameContext
{
    private readonly Dictionary<Type, object> _services = new();

    /// <summary>
    /// Gets the scene being rendered.
    /// </summary>
    public Scene Scene { get; }

    /// <summary>
    /// Gets the active camera view.
    /// </summary>
    public CameraView View { get; }

    /// <summary>
    /// Gets the viewport size in pixels.
    /// </summary>
    public Vector2 ViewportSize { get; }

    /// <summary>
    /// Gets seconds elapsed since the previous frame.
    /// </summary>
    public float DeltaTime { get; }

    /// <summary>
    /// Gets or sets the ambient color used for renderer lighting.
    /// </summary>
    public Vector3 AmbientColor { get; set; }

    /// <summary>
    /// Gets or sets the fallback light direction used when scene lights are unavailable.
    /// </summary>
    public Vector3 FallbackLightDirection { get; set; } = -Vector3.UnitY;

    /// <summary>
    /// Gets or sets the fallback light color used when scene lights are unavailable.
    /// </summary>
    public Vector3 FallbackLightColor { get; set; } = Vector3.One;

    /// <summary>
    /// Gets or sets the fallback light intensity used when scene lights are unavailable.
    /// </summary>
    public float FallbackLightIntensity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets a value indicating whether view-based lighting is enabled.
    /// </summary>
    public bool UseViewBasedLighting { get; set; }

    /// <summary>
    /// Gets or sets the skinning mode used during the frame.
    /// </summary>
    public SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    /// <summary>
    /// Initializes a new frame context.
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="view">The active camera view.</param>
    /// <param name="viewportSize">The viewport size in pixels.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    public RenderFrameContext(Scene scene, in CameraView view, Vector2 viewportSize, float deltaTime)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        View = view;
        ViewportSize = viewportSize;
        DeltaTime = deltaTime;
    }

    /// <summary>
    /// Publishes a frame-scoped service so subsequent passes can consume it.
    /// Replaces any existing entry of the same type.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="service">The service instance.</param>
    public void Set<TService>(TService service) where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(TService)] = service;
    }

    /// <summary>
    /// Resolves a frame-scoped service published earlier in the frame.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <returns>The published service instance.</returns>
    /// <exception cref="InvalidOperationException">No service of the supplied type has been published.</exception>
    public TService GetRequired<TService>() where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out object? value))
        {
            return (TService)value;
        }

        throw new InvalidOperationException($"No frame service of type '{typeof(TService).FullName}' has been published.");
    }

    /// <summary>
    /// Attempts to resolve a frame-scoped service.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="service">The resolved service when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the service was published; otherwise <see langword="false"/>.</returns>
    public bool TryGet<TService>(out TService? service) where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out object? value))
        {
            service = (TService)value;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    /// Clears all published frame services. Called by the renderer at frame start.
    /// </summary>
    public void ResetServices()
    {
        _services.Clear();
    }
}