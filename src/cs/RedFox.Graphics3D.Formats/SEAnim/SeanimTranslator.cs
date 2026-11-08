using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.SEAnim;

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
    public override SceneTranslatorContent Content => SceneTranslatorContent.Animation;

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "SEAnim"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        if (!reader.ReadBytes(MagicValue.Length).AsSpan().SequenceEqual(MagicValue))
        {
            throw new InvalidDataException("Invalid SEAnim file: incorrect magic bytes.");
        }

        var		version		= reader.ReadUInt16();
        var		headerSize	= reader.ReadUInt16();
        long	headerStart	= stream.Position;

        if (version != 1 || headerSize < 28 || headerSize - 2 > stream.Length - headerStart)
        {
            throw new InvalidDataException("Invalid SEAnim version or header length.");
        }

        var transformType = ToTransformType(reader.ReadByte());

        reader.BaseStream.Position++; // not used
        var dataFlags		= reader.ReadByte();
        var	dataPropFlags	= reader.ReadByte();
        reader.BaseStream.Position += 2; // not used
        var frameRate	= reader.ReadSingle();
        var	frameCount	= reader.ReadInt32();
        var	boneCount	= reader.ReadInt32();
        var	modCount	= reader.ReadByte();
        reader.BaseStream.Position += 3; // not used
        var noteCount	= reader.ReadInt32();

        stream.Position = headerStart + headerSize - 2;

        bool hasTranslations	= (dataFlags & 1) != 0;
        bool hasRotations	    = (dataFlags & 2) != 0;
        bool hasScales		    = (dataFlags & 4) != 0;
        bool highPrecision	    = (dataPropFlags & 1) != 0;

        var skelAnim = new SkeletonAnimation(context.Name, boneCount, transformType)
        {
            Framerate = frameRate,
        };

        for (int i = 0; i < boneCount; i++)
        {
            token?.ThrowIfCancellationRequested();
            skelAnim.Tracks.Add(new SkeletonAnimationTrack(reader.ReadUTF8NullTerminatedString()) { TransformType = transformType });
        }

        for (int i = 0; i < modCount; i++)
        {
            int boneIndex = ReadFrameIndex(reader, boneCount);

            if ((uint)boneIndex >= (uint)boneCount)
            {
                throw new InvalidDataException("Invalid SEAnim modifier index.");
            }

            skelAnim.Tracks[boneIndex].TransformType = ToTransformType(reader.ReadByte());
        }

        foreach (var track in skelAnim.Tracks)
        {
            token?.ThrowIfCancellationRequested();
            reader.ReadByte();

            var rotationType = track.TransformType == TransformType.Relative ? TransformType.Absolute : track.TransformType;

            if (hasTranslations)
            {
                track.TranslationCurve = highPrecision ? ReadCurve<double>(reader, frameCount, 3, track.TransformType) : ReadCurve<float>(reader, frameCount, 3, track.TransformType);
            }

            if (hasRotations)
            {
                track.RotationCurve = highPrecision ? ReadCurve<double>(reader, frameCount, 4, rotationType) : ReadCurve<float>(reader, frameCount, 4, rotationType);
            }

            if (hasScales)
            {
                track.ScaleCurve = highPrecision ? ReadCurve<double>(reader, frameCount, 3, track.TransformType) : ReadCurve<float>(reader, frameCount, 3, track.TransformType);
            }
        }

        for (int i = 0; i < noteCount; i++)
        {
            token?.ThrowIfCancellationRequested();
            int frame = ReadFrameIndex(reader, frameCount);
            skelAnim.CreateAction(reader.ReadUTF8NullTerminatedString()).KeyFrames.Add(new(frame, null));
        }

        scene.RootNode.AddNode(skelAnim);
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        SceneTranslationSelection selection	= context.GetSelection(scene);
        var data							= selection.TryGetFirstOfType<SkeletonAnimation>();

        if (data is null)
        {
            if (selection.Filter != SceneNodeFlags.None && scene.TryGetFirstOfType<SkeletonAnimation>() is not null)
            {
                throw new InvalidDataException($"Cannot write SEAnim: no SkeletonAnimation matched the export selection '{selection.Filter}'.");
            }

            throw new InvalidDataException("Scene does not contain a SkeletonAnimation.");
        }

        var baseType			= data.TransformType == TransformType.Unknown ? TransformType.Relative : data.TransformType;
        var boneModifiers		= new List<(int Index, TransformType Type)>();
        bool hasTranslations	= false;
        bool hasRotations		= false;
        bool hasScales			= false;
        bool highPrecision		= false;
        int maximumFrame		= 0;

        for (int i = 0; i < data.Tracks.Count; i++)
        {
            var track	= data.Tracks[i];
            var type	= track.TransformType == TransformType.Unknown ? baseType : track.TransformType;

            if (type != baseType)
            {
                boneModifiers.Add((i, type));
            }

            foreach (var curve in new[] { track.TranslationCurve, track.RotationCurve, track.ScaleCurve })
            {
                if (curve is { KeyFrameCount: > 0 })
                {
                    maximumFrame	= Math.Max(maximumFrame, ToFrame(curve.EndTime));
                    highPrecision	|= curve.Values is DataBuffer<double>;
                }
            }

            hasTranslations	|= track.TranslationCurve is { KeyFrameCount: > 0 };
            hasRotations	|= track.RotationCurve is { KeyFrameCount: > 0 };
            hasScales		|= track.ScaleCurve is { KeyFrameCount: > 0 };
        }

        if (boneModifiers.Count > byte.MaxValue)
        {
            throw new InvalidDataException("SEAnim supports at most 255 bone modifiers.");
        }

        int noteCount = 0;

        foreach (var action in data.Actions ?? [])
        {
            foreach (var keyframe in action.KeyFrames)
            {
                maximumFrame = Math.Max(maximumFrame, ToFrame(keyframe.Frame));
                noteCount++;
            }
        }

        int frameCount	= maximumFrame + 1;
        int trackCount	= data.Tracks.Count;

        byte dataFlags = 0;

        if (hasTranslations)
        {
            dataFlags |= 1;
        }

        if (hasRotations)
        {
            dataFlags |= 2;
        }

        if (hasScales)
        {
            dataFlags |= 4;
        }

        if (noteCount > 0)
        {
            dataFlags |= 64;
        }

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(MagicValue);
        writer.Write((ushort)1);
        writer.Write((ushort)0x1C);
        writer.Write(FromTransformType(baseType));
        writer.Write((byte)0);
        writer.Write(dataFlags);
        writer.Write((byte)(highPrecision ? 1 : 0));
        writer.Write((ushort)0);
        writer.Write(data.Framerate);
        writer.Write(frameCount);
        writer.Write(trackCount);
        writer.Write((byte)boneModifiers.Count);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(noteCount);

        foreach (var track in data.Tracks)
        {
            writer.WriteNullTerminatedString(track.Name.Replace('.', '_'));
        }

        foreach (var (index, type) in boneModifiers)
        {
            WriteFrameIndex(writer, index, trackCount);
            writer.Write(FromTransformType(type));
        }

        foreach (var track in data.Tracks)
        {
            token?.ThrowIfCancellationRequested();
            writer.Write((byte)0);

            if (hasTranslations)
            {
                WriteCurve(writer, track.TranslationCurve, frameCount, 3, highPrecision);
            }

            if (hasRotations)
            {
                WriteCurve(writer, track.RotationCurve, frameCount, 4, highPrecision);
            }

            if (hasScales)
            {
                WriteCurve(writer, track.ScaleCurve, frameCount, 3, highPrecision);
            }
        }

        foreach (var action in data.Actions ?? [])
        {
            foreach (var keyframe in action.KeyFrames)
            {
                WriteFrameIndex(writer, ToFrame(keyframe.Frame), frameCount);
                writer.WriteNullTerminatedString(action.Name);
            }
        }
    }

    private static AnimationCurve? ReadCurve<T>(BinaryReader reader, int frameCount, int componentCount, TransformType type) where T : unmanaged, System.Numerics.INumber<T>
    {
        int keyCount = ReadFrameIndex(reader, frameCount);

        if (keyCount == 0)
        {
            return null;
        }

        if (keyCount < 0 || keyCount > frameCount)
        {
            throw new InvalidDataException("Invalid SEAnim key count.");
        }

        var keys	= new float[keyCount];
        var values	= new T[keyCount * componentCount];

        for (int i = 0; i < keyCount; i++)
        {
            keys[i] = ReadFrameIndex(reader, frameCount);

            for (int c = 0; c < componentCount; c++)
            {
                values[i * componentCount + c] = reader.ReadStruct<T>();
            }
        }

        return new AnimationCurve(TransformSpace.Local, type)
        {
            Keys	= new DataBuffer<float>(keys, 1, 1),
            Values	= new DataBuffer<T>(values, 1, componentCount),
        };
    }

    private static void WriteCurve(BinaryWriter writer, AnimationCurve? curve, int frameCount, int componentCount, bool highPrecision)
    {
        int keyCount = curve?.KeyFrameCount ?? 0;

        WriteFrameIndex(writer, keyCount, frameCount);

        if (curve is null || keyCount == 0)
        {
            return;
        }

        if (curve.ComponentCount != componentCount)
        {
            throw new InvalidDataException("Invalid SEAnim curve dimensions.");
        }

        for (int i = 0; i < keyCount; i++)
        {
            WriteFrameIndex(writer, ToFrame(curve.GetKeyTime(i)), frameCount);

            for (int c = 0; c < componentCount; c++)
            {
                if (highPrecision)
                {
                    writer.Write(curve.Values!.Get<double>(i, 0, c));
                }
                else
                {
                    writer.Write(curve.Values!.Get<float>(i, 0, c));
                }
            }
        }
    }

    private static int ReadFrameIndex(BinaryReader reader, int frameCount)
    {
        if (frameCount <= byte.MaxValue)
        {
            return reader.ReadByte();
        }

        if (frameCount <= ushort.MaxValue)
        {
            return reader.ReadUInt16();
        }

        return reader.ReadInt32();
    }

    private static void WriteFrameIndex(BinaryWriter writer, int value, int frameCount)
    {
        if (frameCount <= byte.MaxValue)
        {
            writer.Write(checked((byte)value));
        }
        else if (frameCount <= ushort.MaxValue)
        {
            writer.Write(checked((ushort)value));
        }
        else
        {
            writer.Write(value);
        }
    }

    private static int ToFrame(float time) => Math.Max(0, (int)MathF.Round(time));

    private static TransformType ToTransformType(byte value) => value switch
    {
        0 => TransformType.Absolute,
        1 => TransformType.Additive,
        2 => TransformType.Relative,
        _ => throw new InvalidDataException("Unsupported SEAnim transform type."),
    };

    private static byte FromTransformType(TransformType type) => type switch
    {
        TransformType.Absolute	=> 0,
        TransformType.Additive	=> 1,
        _						=> 2,
    };
}
