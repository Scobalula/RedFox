using System.Numerics;

namespace RedFox.Graphics3D.Skeletal;

/// <summary>
/// Represents a skeletal animation composed of per-bone
/// <see cref="SkeletonAnimationTrack"/> instances, each containing
/// DataBuffer-backed <see cref="AnimationCurve"/> data for translation,
/// rotation, and scale.
/// <para>
/// Extends <see cref="Animation"/> (which extends <see cref="SceneNode"/>),
/// making it a first-class member of the scene graph.
/// </para>
/// </summary>
public class SkeletonAnimation : Animation
{
    /// <summary>
    /// Gets or sets the skeleton tied to this animation, if any.
    /// When set, the sampler uses this to resolve bone references.
    /// </summary>
    public Skeleton? Skeleton { get; set; }

    /// <summary>
    /// Gets or sets the per-bone animation tracks. Each track contains
    /// translation, rotation, and/or scale curves for a single bone.
    /// </summary>
    public List<SkeletonAnimationTrack> Tracks { get; set; }

    /// <summary>
    /// Gets or sets the global transform type applied to all tracks
    /// that do not specify their own. Determines how keyframe values
    /// relate to the base pose (absolute, relative, additive, etc.).
    /// </summary>
    public TransformType TransformType { get; set; }

    /// <summary>
    /// Gets or sets the global transform space for this animation.
    /// Determines whether values are in local-bone or world space.
    /// </summary>
    public TransformSpace TransformSpace { get; set; }

    /// <summary>
    /// Initializes a new <see cref="SkeletonAnimation"/> with the specified name,
    /// defaulting to 30 fps and unknown transform type.
    /// </summary>
    /// <param name="name">The animation name.</param>
    public SkeletonAnimation(string name) : base(name)
    {
        Tracks = [];
        TransformType = TransformType.Unknown;
        Framerate = 30;
    }

    /// <summary>
    /// Initializes a new <see cref="SkeletonAnimation"/> bound to the given skeleton.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="skeleton">The target skeleton (may be <see langword="null"/>).</param>
    public SkeletonAnimation(string name, Skeleton? skeleton) : base(name)
    {
        Tracks = [];
        Skeleton = skeleton;
    }

    /// <summary>
    /// Initializes a new <see cref="SkeletonAnimation"/> with pre-allocated track
    /// capacity and a specified transform type.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="skeleton">The target skeleton.</param>
    /// <param name="targetCount">Initial track list capacity.</param>
    /// <param name="type">The global transform type.</param>
    public SkeletonAnimation(string name, Skeleton? skeleton, int targetCount, TransformType type) : base(name)
    {
        Skeleton = skeleton;
        Tracks = new(targetCount);
        TransformType = type;
    }

    /// <summary>
    /// Computes the minimum and maximum keyframe times across all tracks in this
    /// animation, considering translation, rotation, and scale curves.
    /// </summary>
    /// <returns>
    /// A tuple of (minFrame, maxFrame). Returns <c>(float.MaxValue, float.MinValue)</c>
    /// if no keyframes exist.
    /// </returns>
    public override (float, float) GetAnimationFrameRange()
    {
        var minFrame = float.MaxValue;
        var maxFrame = float.MinValue;

        foreach (var track in Tracks)
        {
            var (trackMin, trackMax) = track.GetTimeRange();
            minFrame = MathF.Min(minFrame, trackMin);
            maxFrame = MathF.Max(maxFrame, trackMax);
        }

        return (minFrame, maxFrame);
    }

    /// <summary>
    /// Overrides the <see cref="AnimationCurve.TransformType"/> on every curve
    /// in every track to the specified value. Useful when re-interpreting imported
    /// animation data.
    /// </summary>
    /// <param name="transformType">The transform type to set globally.</param>
    public void SetTransformType(TransformType transformType)
    {
        foreach (var track in Tracks)
        {
            track.TranslationCurve?.TransformType = transformType;
            track.RotationCurve?.TransformType = transformType;
            track.ScaleCurve?.TransformType = transformType;
        }
    }

    /// <summary>
    /// Overrides the <see cref="AnimationCurve.TransformSpace"/> on every curve
    /// in every track to the specified value.
    /// </summary>
    /// <param name="transformSpace">The transform space to set globally.</param>
    public void SetTransformSpace(TransformSpace transformSpace)
    {
        foreach (var track in Tracks)
        {
            track.TranslationCurve?.TransformSpace = transformSpace;
            track.RotationCurve?.TransformSpace = transformSpace;
            track.ScaleCurve?.TransformSpace = transformSpace;
        }
    }
}
