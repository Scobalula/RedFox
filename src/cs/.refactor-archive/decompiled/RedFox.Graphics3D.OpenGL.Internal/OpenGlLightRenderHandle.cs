using System;
using System.Numerics;
using RedFox.Graphics3D.OpenGL.Rendering;

namespace RedFox.Graphics3D.OpenGL.Internal;

/// <summary>
/// Stores cached render-time lighting data derived from a scene light node.
/// </summary>
internal sealed class OpenGlLightRenderHandle : ISceneNodeRenderHandle, IDisposable
{
	private readonly Light _light;

	private readonly OpenGlRenderSettings _settings;

	private bool _disposed;

	/// <summary>
	/// Gets the normalized world-space light direction.
	/// </summary>
	public Vector3 Direction { get; private set; } = -Vector3.UnitY;

	/// <summary>
	/// Gets the light color.
	/// </summary>
	public Vector3 Color { get; private set; } = Vector3.One;

	/// <summary>
	/// Gets the light intensity.
	/// </summary>
	public float Intensity { get; private set; } = 1f;

	/// <summary>
	/// Gets a value indicating whether this light is enabled.
	/// </summary>
	public bool Enabled { get; private set; }

	public OpenGlLightRenderHandle(Light light, OpenGlRenderSettings settings)
	{
		_light = light ?? throw new ArgumentNullException("light");
		_settings = settings ?? throw new ArgumentNullException("settings");
	}

	public bool IsOwnedBy(OpenGlRenderSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings, "settings");
		return _settings == settings;
	}

	public void Update()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		Enabled = _light.Enabled;
		Color = _light.Color;
		Intensity = _light.Intensity;
		Vector3 vector = _settings.FallbackLightDirection;
		if (vector.LengthSquared() < 1E-10f)
		{
			vector = -Vector3.UnitY;
		}
		Vector3 value = -_light.Position;
		if (value.LengthSquared() < 1E-10f)
		{
			value = vector;
		}
		if (value.LengthSquared() < 1E-10f)
		{
			value = -Vector3.UnitY;
		}
		Direction = Vector3.Normalize(value);
	}

	public void Release()
	{
		if (!_disposed)
		{
			Enabled = false;
			Direction = -Vector3.UnitY;
			Color = Vector3.One;
			Intensity = 1f;
			_disposed = true;
		}
	}

	public void Dispose()
	{
		Release();
	}
}
