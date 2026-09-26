using System.Numerics;
using CallOfFile;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.IwEngine;

internal static class XAnimWriter
{
    public static void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? cancellationToken)
    {
        SceneTranslationSelection selection = context.GetSelection(scene);
        SkeletonAnimation? animation = selection.TryGetFirstOfType<SkeletonAnimation>();
        if (animation is null)
            throw new InvalidDataException("The scene selection does not contain a skeletal animation to write as XAnim.");
        if (animation.Tracks.Count > ushort.MaxValue)
            throw new NotSupportedException("XAnim does not support more than 65535 parts.");

        Dictionary<string, SkeletonBone> bonesByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (SkeletonBone bone in selection.GetDescendants<SkeletonBone>())
        {
            bonesByName.TryAdd(bone.Name, bone);
        }

        Dictionary<string, SkeletonAnimationTrack> tracksByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            tracksByName[track.Name] = track;
        }

        SortedSet<int> frames = [];
        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            AddFrames(frames, track.TranslationCurve);
            AddFrames(frames, track.RotationCurve);
            AddFrames(frames, track.ScaleCurve);
        }
        if (frames.Count == 0)
        {
            frames.Add(0);
        }

        bool binary = string.Equals(Path.GetExtension(context.TargetFilePath), ".xanim_bin", StringComparison.OrdinalIgnoreCase);
        using XAssetWriter output = new(stream, binary);
        TokenWriter writer = output.Writer;
        writer.WriteSection("ANIMATION");
        writer.WriteUShort("VERSION", 3);
        writer.WriteUShort("NUMPARTS", checked((ushort)animation.Tracks.Count));
        for (int i = 0; i < animation.Tracks.Count; i++)
        {
            writer.WriteUShortString("PART", checked((ushort)i), animation.Tracks[i].Name);
        }
        writer.WriteUShort("FRAMERATE", checked((ushort)Math.Clamp((int)MathF.Round(animation.Framerate), 0, ushort.MaxValue)));
        writer.WriteUInt("NUMFRAMES", checked((uint)(frames.Max + 1)));

        foreach (int frame in frames)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            writer.WriteInt("FRAME", frame);
            Dictionary<SkeletonBone, (Vector3 Position, Quaternion Rotation)> worldPoses = [];
            for (int partIndex = 0; partIndex < animation.Tracks.Count; partIndex++)
            {
                SkeletonAnimationTrack track = animation.Tracks[partIndex];
                Vector3 translation;
                Quaternion rotation;
                if (bonesByName.TryGetValue(track.Name, out SkeletonBone? bone))
                {
                    (translation, rotation) = GetWorldPose(bone, animation, tracksByName, frame, worldPoses);
                }
                else
                {
                    if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                    {
                        translation = translationCurve.SampleVector3(frame);
                    }
                    else
                    {
                        translation = Vector3.Zero;
                    }

                    if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                    {
                        rotation = rotationCurve.SampleQuaternion(frame);
                    }
                    else
                    {
                        rotation = Quaternion.Identity;
                    }
                }

                Vector3 scale = track.ScaleCurve?.SampleVector3(frame) ?? Vector3.One;
                Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);
                writer.WriteUShort("PART", checked((ushort)partIndex));
                writer.WriteVector3("OFFSET", translation);
                writer.WriteVector3("SCALE", scale);
                writer.WriteVector316Bit("X", new Vector3(matrix.M11, matrix.M12, matrix.M13));
                writer.WriteVector316Bit("Y", new Vector3(matrix.M21, matrix.M22, matrix.M23));
                writer.WriteVector316Bit("Z", new Vector3(matrix.M31, matrix.M32, matrix.M33));
            }
        }

        writer.WriteSection("NOTETRACKS");
        int actionKeyCount = animation.GetAnimationActionCount();
        for (int partIndex = 0; partIndex < animation.Tracks.Count; partIndex++)
        {
            writer.WriteUShort("PART", checked((ushort)partIndex));
            writer.WriteUShort("NUMTRACKS", checked((ushort)(partIndex == 0 && actionKeyCount > 0 ? 1 : 0)));
            if (partIndex != 0 || actionKeyCount == 0)
                continue;
            writer.WriteUShort("NOTETRACK", 0);
            writer.WriteUShort("NUMKEYS", checked((ushort)actionKeyCount));
            if (animation.Actions is null)
                continue;
            foreach (AnimationAction action in animation.Actions)
            {
                foreach (AnimationKeyFrame<float, Action<Scene>?> keyFrame in action.KeyFrames)
                    writer.WriteIntString("FRAME", checked((uint)MathF.Round(keyFrame.Frame)), action.Name);
            }
        }
    }

    private static void AddFrames(SortedSet<int> frames, AnimationCurve? curve)
    {
        if (curve is null)
        {
            return;
        }

        for (int i = 0; i < curve.KeyFrameCount; i++)
        {
            frames.Add((int)MathF.Round(curve.GetKeyTime(i)));
        }
    }

    private static (Vector3 Position, Quaternion Rotation) GetWorldPose(SkeletonBone bone, SkeletonAnimation animation, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, float frame, Dictionary<SkeletonBone, (Vector3 Position, Quaternion Rotation)> worldPoses)
    {
        if (worldPoses.TryGetValue(bone, out (Vector3 Position, Quaternion Rotation) worldPose))
        {
            return worldPose;
        }

        Vector3 localPosition = bone.GetBindLocalPosition();
        Quaternion localRotation = bone.GetBindLocalRotation();
        Vector3? worldPosition = null;
        Quaternion? worldRotation = null;

        if (tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track))
        {
            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
            {
                Vector3 value = translationCurve.SampleVector3(frame);
                TransformType transformType = translationCurve.TransformType == TransformType.Unknown ? animation.TransformType : translationCurve.TransformType;
                if (translationCurve.TransformSpace == TransformSpace.World)
                {
                    worldPosition = ResolvePosition(value, bone.GetBindWorldPosition(), transformType, translationCurve.BlendWeight);
                }
                else
                {
                    localPosition = ResolvePosition(value, localPosition, transformType, translationCurve.BlendWeight);
                }
            }

            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
            {
                Quaternion value = rotationCurve.SampleQuaternion(frame);
                TransformType transformType = rotationCurve.TransformType == TransformType.Unknown ? animation.TransformType : rotationCurve.TransformType;
                if (rotationCurve.TransformSpace == TransformSpace.World)
                {
                    worldRotation = ResolveRotation(value, bone.GetBindWorldRotation(), transformType, rotationCurve.BlendWeight);
                }
                else
                {
                    localRotation = ResolveRotation(value, localRotation, transformType, rotationCurve.BlendWeight);
                }
            }
        }

        Vector3 parentWorldPosition;
        Quaternion parentWorldRotation;
        if (bone.Parent is SkeletonBone parentBone)
        {
            (parentWorldPosition, parentWorldRotation) = GetWorldPose(parentBone, animation, tracksByName, frame, worldPoses);
        }
        else
        {
            parentWorldPosition = bone.Parent?.GetBindWorldPosition() ?? Vector3.Zero;
            parentWorldRotation = bone.Parent?.GetBindWorldRotation() ?? Quaternion.Identity;
        }

        Vector3 resolvedWorldPosition = worldPosition ?? parentWorldPosition + Vector3.Transform(localPosition, parentWorldRotation);
        Quaternion resolvedWorldRotation = Quaternion.Normalize(worldRotation ?? parentWorldRotation * localRotation);
        worldPose = (resolvedWorldPosition, resolvedWorldRotation);
        worldPoses[bone] = worldPose;
        return worldPose;
    }

    private static Vector3 ResolvePosition(Vector3 value, Vector3 bindPosition, TransformType transformType, float blendWeight) => transformType switch
    {
        TransformType.Relative => bindPosition + value,
        TransformType.Additive => bindPosition + value * blendWeight,
        _ => value
    };

    private static Quaternion ResolveRotation(Quaternion value, Quaternion bindRotation, TransformType transformType, float blendWeight) => transformType switch
    {
        TransformType.Relative => Quaternion.Normalize(bindRotation * value),
        TransformType.Additive => Quaternion.Normalize(bindRotation * Quaternion.Slerp(Quaternion.Identity, value, blendWeight)),
        _ => Quaternion.Normalize(value)
    };
}
