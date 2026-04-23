using System.Runtime.CompilerServices;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.SEAnim;

/// <summary>
/// Provides functionality to read and write scenes in the SEAnim file format.
/// </summary>
public class SeanimTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "SEAnim";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".seanim"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "SEAnim"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = reader.ReadBytes(MagicValue.Length);

        if (!magic.AsSpan().SequenceEqual(MagicValue))
            throw new InvalidDataException("Invalid SEAnim file: incorrect magic bytes.");

        var version   = reader.ReadInt16();
        var headerSize = reader.ReadInt16();

        var transformType = reader.ReadByte() switch
        {
            0 => TransformType.Absolute,
            1 => TransformType.Additive,
            2 => TransformType.Relative,
            _ => TransformType.Unknown,
        };

        var flags        = reader.ReadByte();
        var dataFlags    = reader.ReadByte();
        var dataPropFlags = reader.ReadByte();
        reader.ReadUInt16();                    // reserved
        var frameRate    = reader.ReadSingle();
        var frameCount   = reader.ReadInt32();
        var boneCount    = reader.ReadInt32();
        var modCount     = reader.ReadByte();
        reader.ReadBytes(3);                    // reserved
        var noteCount    = reader.ReadInt32();

        bool hasTranslations = (dataFlags & 1) != 0;
        bool hasRotations    = (dataFlags & 2) != 0;
        bool hasScales       = (dataFlags & 4) != 0;
        bool highPrecision   = (dataPropFlags & 1) != 0;

        var skelAnim = new SkeletonAnimation(context.Name, boneCount, transformType)
        {
            Framerate     = frameRate,
            TransformType = transformType,
        };

        // ---- Bone names → tracks ----
        for (int i = 0; i < boneCount; i++)
        {
            skelAnim.Tracks.Add(new SkeletonAnimationTrack(ReadUTF8String(reader))
            {
                TransformType = transformType,
            });
        }

        // ---- Per-bone transform-type overrides (modifiers) ----
        for (int i = 0; i < modCount; i++)
        {
            int boneIndex = boneCount <= 0xFF ? reader.ReadByte() : reader.ReadUInt16();

            skelAnim.Tracks[boneIndex].TransformType = reader.ReadByte() switch
            {
                0 => TransformType.Absolute,
                1 => TransformType.Additive,
                2 => TransformType.Relative,
                _ => TransformType.Unknown,
            };
        }

        foreach (var track in skelAnim.Tracks)
        {
            reader.ReadByte();

            if (hasTranslations)
            {
                int keyCount = ReadKeyCount(reader, frameCount);
                if (keyCount > 0)
                {
                    track.TranslationCurve = ReadCurve(reader, keyCount, frameCount, 3, highPrecision);
                    track.TranslationCurve.TransformType = track.TransformType;
                }
            }

            if (hasRotations)
            {
                int keyCount = ReadKeyCount(reader, frameCount);
                if (keyCount > 0)
                {
                    track.RotationCurve = ReadCurve(reader, keyCount, frameCount, 4, highPrecision);
                    track.RotationCurve.TransformType = TransformType.Absolute;
                }
            }

            if (hasScales)
            {
                int keyCount = ReadKeyCount(reader, frameCount);
                if (keyCount > 0)
                {
                    track.ScaleCurve = ReadCurve(reader, keyCount, frameCount, 3, highPrecision);
                    track.ScaleCurve.TransformType = track.TransformType;
                }
            }
        }

        for (int i = 0; i < noteCount; i++)
        {
            int frame = ReadFrameIndex(reader, frameCount);
            skelAnim.CreateAction(ReadUTF8String(reader)).KeyFrames.Add(new(frame, null));
        }

        scene.RootNode.AddNode(skelAnim);
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        SceneTranslationSelection selection = context.GetSelection(scene);
        var data = selection.TryGetFirstOfType<SkeletonAnimation>();
        if (data is null)
        {
            if (selection.Filter != SceneNodeFlags.None && scene.TryGetFirstOfType<SkeletonAnimation>() is not null)
            {
                throw new InvalidDataException(
                    $"Cannot write SEAnim: no SkeletonAnimation matched the export selection '{selection.Filter}'.");
            }

            throw new InvalidDataException("Scene does not contain a SkeletonAnimation.");
        }

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        var tracks     = data.Tracks;
        int trackCount = tracks.Count;

        var (_, maxTrackFrame) = data.GetAnimationFrameRange();
        float maxActionFrame   = 0f;

        if (data.Actions is not null)
        {
            foreach (var action in data.Actions)
                foreach (var kf in action.KeyFrames)
                    maxActionFrame = MathF.Max(maxActionFrame, kf.Frame);
        }

        int frameCount  = (int)MathF.Max(
            maxTrackFrame > float.MinValue ? maxTrackFrame : 0f,
            maxActionFrame);
        int actionCount = data.GetAnimationActionCount();

        // Detect per-track transform-type overrides
        var boneModifiers = new Dictionary<int, byte>();

        for (int i = 0; i < trackCount; i++)
        {
            var tt = tracks[i].TransformType;
            if (tt != TransformType.Unknown && tt != data.TransformType)
            {
                boneModifiers[i] = TransformTypeToByte(tt);
            }
        }

        writer.Write(MagicValue);
        writer.Write((ushort)1);    // version
        writer.Write((ushort)0x1C); // header size

        writer.Write(TransformTypeToByte(data.TransformType));
        writer.Write((byte)0); // flags

        bool hasTranslations = tracks.Any(t => t.TranslationCurve is { KeyFrameCount: > 0 });
        bool hasRotations    = tracks.Any(t => t.RotationCurve    is { KeyFrameCount: > 0 });
        bool hasScales       = tracks.Any(t => t.ScaleCurve       is { KeyFrameCount: > 0 });

        byte dataFlags = 0;

        if (hasTranslations) dataFlags |= 1;
        if (hasRotations)    dataFlags |= 2;
        if (hasScales)       dataFlags |= 4;
        if (actionCount > 0) dataFlags |= 64;

        writer.Write(dataFlags);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Write(data.Framerate);
        writer.Write(frameCount);
        writer.Write(trackCount);
        writer.Write((byte)boneModifiers.Count);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Write(actionCount);

        foreach (var track in tracks)
        {
            writer.Write(Encoding.UTF8.GetBytes(track.Name.Replace('.', '_')));
            writer.Write((byte)0);
        }

        foreach (var (index, type) in boneModifiers)
        {
            if (trackCount <= 0xFF)
                writer.Write((byte)index);
            else if (trackCount <= 0xFFFF)
                writer.Write((ushort)index);
            else
                throw new NotSupportedException("SEAnim does not support more than 65535 bones.");

            writer.Write(type);
        }

        foreach (var track in tracks)
        {
            writer.Write((byte)0);

            if (hasTranslations)
                WriteCurve(writer, track.TranslationCurve, frameCount, 3);
            if (hasRotations)
                WriteCurve(writer, track.RotationCurve, frameCount, 4);
            if (hasScales)
                WriteCurve(writer, track.ScaleCurve, frameCount, 3);
        }

        // ---- Notes / actions ----
        if (data.Actions is not null)
        {
            foreach (var action in data.Actions)
            {
                foreach (var kf in action.KeyFrames)
                {
                    WriteFrameIndex(writer, (int)kf.Frame, frameCount);
                    writer.Write(Encoding.UTF8.GetBytes(action.Name));
                    writer.Write((byte)0);
                }
            }
        }
    }

    private static AnimationCurve ReadCurve(BinaryReader reader, int keyCount, int frameCount, int componentCount, bool highPrecision)
    {
        int frameIndexSize = GetFrameIndexSize(frameCount);
        int componentSize  = highPrecision ? sizeof(double) : sizeof(float);
        int valueSize      = componentCount * componentSize;
        int stride         = frameIndexSize + valueSize;

        // Single bulk read — all keyframes for this channel
        var data = reader.ReadBytes(keyCount * stride);

        var curve = new AnimationCurve
        {
            // Keys view — frame indices as byte / ushort / int, read as float
            Keys = frameIndexSize switch
            {
                1 => new DataBufferView<byte>(data, keyCount, byteOffset: 0, byteStride: stride),
                2 => new DataBufferView<ushort>(data, keyCount, byteOffset: 0, byteStride: stride),
                _ => new DataBufferView<int>(data, keyCount, byteOffset: 0, byteStride: stride),
            }
        };

        // Values view — component data as float or double
        if (highPrecision)
            curve.Values = new DataBufferView<double>(data, keyCount, byteOffset: frameIndexSize, byteStride: stride, valueCount: 1, componentCount: componentCount);
        else
            curve.Values = new DataBufferView<float>(data,  keyCount, byteOffset: frameIndexSize, byteStride: stride, valueCount: 1, componentCount: componentCount);

        return curve;
    }

    private static void WriteCurve(BinaryWriter writer, AnimationCurve? curve, int frameCount, int componentCount)
    {
        int keyCount = curve?.KeyFrameCount ?? 0;
        WriteFrameIndex(writer, keyCount, frameCount);

        if (curve is null || keyCount == 0) return;

        for (int f = 0; f < keyCount; f++)
        {
            WriteFrameIndex(writer, (int)curve.GetKeyTime(f), frameCount);

            for (int c = 0; c < componentCount; c++)
                writer.Write(curve.Values!.Get<float>(f, 0, c));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFrameIndexSize(int frameCount) =>
        frameCount <= 0xFF ? 1 : frameCount <= 0xFFFF ? 2 : 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadFrameIndex(BinaryReader reader, int frameCount) =>
        frameCount <= 0xFF   ? reader.ReadByte() :
        frameCount <= 0xFFFF ? reader.ReadUInt16() :
                               reader.ReadInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadKeyCount(BinaryReader reader, int frameCount) =>
        ReadFrameIndex(reader, frameCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFrameIndex(BinaryWriter writer, int value, int frameCount)
    {
        if (frameCount <= 0xFF)
            writer.Write((byte)value);
        else if (frameCount <= 0xFFFF)
            writer.Write((ushort)value);
        else
            writer.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TransformTypeToByte(TransformType type) => type switch
    {
        TransformType.Absolute => 0,
        TransformType.Additive => 1,
        TransformType.Relative => 2,
        _                      => 2,
    };

    private static string ReadUTF8String(BinaryReader reader)
    {
        var sb = new StringBuilder(32);

        while (true)
        {
            var b = reader.ReadByte();
            if (b == 0) break;
            sb.Append((char)b);
        }

        return sb.ToString();
    }
}
