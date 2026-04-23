namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a screen-space post-processing pass (e.g. SSAO composite, tone-mapping).
/// Always runs in <see cref="RenderPassPhase.PostProcess"/>.
/// </summary>
public interface IPostProcessPass : IRenderPass
{
}
