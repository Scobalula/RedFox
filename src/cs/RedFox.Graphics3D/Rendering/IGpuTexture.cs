using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Represents an opaque GPU texture resource.
/// </summary>
public interface IGpuTexture : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the resource has been disposed.
	/// </summary>
	bool IsDisposed { get; }
}