using System.Numerics;
using System.Text;

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
    /// Initializes a skeletal animation with an empty track collection and a default framerate of 30 frames per second.
    /// </summary>
    public SkeletonAnimation() : base()
    {
        Tracks = [];
        TransformType = TransformType.Unknown;
        Framerate = 30;
    }

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
    /// Initializes a new <see cref="SkeletonAnimation"/> with pre-allocated track
    /// capacity and a specified transform type.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="targetCount">Initial track list capacity.</param>
    /// <param name="type">The global transform type.</param>
    public SkeletonAnimation(string name, int targetCount, TransformType type) : base(name)
    {
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

    /// <summary>
    /// Validates that every track targets exactly one bone under <paramref name="skeletonRoot"/>
    /// and that its curves are well formed.
    /// </summary>
    /// <param name="skeletonRoot">The node whose bone hierarchy the animation is applied to.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    public bool Validate(SceneNode skeletonRoot) => Validate(skeletonRoot, messages: null);

    /// <summary>
    /// Validates that every track targets exactly one bone under <paramref name="skeletonRoot"/>
    /// and that its curves are well formed: one value per key, the expected component count, and keys
    /// in ascending time order. Tracks bind to bones by case-sensitive name, as in <see cref="SkeletonAnimationSampler"/>.
    /// </summary>
    /// <param name="skeletonRoot">The node whose bone hierarchy the animation is applied to.</param>
    /// <param name="messages">When not <see langword="null"/>, receives a human-readable description of every problem found.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    public bool Validate(SceneNode skeletonRoot, StringBuilder? messages)
    {
        ArgumentNullException.ThrowIfNull(skeletonRoot);

        Dictionary<string, int> boneCounts = skeletonRoot.EnumerateHierarchy<SkeletonBone>().CountBy(bone => bone.Name, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        List<string> problems = [];

        foreach (SkeletonAnimationTrack track in Tracks)
        {
            int boneCount = boneCounts.GetValueOrDefault(track.Name);

            if (boneCount != 1)
                problems.Add($"track '{track.Name}' matches {boneCount} bones; exactly one is required.");

            CheckCurve(problems, track.Name, "translation", track.TranslationCurve, 3);
            CheckCurve(problems, track.Name, "rotation", track.RotationCurve, 4);
            CheckCurve(problems, track.Name, "scale", track.ScaleCurve, 3);
        }

        foreach (string problem in problems)
            messages?.AppendLine($"Animation '{Name}' {problem}");

        return problems.Count == 0;
    }

    private static void CheckCurve(List<string> problems, string trackName, string curveName, AnimationCurve? curve, int componentCount)
    {
        if (curve is null)
            return;

        int valueCount = curve.Values?.ElementCount ?? 0;

        if (valueCount != curve.KeyFrameCount)
            problems.Add($"track '{trackName}' {curveName} curve has {curve.KeyFrameCount} keys but {valueCount} values.");

        if (curve.KeyFrameCount > 0 && curve.ComponentCount != componentCount)
            problems.Add($"track '{trackName}' {curveName} curve has {curve.ComponentCount} components per value but {componentCount} are required.");

        for (int key = 1; key < curve.KeyFrameCount; key++)
        {
            if (curve.GetKeyTime(key) < curve.GetKeyTime(key - 1))
            {
                problems.Add($"track '{trackName}' {curveName} curve keys are not in ascending time order at key {key}.");
                return;
            }
        }
    }
}
