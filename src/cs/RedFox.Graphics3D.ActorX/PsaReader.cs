using System.Numerics;
using System.Text;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Reads ActorX PSA animation files into a <see cref="Scene"/>, producing one
/// <see cref="SkeletonAnimation"/> per stored sequence.
/// </summary>
public sealed class PsaReader
{
    private readonly Stream _stream;
    private readonly string _name;

    /// <summary>
    /// Initializes a new <see cref="PsaReader"/>.
    /// </summary>
    /// <param name="stream">The source stream positioned at the start of the file.</param>
    /// <param name="name">The logical name used for created nodes.</param>
    public PsaReader(Stream stream, string name)
    {
        _stream = stream;
        _name = name;
    }

    /// <summary>
    /// Reads the PSA content and populates the supplied scene.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    public void Read(Scene scene)
    {
        using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

        var header = ActorXChunkHeader.Read(reader);
        if (!header.ChunkId.StartsWith(ActorXChunkId.AnimationHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Invalid PSA file: missing ANIMHEAD chunk.");

        List<ActorXBone> boneRecords = [];
        List<ActorXAnimInfo> sequences = [];
        List<ActorXAnimKey> keys = [];

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var chunk = ActorXChunkHeader.Read(reader);

            if (chunk.ChunkId.StartsWith(ActorXChunkId.BoneNames, StringComparison.Ordinal))
                boneRecords = ReadBones(reader, chunk.DataCount);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.AnimationInfo, StringComparison.Ordinal))
                sequences = ReadSequences(reader, chunk.DataCount);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.AnimationKeys, StringComparison.Ordinal))
                keys = ReadKeys(reader, chunk.DataCount);
            else
                reader.BaseStream.Seek(chunk.BodySize, SeekOrigin.Current);
        }

        foreach (var sequence in sequences)
            scene.RootNode.AddNode(BuildAnimation(sequence, boneRecords, keys));
    }

    private static List<ActorXBone> ReadBones(BinaryReader reader, int count)
    {
        var bones = new List<ActorXBone>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ActorXBinary.ReadFixedString(reader, 64);
            uint flags = reader.ReadUInt32();
            int childCount = reader.ReadInt32();
            int parentIndex = reader.ReadInt32();
            Quaternion orientation = ActorXBinary.ReadQuaternion(reader);
            Vector3 position = ActorXBinary.ReadVector3(reader);
            float length = reader.ReadSingle();
            var size = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            bones.Add(new ActorXBone(name, flags, childCount, parentIndex, orientation, position, length, size));
        }

        return bones;
    }

    private static List<ActorXAnimInfo> ReadSequences(BinaryReader reader, int count)
    {
        var sequences = new List<ActorXAnimInfo>(count);
        for (int i = 0; i < count; i++)
        {
            sequences.Add(new ActorXAnimInfo(
                ActorXBinary.ReadFixedString(reader, 64),
                ActorXBinary.ReadFixedString(reader, 64),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()));
        }

        return sequences;
    }

    private static List<ActorXAnimKey> ReadKeys(BinaryReader reader, int count)
    {
        var keys = new List<ActorXAnimKey>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = ActorXBinary.ReadVector3(reader);
            Quaternion orientation = ActorXBinary.ReadQuaternion(reader);
            float time = reader.ReadSingle();
            keys.Add(new ActorXAnimKey(position, orientation, time));
        }

        return keys;
    }

    private static SkeletonAnimation BuildAnimation(ActorXAnimInfo sequence, List<ActorXBone> boneRecords, List<ActorXAnimKey> keys)
    {
        int boneCount = sequence.TotalBones > 0 ? sequence.TotalBones : boneRecords.Count;

        var animation = new SkeletonAnimation(sequence.Name, boneCount, TransformType.Absolute)
        {
            Framerate = sequence.AnimRate > 0f ? sequence.AnimRate : 30f,
            TransformType = TransformType.Absolute,
        };

        var tracks = new SkeletonAnimationTrack[boneCount];
        for (int b = 0; b < boneCount; b++)
        {
            string boneName = b < boneRecords.Count ? boneRecords[b].Name : $"Bone_{b}";
            tracks[b] = new SkeletonAnimationTrack(boneName)
            {
                TransformType = TransformType.Absolute,
                TransformSpace = TransformSpace.Local,
            };
            animation.Tracks.Add(tracks[b]);
        }

        for (int frame = 0; frame < sequence.RawFrameCount; frame++)
        {
            int frameBase = (sequence.FirstRawFrame + frame) * boneCount;
            for (int b = 0; b < boneCount; b++)
            {
                int keyIndex = frameBase + b;
                if ((uint)keyIndex >= (uint)keys.Count)
                    continue;

                var key = keys[keyIndex];
                tracks[b].AddTranslationFrame(frame, key.Position);
                tracks[b].AddRotationFrame(frame, ActorXBinary.ToLocalRotation(key.Orientation, b));
            }
        }

        return animation;
    }
}
