namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a pass that generates shadow maps.
/// Always runs in <see cref="RenderPassPhase.Shadow"/>.
/// </summary>
public interface IShadowPass : IRenderPass
{
}
