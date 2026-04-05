using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.OpenGL.Viewing;

/// <summary>
/// Drives animation playback for a scene by managing skeleton and blend shape animation samplers.
/// </summary>
public sealed class AnimationPlaybackController
{
    private readonly Scene _scene;
    private readonly List<AnimationSampler> _samplers = [];
    private readonly BlendShape[] _blendShapes;
    private float _elapsedSeconds;

    /// <summary>
    /// Creates a new playback controller for the specified scene, optionally filtering by animation name.
    /// </summary>
    /// <param name="scene">The scene containing the animations to play.</param>
    /// <param name="animationName">An optional animation name filter; when specified, only matching animations are loaded.</param>
    public AnimationPlaybackController(Scene scene, string? animationName = null)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _blendShapes = [.. scene.RootNode.EnumerateDescendants<BlendShape>()];

        InitializeSkeletonSamplers(animationName);
        InitializeBlendShapeSamplers(animationName);
    }

    /// <summary>The animation samplers managed by this controller.</summary>
    public IReadOnlyList<AnimationSampler> Samplers => _samplers;

    /// <summary>The playback speed multiplier. A value of 1.0 plays at normal speed.</summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>The total duration of the longest active animation in seconds.</summary>
    public float DurationSeconds => _samplers.Count == 0 ? 0.0f : _samplers.Max(sampler => sampler.Length);

    /// <summary>The highest frame rate among all active animation samplers.</summary>
    public float FrameRate => _samplers.Count == 0 ? 0.0f : _samplers.Max(sampler => sampler.FrameRate);

    /// <summary>The time between frames at the current frame rate, in seconds.</summary>
    public float FrameTime => FrameRate > 0.0f ? 1.0f / FrameRate : 0.0f;

    /// <summary>Gets or sets the current playback position in seconds.</summary>
    public float ElapsedSeconds
    {
        get => _elapsedSeconds;
        set => _elapsedSeconds = Math.Max(value, 0.0f);
    }

    /// <summary>
    /// Advances the playback position by the specified delta time and applies the resulting animation samples.
    /// </summary>
    /// <param name="deltaTime">The time to advance in seconds.</param>
    public void Update(float deltaTime)
    {
        if (_samplers.Count == 0)
            return;

        _elapsedSeconds += deltaTime * Speed;
        float duration = DurationSeconds;
        if (duration > 0.0f)
            _elapsedSeconds %= duration;

        SampleAt(_elapsedSeconds);
    }

    /// <summary>
    /// Samples all animations at the specified elapsed time and applies the results to the scene.
    /// </summary>
    /// <param name="elapsedSeconds">The absolute time in seconds at which to sample.</param>
    public void SampleAt(float elapsedSeconds)
    {
        _elapsedSeconds = Math.Max(elapsedSeconds, 0.0f);

        foreach (SceneNode node in _scene.RootNode.EnumerateDescendants())
            node.ResetLiveTransform();

        foreach (BlendShape blendShape in _blendShapes)
            blendShape.Weight = 0.0f;

        foreach (AnimationSampler sampler in _samplers)
        {
            float sampleTime;
            if (sampler.Length <= 0f)
            {
                sampleTime = 0f;
            }
            else if (_elapsedSeconds <= sampler.Length || MathF.Abs(_elapsedSeconds - sampler.Length) < 1e-5f)
            {
                sampleTime = _elapsedSeconds;
            }
            else
            {
                sampleTime = _elapsedSeconds % sampler.Length;
            }

            sampleTime = Math.Clamp(sampleTime, 0.0f, sampler.Length);

            sampler.Update(sampleTime, AnimationSampleType.AbsoluteTime);
        }
    }

    private void InitializeSkeletonSamplers(string? animationName)
    {
        IEnumerable<SkeletonAnimation> animations = _scene.RootNode.EnumerateDescendants<SkeletonAnimation>();
        if (!string.IsNullOrWhiteSpace(animationName))
            animations = animations.Where(animation => animation.Name.Equals(animationName, StringComparison.OrdinalIgnoreCase));

        foreach (SkeletonAnimation animation in animations)
        {
            Skeleton? skeleton = animation.Skeleton ?? ResolveSkeleton(animation);
            if (skeleton is null)
                continue;

            _samplers.Add(new SkeletonAnimationSampler($"{animation.Name}_Preview", animation, skeleton));
        }
    }

    private void InitializeBlendShapeSamplers(string? animationName)
    {
        IEnumerable<BlendShapeAnimation> animations = _scene.RootNode.EnumerateDescendants<BlendShapeAnimation>();
        if (!string.IsNullOrWhiteSpace(animationName))
            animations = animations.Where(animation => animation.Name.Equals(animationName, StringComparison.OrdinalIgnoreCase));

        if (_blendShapes.Length == 0)
            return;

        foreach (BlendShapeAnimation animation in animations)
            _samplers.Add(new BlendShapeSampler($"{animation.Name}_Preview", animation, _blendShapes));
    }

    private Skeleton? ResolveSkeleton(SkeletonAnimation animation)
    {
        Skeleton? ancestorSkeleton = animation.EnumerateAncestors().OfType<Skeleton>().FirstOrDefault();
        if (ancestorSkeleton is not null)
            return ancestorSkeleton;

        return animation.Scene?.RootNode.TryGetFirstOfType<Skeleton>();
    }
}
