using System.Runtime.CompilerServices;

namespace RedFox.Graphics3D;

/// <summary>
/// Abstract base class for sampling an <see cref="Animation"/> at arbitrary
/// times or in a frame-sequential manner. Manages the playback clock,
/// per-layer weight interpolation, blend mode selection, and animation mask
/// filtering. Concrete subclasses implement <see cref="UpdateObjects"/> to
/// apply sampled values to their target scene nodes.
/// <para>
/// As a <see cref="SceneNode"/>, samplers participate in the scene graph and
/// can be children of an <see cref="AnimationPlayer"/>.
/// </para>
/// </summary>
public abstract class AnimationSampler(string name, Animation animation) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the animation being sampled by this layer.
    /// </summary>
    public Animation Animation { get; set; } = animation;

    /// <summary>
    /// Gets the current playback time in frame units.
    /// Updated each time <see cref="Update(float, AnimationSampleType)"/> is called.
    /// </summary>
    public float CurrentTime { get; private set; }

    /// <summary>
    /// Gets or sets the cursor position for sequential weight lookups,
    /// providing O(1) amortized lookup during linear playback.
    /// </summary>
    public int Cursor { get; set; }

    /// <summary>
    /// Gets or sets the weight curve for this layer. The weight modulates
    /// how strongly this layer's values blend with the layers below it.
    /// <para>
    /// Legacy format using <see cref="AnimationKeyFrame{TFrame,TValue}"/>
    /// for compatibility. Can also use <see cref="WeightCurve"/> for
    /// DataBuffer-backed weight animation.
    /// </para>
    /// </summary>
    public List<AnimationKeyFrame<float, float>> Weights { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional DataBuffer-backed weight curve. When set,
    /// this takes priority over the <see cref="Weights"/> list during
    /// <see cref="UpdateWeight"/>.
    /// </summary>
    public AnimationCurve? WeightCurve { get; set; }

    /// <summary>
    /// Gets or sets the current interpolated weight for this layer.
    /// Ranges from 0 (no influence) to 1 (full influence).
    /// Updated automatically by <see cref="UpdateWeight"/>.
    /// </summary>
    public float CurrentWeight { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the blend mode that determines how this layer's sampled
    /// values combine with layers below it in the animation stack.
    /// Defaults to <see cref="AnimationBlendMode.Override"/>.
    /// </summary>
    public AnimationBlendMode BlendMode { get; set; } = AnimationBlendMode.Override;

    /// <summary>
    /// Gets or sets an optional mask that restricts which nodes this layer
    /// can affect. When <see langword="null"/>, all nodes are affected.
    /// Subclass <see cref="UpdateObjects"/> should check this mask.
    /// </summary>
    public AnimationMask? Mask { get; set; }

    /// <summary>
    /// Gets or sets the total frame count of the animation.
    /// Initialized from the animation's frame range on construction.
    /// </summary>
    public float FrameCount { get; set; } = animation.GetAnimationFrameRange().Item2;

    /// <summary>
    /// Gets or sets the playback framerate in frames per second.
    /// Initialized from the animation's framerate on construction.
    /// </summary>
    public float FrameRate { get; set; } = animation.Framerate;

    /// <summary>
    /// Gets the total animation length in seconds.
    /// </summary>
    public float Length => FrameRate > 0f ? FrameCount / FrameRate : 0f;

    /// <summary>
    /// Gets the duration of a single frame in seconds.
    /// </summary>
    public float Frametime => Animation.Framerate > 0f ? 1.0f / Animation.Framerate : 0f;

    /// <summary>
    /// Gets or sets the frame offset at which this sampler begins playback.
    /// The sampler will not call <see cref="UpdateObjects"/> until
    /// <see cref="CurrentTime"/> reaches this value.
    /// </summary>
    public float StartFrame { get; set; }

    // ------------------------------------------------------------------
    // Update overloads — drive playback forward
    // ------------------------------------------------------------------

    /// <summary>Advances by one frame at the current framerate.</summary>
    public void Update() => Update(CurrentTime + Frametime);

    /// <summary>Advances by one frame using the specified time interpretation.</summary>
    public void Update(AnimationSampleType type) => Update(CurrentTime + Frametime, type);

    /// <summary>Updates to an absolute frame time.</summary>
    public new void Update(float time) => Update(time, AnimationSampleType.AbsoluteFrameTime);

    /// <summary>
    /// Core update method. Converts the input time according to
    /// <paramref name="type"/>, updates the layer weight, and calls
    /// <see cref="UpdateObjects"/> if playback has reached <see cref="StartFrame"/>.
    /// </summary>
    /// <param name="time">Time value whose interpretation depends on <paramref name="type"/>.</param>
    /// <param name="type">How to interpret the <paramref name="time"/> parameter.</param>
    /// <returns>This sampler for fluent chaining.</returns>
    public AnimationSampler Update(float time, AnimationSampleType type)
    {
        CurrentTime = type switch
        {
            AnimationSampleType.Percentage     => FrameCount * time,
            AnimationSampleType.AbsoluteTime   => time * FrameRate,
            AnimationSampleType.DeltaTime       => CurrentTime + time * FrameRate,
            _                                   => time, // AbsoluteFrameTime
        };

        UpdateWeight();

        if (CurrentTime >= StartFrame)
            UpdateObjects();

        return this;
    }

    /// <summary>
    /// Interpolates the current weight from either <see cref="WeightCurve"/>
    /// (DataBuffer-backed) or the legacy <see cref="Weights"/> list.
    /// If neither contains keyframes, the weight remains at its current value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateWeight()
    {
        // Prefer the DataBuffer-backed weight curve when available
        if (WeightCurve is not null && WeightCurve.KeyFrameCount > 0)
        {
            CurrentWeight = WeightCurve.SampleScalar(CurrentTime);
            return;
        }

        // Fall back to legacy keyframe list
        if (Weights.Count == 0) return;

        var (firstIndex, secondIndex) = Animation.GetFramePairIndex(Weights, CurrentTime, 0f, Cursor);
        if (firstIndex < 0) return;

        if (firstIndex == secondIndex)
        {
            CurrentWeight = Weights[firstIndex].Value;
        }
        else
        {
            var first = Weights[firstIndex];
            var second = Weights[secondIndex];
            var t = (CurrentTime - first.Frame) / (second.Frame - first.Frame);
            CurrentWeight = first.Value * (1f - t) + second.Value * t;
        }

        Cursor = firstIndex;
    }

    /// <summary>
    /// Resets the playback state — time, weight, and cursor — to their defaults.
    /// </summary>
    public void Reset()
    {
        CurrentTime = 0f;
        CurrentWeight = 1.0f;
        Cursor = 0;
    }

    /// <summary>
    /// Determines whether the specified object (by name) has any animation
    /// data in this sampler's bound tracks.
    /// </summary>
    /// <param name="objectName">Name of the node/bone to check.</param>
    /// <returns><see langword="true"/> if the object is animated.</returns>
    public abstract bool IsObjectAnimated(string objectName);

    /// <summary>
    /// Applies sampled animation values to all bound target nodes at the
    /// current time. Called by <see cref="Update(float, AnimationSampleType)"/>
    /// after the playback clock and weight have been updated.
    /// </summary>
    public abstract void UpdateObjects();
}
