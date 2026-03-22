// ------------------------------------------------------------------------
// SkeletonAnimationSampler.cs — Samples skeletal animation onto bones
// ------------------------------------------------------------------------
// Binds a SkeletonAnimation's tracks to actual SkeletonBone nodes and
// applies interpolated transform values each frame. Supports weighted
// blending (via AnimationSampler.CurrentWeight), blend modes (Override /
// Additive), animation masks, and all transform spaces/types.
// Works with the new DataBuffer-backed AnimationCurve system.
// ------------------------------------------------------------------------

using System.Diagnostics;
using System.Numerics;

namespace RedFox.Graphics3D.Skeletal;

/// <summary>
/// Binds a <see cref="SkeletonAnimation"/> to a <see cref="Skeleton"/> and samples
/// the animation's tracks onto matching bones each frame. Supports weighted blending
/// via <see cref="AnimationSampler.CurrentWeight"/> and optional
/// <see cref="AnimationSampler.Mask"/> filtering.
/// <para>
/// This sampler resolves bone-to-track bindings once at construction time by
/// iterating the skeleton hierarchy and matching track names, making per-frame
/// sampling O(bound tracks) rather than O(tracks × bones).
/// </para>
/// </summary>
public class SkeletonAnimationSampler : AnimationSampler
{
    /// <summary>
    /// Pre-resolved binding pairs: each entry maps a <see cref="SkeletonBone"/>
    /// to its corresponding <see cref="SkeletonAnimationTrack"/>.
    /// </summary>
    public List<KeyValuePair<SkeletonBone, SkeletonAnimationTrack>> Tracks { get; set; }

    /// <summary>
    /// Initializes a new <see cref="SkeletonAnimationSampler"/> that binds the
    /// given animation's tracks to matching bones in the skeleton hierarchy.
    /// Tracks are matched by name (case-sensitive).
    /// </summary>
    /// <param name="name">Name of this sampler (used as scene node name).</param>
    /// <param name="animation">The skeletal animation to sample.</param>
    /// <param name="skeleton">The skeleton whose bones receive the sampled values.</param>
    public SkeletonAnimationSampler(string name, SkeletonAnimation animation, Skeleton skeleton)
        : base(name, animation)
    {
        Tracks = [];

        foreach (var bone in skeleton.EnumerateHierarchy<SkeletonBone>())
        {
            // Find the corresponding track for this bone (matched by name)
            var track = animation.Tracks.Find(x => x.Name.Equals(bone.Name));

            if (track is not null)
                Tracks.Add(new(bone, track));
        }
    }

    /// <summary>
    /// Applies all bound track curves to their target bones at the current time.
    /// Respects the layer weight (<see cref="AnimationSampler.CurrentWeight"/>),
    /// blend mode (<see cref="AnimationSampler.BlendMode"/>), and optional mask.
    /// </summary>
    public override void UpdateObjects()
    {
        var currentTimeRelative = CurrentTime - StartFrame;

        foreach (var (bone, track) in Tracks)
        {
            // Skip this bone if a mask is active and doesn't include it
            if (Mask is not null && !Mask.Contains(bone.Name))
                continue;

            ApplyTranslation(bone, track, currentTimeRelative);
            ApplyRotation(bone, track, currentTimeRelative);
            ApplyScale(bone, track, currentTimeRelative);
        }
    }

    /// <summary>
    /// Applies the translation curve to the specified bone, handling transform
    /// space (local/world), transform type (absolute/relative/additive), and
    /// layer blend mode/weight.
    /// </summary>
    private void ApplyTranslation(SkeletonBone bone, SkeletonAnimationTrack track, float time)
    {
        if (track.TranslationCurve is null) return;

        var value = track.TranslationCurve.SampleVector3(time);

        if (track.TranslationCurve.TransformSpace == TransformSpace.World)
        {
            var resolved = track.TranslationCurve.TransformType switch
            {
                TransformType.Additive => bone.GetActiveWorldPosition() + value,
                TransformType.Relative => bone.GetBindWorldPosition() + value,
                TransformType.Absolute => value,
                _ => value
            };

            bone.LiveTransform.WorldPosition = BlendMode switch
            {
                AnimationBlendMode.Additive => bone.GetActiveWorldPosition() + (resolved - bone.GetBindWorldPosition()) * CurrentWeight,
                _ => Vector3.Lerp(bone.GetActiveWorldPosition(), resolved, CurrentWeight), // Override
            };
            bone.LiveTransform.LocalPosition = null;
        }
        else
        {
            var resolved = track.TranslationCurve.TransformType switch
            {
                TransformType.Additive => bone.GetLiveLocalPosition() + value,
                TransformType.Relative => bone.GetBindLocalPosition() + value,
                TransformType.Absolute => value,
                _ => value
            };

            bone.LiveTransform.LocalPosition = BlendMode switch
            {
                AnimationBlendMode.Additive => bone.GetLiveLocalPosition() + (resolved - bone.GetBindLocalPosition()) * CurrentWeight,
                _ => Vector3.Lerp(bone.GetLiveLocalPosition(), resolved, CurrentWeight), // Override
            };
            bone.LiveTransform.WorldPosition = null;
        }
    }

