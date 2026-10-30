using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

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

        var magic = reader.ReadBytes(MagicValue.Length);

        if (!magic.AsSpan().SequenceEqual(MagicValue))
            throw new InvalidDataException("Invalid SEAnim file: incorrect magic bytes.");

        var		version		= reader.ReadUInt16();
        var		headerSize	= reader.ReadUInt16();
        long	headerStart	= stream.Position;
        if (version != 1 || headerSize < 28 || headerSize - 2 > stream.Length - headerStart)
            throw new InvalidDataException("Invalid SEAnim version or header length.");

        var transformType = reader.ReadByte() switch
        {
            0 => TransformType.Absolute,
            1 => TransformType.Additive,
            2 => TransformType.Relative,
            _ => TransformType.Unknown,
        };

        reader.ReadByte();
        var	dataFlags		= reader.ReadByte();
        var	dataPropFlags	= reader.ReadByte();
        reader.ReadUInt16();
        var	frameRate	= reader.ReadSingle();
        var	frameCount	= reader.ReadInt32();
        var	boneCount	= reader.ReadInt32();
        var	modCount	= reader.ReadByte();
        reader.ReadBytes(3);
        var noteCount		= reader.ReadInt32();

        if (frameCount <= 0 || boneCount < 0 || boneCount > stream.Length - stream.Position || noteCount < 0 || noteCount > stream.Length - stream.Position || !float.IsFinite(frameRate) || frameRate <= 0 || transformType == TransformType.Unknown || (dataFlags & 7) == 0 && boneCount != 0 || (dataFlags & 64) == 0 && noteCount != 0 || (dataFlags & 56) != 0 || (dataPropFlags & ~1) != 0)
            throw new InvalidDataException("Invalid SEAnim header fields.");
        stream.Position = headerStart + headerSize - 2;

        bool	hasTranslations	= (dataFlags & 1) != 0;
        bool	hasRotations	= (dataFlags & 2) != 0;
        bool	hasScales		= (dataFlags & 4) != 0;
        bool	highPrecision	= (dataPropFlags & 1) != 0;

        var skelAnim = new SkeletonAnimation(context.Name, boneCount, transformType)
        {
            Framerate		= frameRate,
            TransformType	= transformType,
        };

        for (int i = 0; i < boneCount; i++)
        {
            token?.ThrowIfCancellationRequested();
            skelAnim.Tracks.Add(new SkeletonAnimationTrack(ReadUTF8String(reader))
            {
                TransformType = transformType,
            });
        }

        HashSet<int> modifiedBones = [];
        for (int i = 0; i < modCount; i++)
        {
            token?.ThrowIfCancellationRequested();
            int boneIndex = ReadFrameIndex(reader, boneCount);
            if ((uint)boneIndex >= (uint)boneCount || !modifiedBones.Add(boneIndex))
                throw new InvalidDataException("Invalid SEAnim modifier index.");

            skelAnim.Tracks[boneIndex].TransformType = reader.ReadByte() switch
            {
                0 => TransformType.Absolute,
                1 => TransformType.Additive,
                2 => TransformType.Relative,
                _ => throw new NotSupportedException("Unsupported SEAnim bone modifier type."),
            };
        }

        foreach (var track in skelAnim.Tracks)
        {
            token?.ThrowIfCancellationRequested();
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
                    track.RotationCurve					= ReadCurve(reader, keyCount, frameCount, 4, highPrecision);
                    track.RotationCurve.TransformType	= track.TransformType;
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
            token?.ThrowIfCancellationRequested();
            int frame = ReadFrameIndex(reader, frameCount);
            skelAnim.CreateAction(ReadUTF8String(reader)).KeyFrames.Add(new(frame, null));
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

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        List<SkeletonAnimationTrack> tracks = new(data.Tracks.Count);
        Dictionary<int, byte> boneModifiers = [];
        TransformType baseType	= data.TransformType == TransformType.Unknown ? TransformType.Relative : data.TransformType;
        bool hasTranslations	= false;
        bool hasRotations		= false;
        bool hasScales			= false;
        bool highPrecision		= false;
        float maximumFrame		= 0;
        foreach (SkeletonAnimationTrack track in data.Tracks)
        {
            token?.ThrowIfCancellationRequested();
            bool translation	= track.TranslationCurve is { KeyFrameCount: > 0 };
            bool rotation		= track.RotationCurve is { KeyFrameCount: > 0 };
            bool scale			= track.ScaleCurve is { KeyFrameCount: > 0 };
            if (!translation && !rotation && !scale)
                continue;

            TransformType inherited			= track.TransformType == TransformType.Unknown ? baseType : track.TransformType;
            TransformType translationType	= GetCurveTransformType(track.TranslationCurve, inherited);
            TransformType rotationType		= GetCurveTransformType(track.RotationCurve, inherited);
            TransformType scaleType			= GetCurveTransformType(track.ScaleCurve, inherited);
            TransformType mode				= inherited;
            if (translationType != TransformType.Unknown)
                mode = translationType;
            else if (rotationType != TransformType.Unknown)
                mode = rotationType;
            else if (scaleType != TransformType.Unknown)
                mode = scaleType;
            if (translationType != TransformType.Unknown && translationType != mode || rotationType != TransformType.Unknown && rotationType != mode || scaleType != TransformType.Unknown && scaleType != mode)
                throw new InvalidDataException("SEAnim cannot store different transform modes for curves on the same bone.");
            if (mode != baseType)
                boneModifiers.Add(tracks.Count, TransformTypeToByte(mode));
            tracks.Add(track);

            hasTranslations |= translation;
            hasRotations |= rotation;
            hasScales |= scale;
            highPrecision |= track.TranslationCurve?.Values is DataBuffer<double> || track.RotationCurve?.Values is DataBuffer<double> || track.ScaleCurve?.Values is DataBuffer<double>;
            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                maximumFrame = MathF.Max(maximumFrame, translationCurve.EndTime);
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                maximumFrame = MathF.Max(maximumFrame, rotationCurve.EndTime);
            if (track.ScaleCurve is { KeyFrameCount: > 0 } scaleCurve)
                maximumFrame = MathF.Max(maximumFrame, scaleCurve.EndTime);
        }
        int trackCount	= tracks.Count;
        int actionCount	= 0;
        foreach (AnimationAction action in data.Actions ?? [])
        {
            foreach (AnimationKeyFrame<float, object?> keyframe in action.KeyFrames)
            {
                int frame = checked((int)keyframe.Frame);
                if (frame < 0 || frame != keyframe.Frame)
                    throw new InvalidDataException("SEAnim notes require nonnegative integral frame indices.");
                maximumFrame	= MathF.Max(maximumFrame, keyframe.Frame);
                actionCount		= checked(actionCount + 1);
            }
        }
        if (!float.IsFinite(maximumFrame) || maximumFrame < 0 || maximumFrame >= int.MaxValue || maximumFrame != MathF.Truncate(maximumFrame))
            throw new InvalidDataException("SEAnim requires nonnegative integral frame indices.");
        int frameCount = checked((int)maximumFrame + 1);
        if (boneModifiers.Count > byte.MaxValue)
            throw new InvalidDataException("SEAnim supports at most 255 bone modifiers.");

        writer.Write(MagicValue);
        writer.Write((ushort)1);
        writer.Write((ushort)0x1C);

        writer.Write(TransformTypeToByte(data.TransformType));
        writer.Write((byte)0);

        byte dataFlags = 0;

        if (hasTranslations) dataFlags |= 1;
        if (hasRotations)		dataFlags |= 2;
        if (hasScales)			dataFlags |= 4;
        if (actionCount > 0) dataFlags |= 64;

        writer.Write(dataFlags);
        writer.Write((byte)(highPrecision ? 1 : 0));
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
            token?.ThrowIfCancellationRequested();
            writer.Write(Encoding.UTF8.GetBytes(track.Name.Replace('.', '_')));
            writer.Write((byte)0);
        }

        foreach (var (index, type) in boneModifiers)
        {
            token?.ThrowIfCancellationRequested();
            if (trackCount <= 0xFF)
                writer.Write((byte)index);
            else if (trackCount <= 0xFFFF)
                writer.Write((ushort)index);
            else
                writer.Write(index);

            writer.Write(type);
        }

        foreach (var track in tracks)
        {
            token?.ThrowIfCancellationRequested();
            writer.Write((byte)0);

            if (hasTranslations)
                WriteCurve(writer, track.TranslationCurve, frameCount, 3, highPrecision);
            if (hasRotations)
                WriteCurve(writer, track.RotationCurve, frameCount, 4, highPrecision);
            if (hasScales)
                WriteCurve(writer, track.ScaleCurve, frameCount, 3, highPrecision);
        }

        if (data.Actions is not null)
        {
            foreach (var action in data.Actions)
            {
                foreach (var keyframe in action.KeyFrames)
                {
                    int frameIndex = checked((int)keyframe.Frame);
                    if (frameIndex != keyframe.Frame)
                        throw new InvalidDataException("SEAnim notes require integral frame indices.");
                    WriteFrameIndex(writer, frameIndex, frameCount);
                    writer.Write(Encoding.UTF8.GetBytes(action.Name));
                    writer.Write((byte)0);
                }
            }
        }
    }

    private static TransformType GetCurveTransformType(AnimationCurve? curve, TransformType inherited)
    {
        if (curve is not { KeyFrameCount: > 0 })
            return TransformType.Unknown;
        return curve.TransformType == TransformType.Unknown ? inherited : curve.TransformType;
    }

    private static AnimationCurve ReadCurve(BinaryReader reader, int keyCount, int frameCount, int componentCount, bool highPrecision)
    {
        int	frameIndexSize	= GetFrameIndexSize(frameCount);
        int	componentSize	= highPrecision ? sizeof(double) : sizeof(float);
        int	valueSize		= componentCount * componentSize;
        int	stride			= frameIndexSize + valueSize;

        if (keyCount < 0 || keyCount > frameCount || (long)keyCount * stride > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid SEAnim key count.");
        byte[] data = new byte[checked(keyCount * stride)];
        reader.BaseStream.ReadExactly(data);

        var curve = new AnimationCurve
        {

            Keys = frameIndexSize switch
            {
                1 => DataBufferPacking.CreateStrided<byte>(data, keyCount, byteOffset: 0, byteStride: stride, valueCount: 1, componentCount: 1, byteValueStride: sizeof(byte)),
                2 => DataBufferPacking.CreateStrided<ushort>(data, keyCount, byteOffset: 0, byteStride: stride, valueCount: 1, componentCount: 1, byteValueStride: sizeof(ushort)),
                _ => DataBufferPacking.CreateStrided<int>(data, keyCount, byteOffset: 0, byteStride: stride, valueCount: 1, componentCount: 1, byteValueStride: sizeof(int)),
            }
        };

        if (highPrecision)
            curve.Values = DataBufferPacking.CreateStrided<double>(data, keyCount, byteOffset: frameIndexSize, byteStride: stride, valueCount: 1, componentCount: componentCount, byteValueStride: componentCount * sizeof(double));
        else
            curve.Values = DataBufferPacking.CreateStrided<float>(data, keyCount, byteOffset: frameIndexSize, byteStride: stride, valueCount: 1, componentCount: componentCount, byteValueStride: componentCount * sizeof(float));

        return curve;
    }

    private static void WriteCurve(BinaryWriter writer, AnimationCurve? curve, int frameCount, int componentCount, bool highPrecision)
    {
        int keyCount = curve?.KeyFrameCount ?? 0;
        if (keyCount > frameCount || curve is not null && keyCount > 0 && (curve.Values?.ElementCount != keyCount || curve.ComponentCount != componentCount))
            throw new InvalidDataException("Invalid SEAnim curve dimensions.");
        WriteFrameIndex(writer, keyCount, frameCount);

        if (curve is null || keyCount == 0) return;

        DataBuffer values					= curve.Values ?? throw new InvalidDataException("Missing SEAnim curve values.");
        ReadOnlySpan<float> floatValues		= default;
        ReadOnlySpan<double> doubleValues	= default;
        if (BitConverter.IsLittleEndian)
        {
            if (highPrecision)
                values.TryGetReadOnlySpan(out doubleValues);
            else
                values.TryGetReadOnlySpan(out floatValues);
        }
        int previousFrame = -1;
        for (int f = 0; f < keyCount; f++)
        {
            float frame		= curve.GetKeyTime(f);
            int frameIndex	= checked((int)frame);
            if (frame != frameIndex || frameIndex < 0 || frameIndex >= frameCount || frameIndex <= previousFrame)
                throw new InvalidDataException("SEAnim requires increasing integral frame indices.");
            WriteFrameIndex(writer, frameIndex, frameCount);
            previousFrame = frameIndex;

            if (!floatValues.IsEmpty)
            {
                writer.Write(MemoryMarshal.AsBytes(floatValues.Slice(f * componentCount, componentCount)));
            }
            else if (!doubleValues.IsEmpty)
            {
                writer.Write(MemoryMarshal.AsBytes(doubleValues.Slice(f * componentCount, componentCount)));
            }
            else
            {
                for (int c = 0; c < componentCount; c++)
                {
                    if (highPrecision)
                        writer.Write(values.Get<double>(f, 0, c));
                    else
                        writer.Write(values.Get<float>(f, 0, c));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFrameIndexSize(int frameCount)
    {
        if (frameCount <= byte.MaxValue)
            return sizeof(byte);
        if (frameCount <= ushort.MaxValue)
            return sizeof(ushort);
        return sizeof(int);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadFrameIndex(BinaryReader reader, int frameCount)
    {
        if (frameCount <= byte.MaxValue)
            return reader.ReadByte();
        if (frameCount <= ushort.MaxValue)
            return reader.ReadUInt16();
        return reader.ReadInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadKeyCount(BinaryReader reader, int frameCount) => ReadFrameIndex(reader, frameCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFrameIndex(BinaryWriter writer, int value, int frameCount)
    {
        if (frameCount <= 0xFF)
            writer.Write(checked((byte)value));
        else if (frameCount <= 0xFFFF)
            writer.Write(checked((ushort)value));
        else
            writer.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TransformTypeToByte(TransformType type) => type switch
    {
        TransformType.Absolute => 0,
        TransformType.Additive => 1,
        TransformType.Relative => 2,
        _						=> 2,
    };

    private static string ReadUTF8String(BinaryReader reader)
    {
        List<byte> bytes = [];

        while (true)
        {
            byte value = reader.ReadByte();
            if (value == 0)
            {
                break;
            }

            bytes.Add(value);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
