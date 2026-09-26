using System.Diagnostics;

namespace RedFox.Graphics3D;

/// <summary>
/// Binds a <see cref="MorphAnimation"/> to the <see cref="Morph"/> targets of the meshes under a root node and samples weight curves onto them.
/// Tracks are matched to targets by name (case-sensitive). Supports weighted blending via <see cref="AnimationSampler.CurrentWeight"/> and <see cref="AnimationSampler.Mask"/> filtering by target name.
/// </summary>
[DebuggerDisplay("MorphAnimationSampler: {Name}, Bindings = {Bindings.Count}")]
public class MorphAnimationSampler : AnimationSampler
{
    /// <summary>
    /// Gets the resolved bindings, each pairing a morph target with the track that drives its weight.
    /// </summary>
    public List<(Morph Morph, int TargetIndex, MorphAnimationTrack Track)> Bindings { get; } = [];

    /// <summary>
    /// Initializes a new <see cref="MorphAnimationSampler"/> by binding tracks to the morph targets of every mesh under <paramref name="root"/>.
    /// </summary>
    /// <param name="name">Name of this sampler (scene node name).</param>
    /// <param name="animation">The morph animation to sample.</param>
    /// <param name="root">The root node whose meshes receive the sampled weights.</param>
    public MorphAnimationSampler(string name, MorphAnimation animation, SceneNode root) : base(name, animation)
    {
        foreach (Mesh mesh in root.EnumerateHierarchy<Mesh>())
        {
            if (mesh.Morph is not { } morph)
                continue;

            foreach (MorphAnimationTrack track in animation.Tracks)
            {
                int targetIndex = morph.IndexOfTarget(track.Name);

                if (targetIndex >= 0)
                    Bindings.Add((morph, targetIndex, track));
            }
        }
    }

    /// <summary>
    /// Applies sampled weights to all bound targets at the current time, blended by <see cref="AnimationSampler.CurrentWeight"/>.
    /// </summary>
    public override void UpdateObjects()
    {
        float time = CurrentTime - StartFrame;

        foreach ((Morph morph, int targetIndex, MorphAnimationTrack track) in Bindings)
        {
            if (Mask is not null && !Mask.Contains(track.Name))
                continue;

            float sampledWeight = track.SampleWeight(time);
            float currentWeight = morph.Weights[targetIndex];

            morph.Weights[targetIndex] = BlendMode switch
            {
                AnimationBlendMode.Additive => currentWeight + sampledWeight * CurrentWeight,
                _ => float.Lerp(currentWeight, sampledWeight, CurrentWeight),
            };
        }
    }

    /// <inheritdoc/>
    public override bool IsObjectAnimated(string objectName) => Bindings.Exists(binding => binding.Track.Name.Equals(objectName, StringComparison.Ordinal));
}
