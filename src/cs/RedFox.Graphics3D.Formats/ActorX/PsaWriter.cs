using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.ActorX;

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
    public void Write(SceneTranslationSelection selection) => Write(selection, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="selection">The selected scene data to write.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(SceneTranslationSelection selection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selection);

        var animation = selection.TryGetFirstOfType<SkeletonAnimation>() ?? throw new InvalidDataException("Scene does not contain a SkeletonAnimation to write as PSA.");

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

        ActorXBinary.WriteBones(writer, ActorXChunkId.BoneNames, bones, boneNodes);
        WriteSequence(writer, animation, bones.Length, frameCount);
        WriteKeys(writer, bones, boneNodes, trackByName, frameCount, token);

        writer.Flush();
    }

    private static void WriteSequence(BinaryWriter writer, SkeletonAnimation animation, int boneCount, int frameCount)
    {
        ActorXChunkHeader.Write(writer, ActorXChunkId.AnimationInfo, ActorXBinary.Version, 168, 1);

        float rate = animation.Framerate > 0f ? animation.Framerate : 30f;

        ActorXBinary.WriteFixedString(writer, animation.Name, 64);
        ActorXBinary.WriteFixedString(writer, "None", 64);
        writer.Write(boneCount);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0f);
        writer.Write((float)frameCount);
        writer.Write(rate);
        writer.Write(0);
        writer.Write(0);
        writer.Write(frameCount);
    }

    private static void WriteKeys(BinaryWriter writer, SkeletonBone[] bones, SceneNode[] boneNodes, Dictionary<string, SkeletonAnimationTrack> trackByName, int frameCount, CancellationToken token)
    {
        ActorXChunkHeader.Write(writer, ActorXChunkId.AnimationKeys, ActorXBinary.Version, 32, checked(frameCount * bones.Length));

        Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(bones, boneNodes);
        Dictionary<SkeletonBone, Matrix4x4> transforms = [];
        for (int frame = 0; frame < frameCount; frame++)
        {
            token.ThrowIfCancellationRequested();
            transforms.Clear();
            for (int b = 0; b < bones.Length; b++)
            {
                var bone = bones[b];
                SkeletonExportTransforms.GetRelativeAnimatedTransform(bone, parents, trackByName, frame, transforms, out Vector3 position, out Quaternion rotation);

                ActorXBinary.Write(writer, position);
                ActorXBinary.Write(writer, ActorXBinary.ToStoredRotation(rotation, b));
                writer.Write(1f);
            }
        }
    }
}
