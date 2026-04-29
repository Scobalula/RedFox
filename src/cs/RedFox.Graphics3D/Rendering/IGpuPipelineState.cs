using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Represents an opaque GPU pipeline-state resource.
/// </summary>
public interface IGpuPipelineState : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the resource has been disposed.
	/// </summary>
	bool IsDisposed { get; }
}