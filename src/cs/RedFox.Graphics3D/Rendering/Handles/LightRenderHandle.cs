using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Stores per-light render data for renderer-owned light aggregation.
/// </summary>
internal sealed class LightRenderHandle : RenderHandle
{
    private readonly Light _light;

    private Vector3 _color;
    private bool _enabled;
    private float _intensity;
    private Vector3 _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="LightRenderHandle"/> class.
    /// </summary>
    /// <param name="light">The light node represented by this handle.</param>
    public LightRenderHandle(Light light)
    {
        _light = light ?? throw new ArgumentNullException(nameof(light));
    }

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        _position = _light.GetActiveWorldPosition();
        _color = _light.Color;
        _intensity = _light.Intensity;
        _enabled = _light.Enabled;

        if (!_enabled)
        {
            return;
        }

        Vector3 direction = -_position;
        if (direction.LengthSquared() < 1e-10f)
        {
            direction = -Vector3.UnitY;
        }

        commandList.AppendLight(Vector3.Normalize(direction), _color, _intensity);
    }

    /// <inheritdoc/>
    public override void Render(
        ICommandList commandList,
        RenderFlags phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ThrowIfDisposed();
    }
}