namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a pass that performs GPU skinning prior to geometry rendering.
/// Always runs in <see cref="RenderPassPhase.Compute"/>.
/// </summary>
public interface ISkinningPass : IRenderPass
{
}
