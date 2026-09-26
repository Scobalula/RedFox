using System.Diagnostics;

namespace RedFox.Graphics3D;

/// <summary>
/// An animation track that drives the weight of a single <see cref="Morph"/> target over time.
/// The track's name matches the target name in <see cref="Morph.TargetNames"/>, enabling name-based binding in the <see cref="MorphAnimationSampler"/>.
/// </summary>
/// <param name="name">Name of the morph target this track animates.</param>
[DebuggerDisplay("MorphAnimationTrack: {Name}, KeyFrames = {KeyFrameCount}")]
public class MorphAnimationTrack(string name) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the scalar weight curve. Each keyframe stores a float weight value (typically 0–1, but overdrive is allowed).
    /// </summary>
    public AnimationCurve? WeightCurve { get; set; }

    /// <summary>
    /// Gets the number of keyframes in the weight curve.
    /// </summary>
    public int KeyFrameCount => WeightCurve?.KeyFrameCount ?? 0;

    /// <summary>
    /// Appends a weight keyframe, creating the weight curve if needed.
    /// </summary>
    /// <param name="time">Keyframe time (frames or seconds).</param>
    /// <param name="weight">The target weight at this keyframe.</param>
    public void AddWeightFrame(float time, float weight)
    {
        WeightCurve ??= AnimationCurve.CreateScalar();
        WeightCurve.Add(time, weight);
    }

    /// <summary>
    /// Samples the weight curve at the specified time.
    /// </summary>
    /// <param name="time">The time to sample at.</param>
    /// <returns>The interpolated weight, or 0 when no keyframes exist.</returns>
    public float SampleWeight(float time) => WeightCurve?.SampleScalar(time) ?? 0f;
}
