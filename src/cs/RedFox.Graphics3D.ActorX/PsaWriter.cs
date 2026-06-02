using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Writes a single <see cref="SkeletonAnimation"/> from a <see cref="Scene"/> selection to an ActorX PSA file.
/// </summary>
public sealed class PsaWriter
{
    private readonly Stream _stream;

    /// <summary>
    /// Initializes a new <see cref="PsaWriter"/>.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    public PsaWriter(Stream stream) => _stream = stream;

    /// <summary>
    /// Writes the first skeleton animation contained in the selection as PSA.
    /// </summary>
    /// <param name="selection">The scene selection to export.</param>
    public void Write(SceneTranslationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var animation = selection.TryGetFirstOfType<SkeletonAnimation>()
            ?? throw new InvalidDataException("Scene does not contain a SkeletonAnimation to write as PSA.");

        var bones = selection.GetDescendants<SkeletonBone>();
        if (bones.Length == 0)
            bones = Array.ConvertAll([.. animation.Tracks], static track => new SkeletonBone(track.Name));

        SceneNode[] boneNodes = Array.ConvertAll(bones, static bone => (SceneNode)bone);

        var trackByName = new Dictionary<string, SkeletonAnimationTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in animation.Tracks)
            trackByName[track.Name] = track;

        var (_, maxFrame) = animation.GetAnimationFrameRange();
        int frameCount = maxFrame > float.MinValue ? (int)MathF.Ceiling(maxFrame) + 1 : 1;

        using var writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);

        ActorXChunkHeader.Write(writer, ActorXChunkId.AnimationHeader, ActorXBinary.Version, 0, 0);

        WriteBoneNames(writer, bones, boneNodes);
        WriteSequence(writer, animation, bones.Length, frameCount);
        WriteKeys(writer, bones, trackByName, frameCount);

        writer.Flush();
    }

    private static void WriteBoneNames(BinaryWriter writer, SkeletonBone[] bones, SceneNode[] boneNodes)
    {
        var parentIndices = new int[bones.Length];
        var childCounts = new int[bones.Length];

        for (int i = 0; i < bones.Length; i++)
            parentIndices[i] = SceneNode.GetBestParentIndex(bones[i], boneNodes);

        for (int i = 0; i < bones.Length; i++)
        {
            int parent = parentIndices[i];
            if (parent >= 0)
                childCounts[parent]++;
        }

        ActorXChunkHeader.Write(writer, ActorXChunkId.BoneNames, ActorXBinary.Version, 120, bones.Length);

        for (int i = 0; i < bones.Length; i++)
        {
            var bone = bones[i];
            ActorXBinary.WriteFixedString(writer, bone.Name, 64);
            writer.Write(0u);
            writer.Write(childCounts[i]);
            writer.Write(parentIndices[i] < 0 ? 0 : parentIndices[i]);

            Quaternion stored = ActorXBinary.ToStoredRotation(bone.BindTransform.LocalRotation ?? Quaternion.Identity, i);
            ActorXBinary.Write(writer, stored);
            ActorXBinary.Write(writer, bone.BindTransform.LocalPosition ?? Vector3.Zero);

            writer.Write(0f);
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(0f);
        }
    }

    private static void WriteSequence(BinaryWriter writer, SkeletonAnimation animation, int boneCount, int frameCount)
    {
        ActorXChunkHeader.Write(writer, ActorXChunkId.AnimationInfo, ActorXBinary.Version, 168, 1);

        float rate = animation.Framerate > 0f ? animation.Framerate : 30f;

        ActorXBinary.WriteFixedString(writer, animation.Name, 64);
        ActorXBinary.WriteFixedString(writer, "None", 64);
        writer.Write(boneCount);   // TotalBones
        writer.Write(0);           // RootInclude
        writer.Write(0);           // KeyCompressionStyle
        writer.Write(0);           // KeyQuotum
        writer.Write(0f);          // KeyReduction
        writer.Write(frameCount / rate); // TrackTime
        writer.Write(rate);        // AnimRate
        writer.Write(0);           // StartBone
        writer.Write(0);           // FirstRawFrame
        writer.Write(frameCount);  // NumRawFrames
    }

    private static void WriteKeys(
        BinaryWriter writer,
        SkeletonBone[] bones,
        Dictionary<string, SkeletonAnimationTrack> trackByName,
        int frameCount)
    {
        ActorXChunkHeader.Write(writer, ActorXChunkId.AnimationKeys, ActorXBinary.Version, 32, frameCount * bones.Length);

        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int b = 0; b < bones.Length; b++)
            {
                var bone = bones[b];
                Vector3 position = bone.BindTransform.LocalPosition ?? Vector3.Zero;
                Quaternion rotation = bone.BindTransform.LocalRotation ?? Quaternion.Identity;

                if (trackByName.TryGetValue(bone.Name, out var track))
                {
                    if (track.TranslationCurve is { KeyFrameCount: > 0 } translation)
                        position = translation.SampleVector3(frame);
                    if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                        rotation = rotationCurve.SampleQuaternion(frame);
                }

                ActorXBinary.Write(writer, position);
                ActorXBinary.Write(writer, ActorXBinary.ToStoredRotation(rotation, b));
                writer.Write(1f); // Time
            }
        }
    }
}
