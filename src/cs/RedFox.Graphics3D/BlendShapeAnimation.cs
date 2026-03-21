
namespace RedFox.Graphics3D
{
    /// <summary>
    /// An <see cref="Animation"/> that drives <see cref="BlendShape"/> weights
    /// over time. Contains a list of <see cref="BlendShapeTrack"/> instances,
    /// each animating a single morph target's weight via a scalar
    /// <see cref="AnimationCurve"/>.
    /// <para>
    /// Pair with <see cref="BlendShapeSampler"/> for playback.
    /// </para>
    /// </summary>
    public class BlendShapeAnimation : Animation
    {
        /// <summary>
        /// Gets or sets the per-target weight animation tracks.
        /// </summary>
        public List<BlendShapeTrack> Tracks { get; set; } = [];

        /// <summary>
        /// Initializes a new <see cref="BlendShapeAnimation"/> with the given name
        /// and a default framerate of 30 fps.
        /// </summary>
        /// <param name="name">The animation name.</param>
        public BlendShapeAnimation(string name) : base(name)
        {
            Framerate = 30;
        }

        /// <summary>
        /// Creates or retrieves a <see cref="BlendShapeTrack"/> with the given target name.
        /// If a track already exists for that target, it is returned unchanged.
        /// </summary>
        /// <param name="targetName">Name of the blend shape to animate.</param>
        /// <returns>The existing or newly created track.</returns>
        public BlendShapeTrack GetOrCreateTrack(string targetName)
        {
            var existing = Tracks.Find(t => t.Name.Equals(targetName, StringComparison.Ordinal));
            if (existing is not null)
                return existing;

            var track = new BlendShapeTrack(targetName);
            Tracks.Add(track);
            return track;
        }

        /// <summary>
        /// Computes the minimum and maximum keyframe times across all blend shape tracks.
        /// </summary>
        /// <returns>
        /// A tuple of (minFrame, maxFrame). Returns (float.MaxValue, float.MinValue)
        /// if no tracks have keyframes.
        /// </returns>
        public override (float, float) GetAnimationFrameRange()
        {
            var min = float.MaxValue;
            var max = float.MinValue;

            foreach (var track in Tracks)
            {
                if (track.WeightCurve is null || track.WeightCurve.KeyFrameCount == 0)
                    continue;

                min = MathF.Min(min, track.WeightCurve.StartTime);
                max = MathF.Max(max, track.WeightCurve.EndTime);
            }

            return (min, max);
        }
    }
}
