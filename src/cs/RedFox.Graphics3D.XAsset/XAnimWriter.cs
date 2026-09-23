using System.Numerics;
using CallOfFile;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.XAsset;

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

        SortedSet<int> frames = [];
        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            AddFrames(frames, track.TranslationCurve);
            AddFrames(frames, track.RotationCurve);
            AddFrames(frames, track.ScaleCurve);
        }
        if (frames.Count == 0)
            frames.Add(0);

        bool binary = string.Equals(Path.GetExtension(context.TargetFilePath), ".xanim_bin", StringComparison.OrdinalIgnoreCase);
        using XAssetWriter output = new(stream, binary);
        TokenWriter writer = output.Writer;
        writer.WriteSection("ANIMATION", 0x7AAC);
        writer.WriteUShort("VERSION", 0x24D1, 3);
        writer.WriteUShort("NUMPARTS", 0x9279, checked((ushort)animation.Tracks.Count));
        for (int i = 0; i < animation.Tracks.Count; i++)
            writer.WriteUShortString("PART", 0x360B, checked((ushort)i), animation.Tracks[i].Name);
        writer.WriteUShort("FRAMERATE", 0x92D3, checked((ushort)Math.Clamp((int)MathF.Round(animation.Framerate), 0, ushort.MaxValue)));
        writer.WriteUInt("NUMFRAMES", 0xB917, checked((uint)frames.Count));

        foreach (int frame in frames)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            writer.WriteUInt("FRAME", 0xC723, checked((uint)frame));
            for (int partIndex = 0; partIndex < animation.Tracks.Count; partIndex++)
            {
                SkeletonAnimationTrack track = animation.Tracks[partIndex];
                Vector3 translation = track.TranslationCurve?.SampleVector3(frame) ?? Vector3.Zero;
                Vector3 scale = track.ScaleCurve?.SampleVector3(frame) ?? Vector3.One;
                Quaternion rotation = track.RotationCurve?.SampleQuaternion(frame) ?? Quaternion.Identity;
                Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);
                writer.WriteUShort("PART", 0x745A, checked((ushort)partIndex));
                writer.WriteVector3("OFFSET", 0x9383, translation);
                writer.WriteVector3("SCALE", 0x1C56, scale);
                writer.WriteVector316Bit("X", 0xDCFD, new Vector3(matrix.M11, matrix.M12, matrix.M13));
                writer.WriteVector316Bit("Y", 0xCCDC, new Vector3(matrix.M21, matrix.M22, matrix.M23));
                writer.WriteVector316Bit("Z", 0xFCBF, new Vector3(matrix.M31, matrix.M32, matrix.M33));
            }
        }

        writer.WriteSection("NOTETRACKS", 0xC7F3);
        int actionKeyCount = animation.GetAnimationActionCount();
        for (int partIndex = 0; partIndex < animation.Tracks.Count; partIndex++)
        {
            writer.WriteUShort("PART", 0x745A, checked((ushort)partIndex));
            writer.WriteUShort("NUMTRACKS", 0x9016, checked((ushort)(partIndex == 0 && actionKeyCount > 0 ? 1 : 0)));
            if (partIndex != 0 || actionKeyCount == 0)
                continue;
            writer.WriteUShort("NOTETRACK", 0x4643, 0);
            writer.WriteUShort("NUMKEYS", 0x7A6C, checked((ushort)actionKeyCount));
            if (animation.Actions is null)
                continue;
            foreach (AnimationAction action in animation.Actions)
            {
                foreach (AnimationKeyFrame<float, Action<Scene>?> keyFrame in action.KeyFrames)
                    writer.WriteIntString("FRAME", 0x1675, (int)MathF.Round(keyFrame.Frame), action.Name);
            }
        }
    }

    private static void AddFrames(SortedSet<int> frames, AnimationCurve? curve)
    {
        if (curve is null)
            return;
        for (int i = 0; i < curve.KeyFrameCount; i++)
            frames.Add((int)MathF.Round(curve.GetKeyTime(i)));
    }
}
