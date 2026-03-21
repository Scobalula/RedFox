
using System.Diagnostics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Binds a <see cref="BlendShapeAnimation"/> to a set of <see cref="BlendShape"/>
    /// targets and samples weight curves onto them each frame. Supports weighted
    /// blending via <see cref="AnimationSampler.CurrentWeight"/> and optional
    /// <see cref="AnimationSampler.Mask"/> filtering.
    /// <para>
    /// Track-to-target binding is resolved at construction time by matching names.
    /// </para>
    /// </summary>
    [DebuggerDisplay("BlendShapeSampler: {Name}, Bindings = {Bindings.Count}")]
    public class BlendShapeSampler : AnimationSampler
    {
        /// <summary>
        /// Pre-resolved binding pairs: each entry maps a <see cref="BlendShape"/>
        /// to its corresponding <see cref="BlendShapeTrack"/>.
        /// </summary>
        public List<KeyValuePair<BlendShape, BlendShapeTrack>> Bindings { get; set; }

        /// <summary>
        /// Initializes a new <see cref="BlendShapeSampler"/> by binding animation
        /// tracks to blend shape targets. Targets are matched by name (case-sensitive).
        /// </summary>
        /// <param name="name">Name of this sampler (scene node name).</param>
        /// <param name="animation">The blend shape animation to sample.</param>
        /// <param name="targets">The blend shape targets to update.</param>
        public BlendShapeSampler(string name, BlendShapeAnimation animation, IEnumerable<BlendShape> targets)
            : base(name, animation)
        {
            Bindings = [];

            foreach (var target in targets)
            {
                var track = animation.Tracks.Find(t => t.Name.Equals(target.Name, StringComparison.Ordinal));
                if (track is not null)
                    Bindings.Add(new(target, track));
            }
        }

        /// <summary>
        /// Applies sampled blend shape weights to all bound targets at the current time.
        /// The layer weight (<see cref="AnimationSampler.CurrentWeight"/>) modulates
        /// the sampled value via linear interpolation with the target's existing weight.
        /// </summary>
        public override void UpdateObjects()
        {
            var time = CurrentTime - StartFrame;

            foreach (var (target, track) in Bindings)
            {
                // Skip if mask is active and doesn't include this target
                if (Mask is not null && !Mask.Contains(target.Name))
                    continue;

                var sampledWeight = track.SampleWeight(time);

                // Blend with existing weight using the layer weight
                target.Weight = BlendMode switch
                {
                    AnimationBlendMode.Additive => target.Weight + sampledWeight * CurrentWeight,
                    _ => float.Lerp(target.Weight, sampledWeight, CurrentWeight), // Override
                };
            }
        }

        /// <inheritdoc/>
        public override bool IsObjectAnimated(string objectName) =>
            Bindings.Exists(b => b.Key.Name.Equals(objectName, StringComparison.Ordinal));
    }
}
