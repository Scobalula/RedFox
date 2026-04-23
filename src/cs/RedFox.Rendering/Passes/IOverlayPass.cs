namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a pass that renders debug/overlay geometry on top of (or in place of)
/// the main scene. Always runs in <see cref="RenderPassPhase.Overlay"/>.
/// </summary>
public interface IOverlayPass : IRenderPass
{
}
