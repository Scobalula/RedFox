using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.Wilt;

/// <summary>
/// Reads and writes Wilt animation files.
/// </summary>
public sealed class WiltAnimTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "WiltAnim";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".wiltanim"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "WLTA"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, true);

        scene.UpAxis = reader.ReadWiltHeader(MagicValue);

        string					name			= context.Name;
        float					framerate		= 30;
        TransformType			transformType	= TransformType.Unknown;
        TransformSpace			transformSpace	= TransformSpace.Local;
        SkeletonAnimation?		skeletal		= null;
        MorphAnimation?			morph			= null;
        List<AnimationAction>?	actions			= null;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            token?.ThrowIfCancellationRequested();

            WiltSection	section	= (WiltSection)reader.ReadUInt32();
            uint		size	= reader.ReadUInt32();
            long		end		= reader.BaseStream.Position + size;

            switch (section)
            {
                case WiltSection.Meta:
                    reader.ReadWiltMetadata(scene);
                    break;
                case WiltSection.Animation:
                    name			= reader.ReadWiltString();
                    framerate		= reader.ReadSingle();
                    transformType	= (TransformType)reader.ReadByte();
                    transformSpace	= (TransformSpace)reader.ReadByte();
                    break;
                case WiltSection.SkeletalTracks:
                    skeletal = new SkeletonAnimation(name) { Framerate = framerate, TransformType = transformType, TransformSpace = transformSpace };
                    ReadSkeletalTracks(reader, skeletal);
                    break;
                case WiltSection.MorphTracks:
                    morph = new MorphAnimation(name) { Framerate = framerate };
                    ReadMorphTracks(reader, morph);
                    break;
                case WiltSection.Actions:
                    actions = ReadActions(reader);
                    break;
            }

            reader.BaseStream.Position = end;
        }

        if (skeletal is null && morph is null)
            skeletal = new SkeletonAnimation(name) { Framerate = framerate, TransformType = transformType, TransformSpace = transformSpace };

        if (skeletal is not null)
        {
            skeletal.Actions = actions;
            scene.RootNode.AddNode(skeletal);
        }

        if (morph is not null)
        {
            if (skeletal is not null)
            {
                morph.Name = $"{name}_Morph";
            }
            else
            {
                morph.Actions = actions;
            }

            scene.RootNode.AddNode(morph);
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);

        SceneTranslationSelection	selection	= context.GetSelection(scene);
        SkeletonAnimation?			skeletal	= selection.TryGetFirstOfType<SkeletonAnimation>();
        MorphAnimation?				morph		= selection.TryGetFirstOfType<MorphAnimation>();
        Animation					animation	= (Animation?)skeletal ?? morph ?? throw new InvalidDataException("Scene does not contain a SkeletonAnimation or MorphAnimation.");
        AnimationAction[]			actions		= [.. skeletal?.Actions ?? [], .. morph?.Actions ?? []];

        writer.WriteWiltHeader(MagicValue, scene.UpAxis);
        writer.WriteWiltMetadata(scene.Attributes);

        long section = writer.BeginWiltSection(WiltSection.Animation);

        writer.WriteWiltString(animation.Name);
        writer.Write(animation.Framerate);
        writer.Write((byte)(skeletal?.TransformType ?? TransformType.Unknown));
        writer.Write((byte)(skeletal?.TransformSpace ?? TransformSpace.Local));
        writer.EndWiltSection(section);

        if (skeletal is not null)
            WriteSkeletalTracks(writer, skeletal, token);
        if (morph is not null)
            WriteMorphTracks(writer, morph);
        if (actions.Length > 0)
            WriteActions(writer, actions);
    }

    private static void WriteSkeletalTracks(BinaryWriter writer, SkeletonAnimation animation, CancellationToken? token)
    {
        long section = writer.BeginWiltSection(WiltSection.SkeletalTracks);

        writer.Write((uint)animation.Tracks.Count);

        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            token?.ThrowIfCancellationRequested();

            byte curveMask = (byte)((track.TranslationCurve is not null ? 1 : 0) | (track.RotationCurve is not null ? 2 : 0) | (track.ScaleCurve is not null ? 4 : 0));

            writer.WriteWiltString(track.Name);
            writer.Write((byte)track.TransformType);
            writer.Write((byte)track.TransformSpace);
            writer.Write(curveMask);

            if (track.TranslationCurve is not null)
                WriteCurve(writer, track.TranslationCurve);
            if (track.RotationCurve is not null)
                WriteCurve(writer, track.RotationCurve);
            if (track.ScaleCurve is not null)
                WriteCurve(writer, track.ScaleCurve);

            writer.Write(checked((ushort)(track.CustomCurves?.Count ?? 0)));

            foreach ((string curveName, AnimationCurve curve) in track.CustomCurves ?? [])
            {
                writer.WriteWiltString(curveName);
                WriteCurve(writer, curve);
            }
        }

        writer.EndWiltSection(section);
    }

    private static void ReadSkeletalTracks(BinaryReader reader, SkeletonAnimation animation)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
        {
            SkeletonAnimationTrack track = new(reader.ReadWiltString())
            {
                TransformType	= (TransformType)reader.ReadByte(),
                TransformSpace	= (TransformSpace)reader.ReadByte(),
            };

            byte curveMask = reader.ReadByte();

            if ((curveMask & 1) != 0)
                track.TranslationCurve = ReadCurve(reader);
            if ((curveMask & 2) != 0)
                track.RotationCurve = ReadCurve(reader);
            if ((curveMask & 4) != 0)
                track.ScaleCurve = ReadCurve(reader);

            ushort customCount = reader.ReadUInt16();

            if (customCount > 0)
            {
                track.CustomCurves = new(customCount);

                for (int c = 0; c < customCount; c++)
                    track.CustomCurves[reader.ReadWiltString()] = ReadCurve(reader);
            }

            animation.Tracks.Add(track);
        }
    }

    private static void WriteMorphTracks(BinaryWriter writer, MorphAnimation animation)
    {
        long						section	= writer.BeginWiltSection(WiltSection.MorphTracks);
        List<MorphAnimationTrack>	tracks	= [.. animation.Tracks.Where(track => track.WeightCurve is not null)];

        writer.Write((uint)tracks.Count);

        foreach (MorphAnimationTrack track in tracks)
        {
            writer.WriteWiltString(track.Name);
            WriteCurve(writer, track.WeightCurve!);
        }

        writer.EndWiltSection(section);
    }

    private static void ReadMorphTracks(BinaryReader reader, MorphAnimation animation)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
            animation.Tracks.Add(new MorphAnimationTrack(reader.ReadWiltString()) { WeightCurve = ReadCurve(reader) });
    }

    private static void WriteActions(BinaryWriter writer, AnimationAction[] actions)
    {
        long section = writer.BeginWiltSection(WiltSection.Actions);

        writer.Write((uint)actions.Length);

        foreach (AnimationAction action in actions)
        {
            writer.WriteWiltString(action.Name);
            writer.WriteWiltString(action.Type);
            writer.Write((uint)action.KeyFrames.Count);

            foreach (AnimationKeyFrame<float, object?> keyFrame in action.KeyFrames)
            {
                writer.Write(keyFrame.Frame);
                writer.WriteWiltValue(keyFrame.Value);
            }
        }

        writer.EndWiltSection(section);
    }

    private static List<AnimationAction> ReadActions(BinaryReader reader)
    {
        int						count	= checked((int)reader.ReadUInt32());
        List<AnimationAction>	actions	= new(count);

        for (int i = 0; i < count; i++)
        {
            AnimationAction	action		= new(reader.ReadWiltString(), reader.ReadWiltString());
            uint			keyCount	= reader.ReadUInt32();

            for (uint k = 0; k < keyCount; k++)
                action.KeyFrames.Add(new(reader.ReadSingle(), reader.ReadWiltValue()));

            actions.Add(action);
        }

        return actions;
    }

    private static void WriteCurve(BinaryWriter writer, AnimationCurve curve)
    {
        writer.Write((byte)curve.TransformType);
        writer.Write((byte)curve.TransformSpace);
        writer.Write(curve.BlendWeight);
        writer.WriteWiltBuffer(curve.Keys);
        writer.WriteWiltBuffer(curve.Values);
    }

    private static AnimationCurve ReadCurve(BinaryReader reader)
    {
        TransformType	transformType	= (TransformType)reader.ReadByte();
        TransformSpace	transformSpace	= (TransformSpace)reader.ReadByte();
        float			blendWeight		= reader.ReadSingle();

        return new AnimationCurve(transformSpace, transformType)
        {
            BlendWeight	= blendWeight,
            Keys		= reader.ReadWiltBuffer(),
            Values		= reader.ReadWiltBuffer(),
        };
    }
}
