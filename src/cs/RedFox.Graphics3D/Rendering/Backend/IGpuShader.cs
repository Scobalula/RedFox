using System;

namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Represents an opaque GPU shader resource.
/// </summary>
public interface IGpuShader : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the resource has been disposed.
	/// </summary>
	bool IsDisposed { get; }
}