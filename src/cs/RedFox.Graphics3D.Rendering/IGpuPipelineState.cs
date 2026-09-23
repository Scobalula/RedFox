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

	/// <summary>
	/// Attempts to resolve the backend binding slot for a named vertex attribute or shader buffer resource.
	/// </summary>
	/// <param name="name">The shader-visible buffer or vertex attribute name.</param>
	/// <param name="slot">Receives the resolved backend binding slot when found.</param>
	/// <returns><see langword="true"/> when a slot was resolved; otherwise <see langword="false"/>.</returns>
	bool TryGetBufferSlot(string name, out int slot);
}
