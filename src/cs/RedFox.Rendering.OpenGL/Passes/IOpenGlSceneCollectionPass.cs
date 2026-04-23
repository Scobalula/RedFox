using RedFox.Rendering.Passes;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Marker interface implemented by <see cref="ISceneCollectionPass"/> implementations
/// for the OpenGL backend (mesh/grid/bone/light handles).
/// </summary>
internal interface IOpenGlSceneCollectionPass : ISceneCollectionPass
{
}
