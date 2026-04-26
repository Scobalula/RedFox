using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Hosting;

namespace RedFox.Samples.Examples;

internal sealed class MeshSampleSceneContext
{
    public MeshSampleSceneContext(
        MeshSampleOptions options,
        Scene scene,
        OrbitCamera camera,
        Grid? grid,
        SceneViewportController viewportController,
        IReadOnlyList<AnimationPlayer> animationPlayers)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        Grid = grid;
        ViewportController = viewportController ?? throw new ArgumentNullException(nameof(viewportController));
        AnimationPlayers = animationPlayers ?? throw new ArgumentNullException(nameof(animationPlayers));
    }

    public MeshSampleOptions Options { get; }

    public Scene Scene { get; }

    public OrbitCamera Camera { get; }

    public Grid? Grid { get; }

    public SceneViewportController ViewportController { get; }

    public IReadOnlyList<AnimationPlayer> AnimationPlayers { get; }
}
