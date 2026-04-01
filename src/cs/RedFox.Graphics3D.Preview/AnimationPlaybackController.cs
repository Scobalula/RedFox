using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Preview;

public sealed class AnimationPlaybackController
{
    private readonly Scene _scene;
    private readonly List<AnimationSampler> _samplers = [];
    private readonly BlendShape[] _blendShapes;
    private float _elapsedSeconds;

    public AnimationPlaybackController(Scene scene, string? animationName = null)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _blendShapes = [.. scene.RootNode.EnumerateDescendants<BlendShape>()];

        InitializeSkeletonSamplers(animationName);
        InitializeBlendShapeSamplers(animationName);
    }

    public IReadOnlyList<AnimationSampler> Samplers => _samplers;
    public float Speed { get; set; } = 1.0f;

    public void Update(float deltaTime)
    {
        if (_samplers.Count == 0)
            return;

        _elapsedSeconds += deltaTime * Speed;

        foreach (SceneNode node in _scene.RootNode.EnumerateDescendants())
            node.ResetLiveTransform();

        foreach (BlendShape blendShape in _blendShapes)
            blendShape.Weight = 0.0f;

        foreach (AnimationSampler sampler in _samplers)
        {
            float sampleTime = sampler.Length > 0f
                ? _elapsedSeconds % sampler.Length
                : 0f;

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
