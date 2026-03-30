using System.Globalization;
using System.Numerics;
using System.Text;

using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Bvh;

/// <summary>
/// Writes scene data as Biovision Hierarchy (<c>.bvh</c>) text files.
/// </summary>
public sealed class BvhWriter
{
    /// <summary>
    /// Gets the minimum acceptable quaternion alignment when converting sampled rotations back to Euler angles.
    /// </summary>
    public const float RotationAlignmentTolerance = 0.9999f;

    /// <summary>
    /// Gets the tolerance used when comparing vectors during BVH validation.
    /// </summary>
    public const float VectorTolerance = 0.0001f;

    /// <summary>
    /// Gets the destination stream that receives BVH text data.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Gets the logical scene or file name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the translator options used by this writer.
    /// </summary>
    public SceneTranslatorOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BvhWriter"/>.
    /// </summary>
    /// <param name="stream">The stream that will receive BVH text data.</param>
    /// <param name="name">The logical scene or file name.</param>
    /// <param name="options">Options that control translator behaviour.</param>
    public BvhWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        Stream = stream;
        Name = name;
        Options = options;
    }

    /// <summary>
    /// Serializes the supplied scene to the output stream in BVH format.
    /// </summary>
    /// <param name="scene">The scene to serialize.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">Thrown when the destination stream cannot be written.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the scene contains data that cannot be represented by BVH.</exception>
    public void Write(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (!Stream.CanWrite)
        {
            throw new IOException("The supplied BVH stream is not writable.");
        }

        _ = Options;

        ValidateSupportedNodes(scene);

        Skeleton skeleton = GetSingleSkeleton(scene);
        SkeletonBone rootBone = GetSingleRootBone(skeleton);
        SkeletonAnimation? animation = GetSingleAnimation(scene);
        SkeletonBone[] bones = skeleton.GetBones();
        Dictionary<string, SkeletonAnimationTrack> tracksByName = BuildTrackMap(animation, bones);
        int frameCount = GetFrameCount(animation);
        float frameTime = GetFrameTime(animation);
        Dictionary<SkeletonBone, BvhChannelType[]> channelsByBone = BuildChannelMap(bones, rootBone, tracksByName);

        ValidateTracksAndBindTransforms(bones, tracksByName);

        using StreamWriter writer = new(Stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096, leaveOpen: true);
        WriteHierarchy(writer, rootBone, channelsByBone, 0, true);
        WriteMotion(writer, bones, animation, tracksByName, channelsByBone, frameCount, frameTime);
        writer.Flush();
    }

    /// <summary>
    /// Validates that the scene contains only nodes representable by BVH.
    /// </summary>
    /// <param name="scene">The scene to validate.</param>
    public static void ValidateSupportedNodes(Scene scene)
    {
        foreach (SceneNode node in scene.RootNode.EnumerateDescendants())
        {
            if (node is Skeleton or SkeletonBone or SkeletonAnimation)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"BVH export cannot represent scene node '{node.Name}' of type '{node.GetType().Name}'. BVH only supports a skeleton hierarchy and a single skeleton animation clip.");
        }
    }

    /// <summary>
    /// Gets the single skeleton that can be exported from the scene.
    /// </summary>
    /// <param name="scene">The scene to inspect.</param>
    /// <returns>The single exportable skeleton.</returns>
    public static Skeleton GetSingleSkeleton(Scene scene)
    {
        Skeleton[] skeletons = scene.GetDescendants<Skeleton>();
        if (skeletons.Length == 0)
        {
            throw new InvalidOperationException("BVH export requires exactly one Skeleton node, but the scene does not contain one.");
        }

        if (skeletons.Length > 1)
        {
            throw new InvalidOperationException("BVH export requires exactly one Skeleton node, but the scene contains multiple skeletons.");
        }

        return skeletons[0];
    }

    /// <summary>
    /// Gets the single animation clip that can be exported from the scene.
    /// </summary>
    /// <param name="scene">The scene to inspect.</param>
    /// <returns>The single exportable animation, or <see langword="null"/> when the scene is static.</returns>
    public static SkeletonAnimation? GetSingleAnimation(Scene scene)
    {
        SkeletonAnimation[] animations = scene.GetDescendants<SkeletonAnimation>();
        if (animations.Length > 1)
        {
            throw new InvalidOperationException("BVH export supports a single SkeletonAnimation clip per file.");
        }

        return animations.Length == 0 ? null : animations[0];
    }

    /// <summary>
    /// Gets the single root bone that can be exported from the skeleton.
    /// </summary>
    /// <param name="skeleton">The skeleton to inspect.</param>
    /// <returns>The exportable root bone.</returns>
    public static SkeletonBone GetSingleRootBone(Skeleton skeleton)
    {
        List<SkeletonBone> rootBones = [.. skeleton.EnumerateChildren().OfType<SkeletonBone>()];
        if (rootBones.Count == 0)
        {
            throw new InvalidOperationException($"Skeleton '{skeleton.Name}' does not contain a root bone.");
        }

        if (rootBones.Count > 1)
        {
            throw new InvalidOperationException($"Skeleton '{skeleton.Name}' contains multiple root bones, but BVH supports only one root hierarchy.");
        }

        ValidateNodeName(rootBones[0].Name, "root joint");
        return rootBones[0];
    }

    /// <summary>
    /// Builds a lookup from joint name to skeleton animation track.
    /// </summary>
    /// <param name="animation">The optional animation clip.</param>
    /// <param name="bones">The bones that will be written.</param>
    /// <returns>A dictionary keyed by joint name.</returns>
    public static Dictionary<string, SkeletonAnimationTrack> BuildTrackMap(SkeletonAnimation? animation, IReadOnlyList<SkeletonBone> bones)
    {
        Dictionary<string, SkeletonAnimationTrack> tracksByName = new(StringComparer.OrdinalIgnoreCase);
        if (animation is null)
        {
            return tracksByName;
        }

        HashSet<string> boneNames = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bones.Count; i++)
        {
            boneNames.Add(bones[i].Name);
        }

        for (int i = 0; i < animation.Tracks.Count; i++)
        {
            SkeletonAnimationTrack track = animation.Tracks[i];
            if (string.IsNullOrWhiteSpace(track.Name))
            {
                throw new InvalidOperationException("BVH export encountered a skeleton animation track without a valid target name.");
            }

            if (!boneNames.Contains(track.Name))
            {
                throw new InvalidOperationException($"BVH export cannot write animation track '{track.Name}' because no matching joint exists in the skeleton hierarchy.");
            }

            if (!tracksByName.TryAdd(track.Name, track))
            {
                throw new InvalidOperationException($"BVH export encountered multiple animation tracks targeting the joint '{track.Name}'.");
            }
        }

        return tracksByName;
    }

    /// <summary>
    /// Computes the number of BVH motion frames to write.
    /// </summary>
    /// <param name="animation">The optional animation clip.</param>
    /// <returns>The number of frames that should be written.</returns>
    public static int GetFrameCount(SkeletonAnimation? animation)
    {
        if (animation is null)
        {
            return 1;
        }

        (float minFrame, float maxFrame) = animation.GetAnimationFrameRange();
        if (maxFrame < minFrame || maxFrame == float.MinValue)
        {
            return 1;
        }

        if (minFrame < 0.0f)
        {
            throw new InvalidOperationException("BVH export does not support animation tracks with negative keyframe times.");
        }

        return (int)MathF.Ceiling(maxFrame) + 1;
    }

    /// <summary>
    /// Computes the BVH frame time to write.
    /// </summary>
    /// <param name="animation">The optional animation clip.</param>
    /// <returns>The frame time in seconds.</returns>
    public static float GetFrameTime(SkeletonAnimation? animation)
    {
        if (animation is null)
        {
            return BvhFormat.DefaultFrameTime;
        }

        if (!float.IsFinite(animation.Framerate) || animation.Framerate <= 0.0f)
        {
            throw new InvalidOperationException("BVH export requires SkeletonAnimation.Framerate to be a finite positive value.");
        }

        return 1.0f / animation.Framerate;
    }

    /// <summary>
    /// Builds the channel sequence that will be written for each bone.
    /// </summary>
    /// <param name="bones">The bones to be written.</param>
    /// <param name="rootBone">The root bone.</param>
    /// <param name="tracksByName">The animation-track lookup keyed by joint name.</param>
    /// <returns>A dictionary mapping each bone to its BVH channel sequence.</returns>
    public static Dictionary<SkeletonBone, BvhChannelType[]> BuildChannelMap(IReadOnlyList<SkeletonBone> bones, SkeletonBone rootBone, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName)
    {
        Dictionary<SkeletonBone, BvhChannelType[]> channelsByBone = new(bones.Count);

        for (int i = 0; i < bones.Count; i++)
        {
            SkeletonBone bone = bones[i];
            SkeletonAnimationTrack? track = tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? mappedTrack) ? mappedTrack : null;
            BvhChannelType[] channels = ResolveChannelSequence(bone == rootBone, track);
            channelsByBone.Add(bone, channels);
        }

        return channelsByBone;
    }

    /// <summary>
    /// Resolves the default BVH channel sequence for one joint.
    /// </summary>
    /// <param name="isRoot">Whether the joint is the root joint.</param>
    /// <param name="track">The optional animation track for the joint.</param>
    /// <returns>The channel sequence to write.</returns>
    public static BvhChannelType[] ResolveChannelSequence(bool isRoot, SkeletonAnimationTrack? track)
    {
        bool includePositionChannels = isRoot || track?.TranslationCurve is { KeyFrameCount: > 0 };
        return BvhFormat.CreateDefaultChannelSequence(includePositionChannels);
    }

    /// <summary>
    /// Validates bind-pose and animation semantics before export.
    /// </summary>
    /// <param name="bones">The bones to validate.</param>
    /// <param name="tracksByName">The animation-track lookup keyed by joint name.</param>
    public static void ValidateTracksAndBindTransforms(IReadOnlyList<SkeletonBone> bones, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName)
    {
        for (int i = 0; i < bones.Count; i++)
        {
            SkeletonBone bone = bones[i];
            ValidateNodeName(bone.Name, "joint");

            if (bone.BindTransform.Scale is Vector3 bindScale && !ApproximatelyEquals(bindScale, Vector3.One))
            {
                throw new InvalidOperationException($"BVH export cannot represent non-unit bind scale on joint '{bone.Name}'.");
            }

            if (!tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track))
            {
                continue;
            }

            if (track.ScaleCurve is { KeyFrameCount: > 0 })
            {
                throw new InvalidOperationException($"BVH export cannot represent animated scale on joint '{bone.Name}'.");
            }

            if (track.CustomCurves is { Count: > 0 })
            {
                throw new InvalidOperationException($"BVH export cannot represent custom animation curves on joint '{bone.Name}'.");
            }

            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
            {
                ValidateCurveSemantics(translationCurve, bone.Name, "translation", 3);
            }

            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
            {
                ValidateCurveSemantics(rotationCurve, bone.Name, "rotation", 4);
            }
        }
    }

    /// <summary>
    /// Validates the semantics of one animation curve before export.
    /// </summary>
    /// <param name="curve">The curve to validate.</param>
    /// <param name="boneName">The target joint name.</param>
    /// <param name="curveType">The human-readable curve type.</param>
    /// <param name="expectedComponentCount">The expected component count for the curve.</param>
    public static void ValidateCurveSemantics(AnimationCurve curve, string boneName, string curveType, int expectedComponentCount)
    {
        if (curve.TransformSpace == TransformSpace.World)
        {
            throw new InvalidOperationException($"BVH export cannot represent world-space {curveType} curves on joint '{boneName}'.");
        }

        if (curve.TransformType is TransformType.Parent or TransformType.Relative or TransformType.Additive)
        {
            throw new InvalidOperationException($"BVH export cannot represent {curve.TransformType} {curveType} curves on joint '{boneName}'.");
        }

        if (curve.ComponentCount != expectedComponentCount)
        {
            throw new InvalidOperationException($"BVH export expected {curveType} curves on joint '{boneName}' to have {expectedComponentCount} components, but found {curve.ComponentCount}.");
        }
    }

    /// <summary>
    /// Writes the BVH hierarchy block for the supplied bone tree.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="bone">The current bone to write.</param>
    /// <param name="channelsByBone">The channel sequence for each bone.</param>
    /// <param name="depth">The current hierarchy depth.</param>
    /// <param name="isRoot">Whether the current bone is the root joint.</param>
    public static void WriteHierarchy(StreamWriter writer, SkeletonBone bone, IReadOnlyDictionary<SkeletonBone, BvhChannelType[]> channelsByBone, int depth, bool isRoot)
    {
        string indent = new(' ', depth * 2);
        if (isRoot)
        {
            writer.WriteLine("HIERARCHY");
        }

        writer.Write(indent);
        writer.Write(isRoot ? "ROOT " : "JOINT ");
        writer.WriteLine(bone.Name);
        writer.Write(indent);
        writer.WriteLine("{");

        Vector3 offset = bone.BindTransform.LocalPosition ?? Vector3.Zero;
        writer.Write(indent);
        writer.Write("  OFFSET ");
        writer.Write(FormatNumber(offset.X));
        writer.Write(' ');
        writer.Write(FormatNumber(offset.Y));
        writer.Write(' ');
        writer.WriteLine(FormatNumber(offset.Z));

        BvhChannelType[] channels = channelsByBone[bone];
        writer.Write(indent);
        writer.Write("  CHANNELS ");
        writer.Write(channels.Length.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < channels.Length; i++)
        {
            writer.Write(' ');
            writer.Write(BvhFormat.GetChannelName(channels[i]));
        }

        writer.WriteLine();

        foreach (SkeletonBone childBone in bone.EnumerateChildren().OfType<SkeletonBone>())
        {
            WriteHierarchy(writer, childBone, channelsByBone, depth + 1, false);
        }

        writer.Write(indent);
        writer.WriteLine("}");
    }

    /// <summary>
    /// Writes the BVH motion section for the supplied animation data.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="bones">The bones in motion-channel order.</param>
    /// <param name="animation">The optional animation clip.</param>
    /// <param name="tracksByName">The animation-track lookup keyed by joint name.</param>
    /// <param name="channelsByBone">The channel sequence for each bone.</param>
    /// <param name="frameCount">The number of frames to write.</param>
    /// <param name="frameTime">The frame time in seconds.</param>
    public static void WriteMotion(StreamWriter writer, IReadOnlyList<SkeletonBone> bones, SkeletonAnimation? animation, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, IReadOnlyDictionary<SkeletonBone, BvhChannelType[]> channelsByBone, int frameCount, float frameTime)
    {
        writer.WriteLine("MOTION");
        writer.Write("Frames: ");
        writer.WriteLine(frameCount.ToString(CultureInfo.InvariantCulture));
        writer.Write("Frame Time: ");
        writer.WriteLine(frameTime.ToString("0.######", CultureInfo.InvariantCulture));

        Vector3[] previousEulerDegrees = new Vector3[bones.Count];
        StringBuilder lineBuilder = new(512);

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            float sampleTime = frameIndex;
            lineBuilder.Clear();

            for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
            {
                SkeletonBone bone = bones[boneIndex];
                BvhChannelType[] channels = channelsByBone[bone];
                SkeletonAnimationTrack? track = tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? mappedTrack) ? mappedTrack : null;
                Vector3 offset = bone.BindTransform.LocalPosition ?? Vector3.Zero;
                Vector3 localPosition = track?.TranslationCurve is { KeyFrameCount: > 0 } translationCurve ? translationCurve.SampleVector3(sampleTime) : offset;
                Quaternion localRotation = track?.RotationCurve is { KeyFrameCount: > 0 } rotationCurve ? Quaternion.Normalize(rotationCurve.SampleQuaternion(sampleTime)) : Quaternion.Normalize(bone.BindTransform.LocalRotation ?? Quaternion.Identity);
                Vector3 rotationDegrees = BvhRotation.ToEulerDegrees(localRotation, channels, previousEulerDegrees[boneIndex]);
                Quaternion recomposedRotation = BvhRotation.ComposeDegrees(rotationDegrees, channels);
                float alignment = MathF.Abs(Quaternion.Dot(recomposedRotation, localRotation));
                if (alignment < RotationAlignmentTolerance)
                {
                    throw new InvalidOperationException($"BVH export cannot represent the rotation for joint '{bone.Name}' at frame {frameIndex.ToString(CultureInfo.InvariantCulture)} with the default BVH channel order.");
                }

                previousEulerDegrees[boneIndex] = rotationDegrees;

                for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
                {
                    if (lineBuilder.Length > 0)
                    {
                        lineBuilder.Append(' ');
                    }

                    BvhChannelType channel = channels[channelIndex];
                    switch (channel)
                    {
                        case BvhChannelType.Xposition:
                            lineBuilder.Append(FormatNumber(localPosition.X - offset.X));
                            break;

                        case BvhChannelType.Yposition:
                            lineBuilder.Append(FormatNumber(localPosition.Y - offset.Y));
                            break;

                        case BvhChannelType.Zposition:
                            lineBuilder.Append(FormatNumber(localPosition.Z - offset.Z));
                            break;

                        case BvhChannelType.Xrotation:
                            lineBuilder.Append(FormatNumber(rotationDegrees.X));
                            break;

                        case BvhChannelType.Yrotation:
                            lineBuilder.Append(FormatNumber(rotationDegrees.Y));
                            break;

                        case BvhChannelType.Zrotation:
                            lineBuilder.Append(FormatNumber(rotationDegrees.Z));
                            break;
                    }
                }
            }

            writer.WriteLine(lineBuilder.ToString());
        }
    }

    /// <summary>
    /// Formats one floating-point value for BVH output.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted numeric string.</returns>
    public static string FormatNumber(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Determines whether two vectors are equal within <see cref="VectorTolerance"/>.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns><see langword="true"/> when the vectors are approximately equal; otherwise, <see langword="false"/>.</returns>
    public static bool ApproximatelyEquals(Vector3 left, Vector3 right)
    {
        return MathF.Abs(left.X - right.X) <= VectorTolerance && MathF.Abs(left.Y - right.Y) <= VectorTolerance && MathF.Abs(left.Z - right.Z) <= VectorTolerance;
    }

    /// <summary>
    /// Validates that a BVH node name is non-empty and token-safe.
    /// </summary>
    /// <param name="nodeName">The node name to validate.</param>
    /// <param name="description">A human-readable description used in exception messages.</param>
    public static void ValidateNodeName(string nodeName, string description)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            throw new InvalidOperationException($"The BVH {description} is missing a valid name.");
        }

        for (int i = 0; i < nodeName.Length; i++)
        {
            char character = nodeName[i];
            if (char.IsWhiteSpace(character) || character is '{' or '}')
            {
                throw new InvalidOperationException($"The BVH {description} name '{nodeName}' contains unsupported whitespace or brace characters.");
            }
        }
    }
}
