namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a pass that draws transparent / blended geometry back-to-front.
/// Always runs in <see cref="RenderPassPhase.Transparent"/>.
/// </summary>
public interface ITransparentGeometryPass : IRenderPass
{
}
