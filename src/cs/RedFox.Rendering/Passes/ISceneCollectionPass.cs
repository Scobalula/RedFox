namespace RedFox.Rendering.Passes;

/// <summary>
/// Marker interface for a pass that traverses the scene graph and builds the per-frame draw list.
/// Always runs in <see cref="RenderPassPhase.Collect"/>.
/// </summary>
public interface ISceneCollectionPass : IRenderPass
{
}
