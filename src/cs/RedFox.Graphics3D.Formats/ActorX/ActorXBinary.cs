using System.Numerics;
using System.Text;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.ActorX;

/// <summary>
/// Low-level binary helpers shared by the ActorX PSK/PSKX and PSA readers and writers.
/// </summary>
public static class ActorXBinary
{
    /// <summary>
    /// The version stamp written into the <see cref="ActorXChunkHeader.TypeFlag"/> field of emitted chunks.
    /// </summary>
    public const int Version = 1999801;

    /// <summary>
    /// Reads a fixed-length, null-padded ASCII string and trims any trailing padding.
    /// </summary>
    /// <param name="reader">The source reader.</param>
    /// <param name="length">The fixed field length in bytes.</param>
    /// <returns>The decoded string.</returns>
    public static string ReadFixedString(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        int end = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
    }

    /// <summary>
    /// Writes a string into a fixed-length, null-padded ASCII field, always leaving a trailing terminator.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="value">The value to write; longer values are truncated to fit the field.</param>
    /// <param name="length">The fixed field length in bytes.</param>
    public static void WriteFixedString(BinaryWriter writer, string? value, int length)
    {
        Span<byte> buffer = stackalloc byte[length];
        buffer.Clear();

        if (!string.IsNullOrEmpty(value))
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            bytes.AsSpan(0, Math.Min(bytes.Length, length - 1)).CopyTo(buffer);
        }

        writer.Write(buffer);
    }

    /// <summary>Reads a <see cref="Vector3"/> as three single-precision floats.</summary>
    public static Vector3 ReadVector3(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    /// <summary>Reads a <see cref="Quaternion"/> as four single-precision floats in X, Y, Z, W order.</summary>
    public static Quaternion ReadQuaternion(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    /// <summary>Writes a <see cref="Vector3"/> as three single-precision floats.</summary>
    public static void Write(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    /// <summary>Writes a <see cref="Quaternion"/> as four single-precision floats in X, Y, Z, W order.</summary>
    public static void Write(BinaryWriter writer, Quaternion value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    /// <summary>
    /// Converts a stored ActorX bone orientation into a local-space rotation.
    /// ActorX stores the root bone unmodified and conjugates every child bone, so this applies the inverse.
    /// </summary>
    /// <param name="stored">The orientation as stored in the file.</param>
    /// <param name="boneIndex">The bone's index within the skeleton.</param>
    public static Quaternion ToLocalRotation(Quaternion stored, int boneIndex)
        => boneIndex == 0 ? stored : Quaternion.Conjugate(stored);

    /// <summary>
    /// Converts a local-space rotation into the orientation stored by ActorX, mirroring <see cref="ToLocalRotation"/>.
    /// </summary>
    /// <param name="local">The local-space rotation.</param>
    /// <param name="boneIndex">The bone's index within the skeleton.</param>
    public static Quaternion ToStoredRotation(Quaternion local, int boneIndex)
        => boneIndex == 0 ? local : Quaternion.Conjugate(local);

    internal static List<ActorXBone> ReadBones(BinaryReader reader, int count)
    {
        var bones = new List<ActorXBone>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadFixedString(reader, 64);
            uint flags = reader.ReadUInt32();
            int childCount = reader.ReadInt32();
            int parentIndex = reader.ReadInt32();
            Quaternion orientation = ReadQuaternion(reader);
            Vector3 position = ReadVector3(reader);
            float length = reader.ReadSingle();
            var size = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            bones.Add(new ActorXBone(name, flags, childCount, parentIndex, orientation, position, length, size));
        }

        return bones;
    }

    internal static void WriteBones(BinaryWriter writer, string chunkId, SkeletonBone[] bones, SceneNode[] boneNodes)
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

        ActorXChunkHeader.Write(writer, chunkId, Version, 120, bones.Length);

        for (int i = 0; i < bones.Length; i++)
        {
            SkeletonBone bone = bones[i];
            WriteFixedString(writer, bone.Name, 64);
            writer.Write(0u);
            writer.Write(childCounts[i]);
            writer.Write(parentIndices[i] < 0 ? 0 : parentIndices[i]);
            Write(writer, ToStoredRotation(bone.GetBindLocalRotation(), i));
            Write(writer, bone.GetBindLocalPosition());
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(0f);
        }
    }
}
