namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a pass that draws opaque geometry.
/// Always runs in <see cref="RenderPassPhase.Opaque"/>.
/// </summary>
public interface IOpaqueGeometryPass : IRenderPass
{
}
