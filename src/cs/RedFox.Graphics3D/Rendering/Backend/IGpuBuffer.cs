using System;

namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Represents an opaque GPU buffer resource.
/// </summary>
public interface IGpuBuffer : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the resource has been disposed.
	/// </summary>
	bool IsDisposed { get; }
}