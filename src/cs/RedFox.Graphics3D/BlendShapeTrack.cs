
using System.Diagnostics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// An animation track that drives a single <see cref="BlendShape"/>'s weight
    /// over time. Contains a scalar <see cref="AnimationCurve"/> whose sampled
    /// value is applied as the morph target weight during playback.
    /// <para>
    /// The track's <see cref="SceneNode.Name"/> should match the
    /// <see cref="BlendShape.Name"/> it targets, enabling name-based binding
    /// in the <see cref="BlendShapeSampler"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="BlendShapeTrack"/> targeting the blend
    /// shape with the specified name.
    /// </remarks>
    /// <param name="name">Name of the blend shape this track targets.</param>
    [DebuggerDisplay("BlendShapeTrack: {Name}, KeyFrames = {WeightCurve?.KeyFrameCount ?? 0}")]
    public class BlendShapeTrack(string name) : SceneNode(name)
    {
        /// <summary>
        /// Gets or sets the scalar weight curve. Each keyframe stores a
        /// float weight value (typically 0–1, but overdrive is allowed).
        /// </summary>
        public AnimationCurve? WeightCurve { get; set; }

        /// <summary>
        /// Gets the number of keyframes in the weight curve.
        /// </summary>
        public int KeyFrameCount => WeightCurve?.KeyFrameCount ?? 0;

        /// <summary>
        /// Appends a weight keyframe. Lazily creates the weight curve if needed.
        /// </summary>
        /// <param name="time">Keyframe time (frames or seconds).</param>
        /// <param name="weight">The blend shape weight at this keyframe.</param>
        public void AddWeightFrame(float time, float weight)
        {
            WeightCurve ??= AnimationCurve.CreateScalar();
            WeightCurve.Add(time, weight);
        }

        /// <summary>
        /// Samples the weight curve at the specified time.
        /// Returns 0 if no keyframes exist.
        /// </summary>
        /// <param name="time">The time to sample at.</param>
        /// <returns>The interpolated weight value.</returns>
        public float SampleWeight(float time)
        {
            return WeightCurve?.SampleScalar(time) ?? 0f;
        }

        /// <summary>
        /// Gets the time range spanned by this track's weight keyframes.
        /// </summary>
        /// <returns>A tuple of (startTime, endTime).</returns>
        public (float Start, float End) GetTimeRange()
        {
            if (WeightCurve is null || WeightCurve.KeyFrameCount == 0)
                return (0f, 0f);
            return (WeightCurve.StartTime, WeightCurve.EndTime);
        }
    }
}
