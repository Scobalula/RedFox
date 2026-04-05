using RedFox.Graphics3D.OpenGL.Viewing;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class AnimationPass : IRenderPass
{
    private AnimationPlaybackController? _controller;
    private Scene? _lastScene;
    private string? _lastAnimationName;

    public string Name => "Animation";
    public PassPhase Phase => PassPhase.Prepass;
    public bool Enabled { get; set; } = true;

    public string? AnimationName { get; set; }
    public float Speed { get; set; } = 1.0f;
    public float ElapsedSeconds => _controller?.ElapsedSeconds ?? 0;
    public float DurationSeconds => _controller?.DurationSeconds ?? 0;
    public float FrameRate => _controller?.FrameRate ?? 0;

    public void Initialize(GLRenderer renderer) { }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        EnsureController(scene);

        _controller!.Speed = Math.Max(Speed, 0.0f);

        if (Enabled)
            _controller.Update(deltaTime);
        else
            _controller.SampleAt(_controller.ElapsedSeconds);
    }

    public void Reset(Scene scene, string? animationName, float startTime = 0.0f)
    {
        AnimationName = animationName;
        _controller = new AnimationPlaybackController(scene, animationName)
        {
            Speed = Math.Max(Speed, 0.0f),
            ElapsedSeconds = startTime,
        };
        _controller.SampleAt(_controller.ElapsedSeconds);
        _lastScene = scene;
        _lastAnimationName = animationName;
    }

    public void SampleAt(float elapsedSeconds)
    {
        _controller?.SampleAt(elapsedSeconds);
    }

    private void EnsureController(Scene scene)
    {
        if (_controller is not null && ReferenceEquals(_lastScene, scene) && _lastAnimationName == AnimationName)
            return;

        _controller = new AnimationPlaybackController(scene, AnimationName)
        {
            Speed = Math.Max(Speed, 0.0f),
        };
        _controller.SampleAt(0);
        _lastScene = scene;
        _lastAnimationName = AnimationName;
    }

    public void Dispose() { }
}