    /// <summary>
    /// Applies the rotation curve to the specified bone, handling transform
    /// space (local/world), transform type (absolute/relative/additive), and
    /// layer blend mode/weight.
    /// </summary>
    private void ApplyRotation(SkeletonBone bone, SkeletonAnimationTrack track, float time)
    {
        if (track.RotationCurve is null) return;

        var value = track.RotationCurve.SampleQuaternion(time);

        if (track.RotationCurve.TransformSpace == TransformSpace.World)
        {
            var resolved = track.RotationCurve.TransformType switch
            {
                TransformType.Additive => bone.GetActiveWorldRotation() * value,
                TransformType.Relative => bone.GetBindWorldRotation() * value,
                TransformType.Absolute => value,
                _ => value
            };

            bone.LiveTransform.WorldRotation = BlendMode switch
            {
                AnimationBlendMode.Additive => Quaternion.Normalize(bone.GetActiveWorldRotation() * Quaternion.Slerp(Quaternion.Identity, value, CurrentWeight)),
                _ => Quaternion.Slerp(bone.GetActiveWorldRotation(), Quaternion.Normalize(resolved), CurrentWeight), // Override
            };
            bone.LiveTransform.LocalRotation = null;
        }
        else
        {
            var resolved = track.RotationCurve.TransformType switch
            {
                TransformType.Additive => bone.GetLiveLocalRotation() * value,
                TransformType.Relative => bone.GetBindLocalRotation() * value,
                TransformType.Absolute => value,
                _ => value
            };

            bone.LiveTransform.LocalRotation = BlendMode switch
            {
                AnimationBlendMode.Additive => Quaternion.Normalize(bone.GetLiveLocalRotation() * Quaternion.Slerp(Quaternion.Identity, value, CurrentWeight)),
                _ => Quaternion.Slerp(bone.GetLiveLocalRotation(), Quaternion.Normalize(resolved), CurrentWeight), // Override
            };
            bone.LiveTransform.WorldRotation = null;
        }
    }

    /// <summary>
    /// Applies the scale curve to the specified bone.
    /// </summary>
    private void ApplyScale(SkeletonBone bone, SkeletonAnimationTrack track, float time)
    {
        if (track.ScaleCurve is null) return;

        var value = track.ScaleCurve.SampleVector3(time);
        var current = bone.GetLiveLocalScale();

        bone.LiveTransform.Scale = BlendMode switch
        {
            AnimationBlendMode.Additive => current + (value - Vector3.One) * CurrentWeight,
            _ => Vector3.Lerp(current, value, CurrentWeight), // Override
        };
    }

    /// <summary>
    /// Sets the transform type on all bound tracks' curves.
    /// </summary>
    /// <param name="transformType">The transform type to apply.</param>
    public void SetTransformType(TransformType transformType)
    {
        foreach (var (_, track) in Tracks)
        {
            if (track.TranslationCurve is not null)
                track.TranslationCurve.TransformType = transformType;
            if (track.RotationCurve is not null)
                track.RotationCurve.TransformType = transformType;
            if (track.ScaleCurve is not null)
                track.ScaleCurve.TransformType = transformType;
        }
    }

    /// <inheritdoc/>
    public override bool IsObjectAnimated(string objectName) =>
        Tracks.Exists(x => x.Key.Name.Equals(objectName, StringComparison.Ordinal));
}
