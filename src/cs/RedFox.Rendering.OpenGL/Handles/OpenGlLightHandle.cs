using RedFox.Graphics3D;
using System;
using System.Numerics;

namespace RedFox.Rendering.OpenGL.Handles;

/// <summary>
/// Stores cached render-time lighting data derived from a <see cref="Light"/> scene node.
/// This handle does not own any GPU resources; it's a frame-snapshot consumed by the
/// scene-collection pass.
/// </summary>
internal sealed class OpenGlLightHandle : ISceneNodeRenderHandle
{
    private readonly Light _light;
    private readonly OpenGlRenderSettings _settings;
    private bool _disposed;

    public OpenGlLightHandle(Light light, OpenGlRenderSettings settings)
    {
        _light = light ?? throw new ArgumentNullException(nameof(light));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Gets the normalized world-space light direction.</summary>
    public Vector3 Direction { get; private set; } = -Vector3.UnitY;

    /// <summary>Gets the light color.</summary>
    public Vector3 Color { get; private set; } = Vector3.One;

    /// <summary>Gets the light intensity.</summary>
    public float Intensity { get; private set; } = 1.0f;

    /// <summary>Gets a value indicating whether this light is enabled.</summary>
    public bool Enabled { get; private set; }

    public bool IsOwnedBy(OpenGlRenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ReferenceEquals(_settings, settings);
    }

    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Enabled = _light.Enabled;
        Color = _light.Color;
        Intensity = _light.Intensity;

        Vector3 fallback = _settings.FallbackLightDirection;
        if (fallback.LengthSquared() < 1e-10f)
        {
            fallback = -Vector3.UnitY;
        }

        Vector3 direction = -_light.Position;
        if (direction.LengthSquared() < 1e-10f)
        {
            direction = fallback;
        }

        if (direction.LengthSquared() < 1e-10f)
        {
            direction = -Vector3.UnitY;
        }

        Direction = Vector3.Normalize(direction);
    }

    public void Release()
    {
        if (_disposed)
        {
            return;
        }

        Enabled = false;
        Direction = -Vector3.UnitY;
        Color = Vector3.One;
        Intensity = 1.0f;
        _disposed = true;
    }

    public void Dispose()
    {
        Release();
    }
}
