using System;

namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Represents an opaque GPU render-target resource.
/// </summary>
public interface IGpuRenderTarget : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the resource has been disposed.
	/// </summary>
	bool IsDisposed { get; }
}