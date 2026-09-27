using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Hosting;

namespace RedFox.Samples.Examples;

internal sealed class MeshSampleSceneContext(MeshSampleOptions options, Scene scene, OrbitCamera camera, Grid? grid, SceneViewportController viewportController, IReadOnlyList<AnimationPlayer> animationPlayers)
{
    public MeshSampleOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public Scene Scene { get; } = scene ?? throw new ArgumentNullException(nameof(scene));

    public OrbitCamera Camera { get; } = camera ?? throw new ArgumentNullException(nameof(camera));

    public Grid? Grid { get; }

    public SceneViewportController ViewportController { get; } = viewportController ?? throw new ArgumentNullException(nameof(viewportController));

    public IReadOnlyList<AnimationPlayer> AnimationPlayers { get; } = animationPlayers ?? throw new ArgumentNullException(nameof(animationPlayers));
}
