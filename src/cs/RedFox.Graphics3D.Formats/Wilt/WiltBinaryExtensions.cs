using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using RedFox.Graphics3D.Buffers;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.Wilt;

internal static class WiltBinaryExtensions
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 0;

    public static void WriteWiltHeader(this BinaryWriter writer, ReadOnlySpan<byte> magic, SceneUpAxis upAxis)
    {
        writer.Write(magic);
        writer.Write(MajorVersion);
        writer.Write(MinorVersion);
        writer.Write((byte)upAxis);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
    }

    public static SceneUpAxis ReadWiltHeader(this BinaryReader reader, ReadOnlySpan<byte> magic)
    {
        if (!reader.ReadBytes(magic.Length).AsSpan().SequenceEqual(magic))
            throw new InvalidDataException("Invalid Wilt file: incorrect magic number.");

        ushort majorVersion = reader.ReadUInt16();

        if (majorVersion != MajorVersion)
            throw new InvalidDataException($"Unsupported Wilt major version: {majorVersion}.");

        reader.ReadUInt16();

        SceneUpAxis upAxis = (SceneUpAxis)reader.ReadByte();

        reader.ReadBytes(3);
        return upAxis;
    }

    public static long BeginWiltSection(this BinaryWriter writer, WiltSection section)
    {
        writer.Write((uint)section);
        writer.Write(0u);
        return writer.BaseStream.Position;
    }

    public static void EndWiltSection(this BinaryWriter writer, long start)
    {
        long end = writer.BaseStream.Position;

        writer.BaseStream.Position = start - sizeof(uint);
        writer.Write(checked((uint)(end - start)));
        writer.BaseStream.Position = end;
    }

    public static void WriteWiltString(this BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);

        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    public static string ReadWiltString(this BinaryReader reader) => Encoding.UTF8.GetString(reader.ReadBytes(checked((int)reader.ReadUInt32())));

    public static void WriteWiltValue(this BinaryWriter writer, object? value)
    {
        switch (value)
        {
            case null:			writer.Write((byte)WiltValueType.None);								break;
            case bool v:		writer.Write((byte)WiltValueType.Bool);		writer.Write(v);			break;
            case int v:			writer.Write((byte)WiltValueType.S32);		writer.Write(v);			break;
            case long v:		writer.Write((byte)WiltValueType.S64);		writer.Write(v);			break;
            case float v:		writer.Write((byte)WiltValueType.F32);		writer.Write(v);			break;
            case double v:		writer.Write((byte)WiltValueType.F64);		writer.Write(v);			break;
            case string v:		writer.Write((byte)WiltValueType.String);	writer.WriteWiltString(v);	break;
            case Vector2 v:		writer.Write((byte)WiltValueType.Vec2);		writer.WriteStruct(v);		break;
            case Vector3 v:		writer.Write((byte)WiltValueType.Vec3);		writer.WriteStruct(v);		break;
            case Vector4 v:		writer.Write((byte)WiltValueType.Vec4);		writer.WriteStruct(v);		break;
            case Quaternion v:	writer.Write((byte)WiltValueType.Quat);		writer.WriteStruct(v);		break;
            default:			throw new InvalidDataException($"Wilt cannot store values of type {value.GetType().Name}.");
        }
    }

    public static object? ReadWiltValue(this BinaryReader reader)
    {
        WiltValueType type = (WiltValueType)reader.ReadByte();

        return type switch
        {
            WiltValueType.None		=> null,
            WiltValueType.Bool		=> reader.ReadBoolean(),
            WiltValueType.S32		=> reader.ReadInt32(),
            WiltValueType.S64		=> reader.ReadInt64(),
            WiltValueType.F32		=> reader.ReadSingle(),
            WiltValueType.F64		=> reader.ReadDouble(),
            WiltValueType.String	=> reader.ReadWiltString(),
            WiltValueType.Vec2		=> reader.ReadStruct<Vector2>(),
            WiltValueType.Vec3		=> reader.ReadStruct<Vector3>(),
            WiltValueType.Vec4		=> reader.ReadStruct<Vector4>(),
            WiltValueType.Quat		=> reader.ReadStruct<Quaternion>(),
            _						=> throw new InvalidDataException($"Unsupported Wilt value type: {type}."),
        };
    }

    public static void WriteWiltMetadata(this BinaryWriter writer, Dictionary<string, object>? attributes)
    {
        long section = writer.BeginWiltSection(WiltSection.Meta);

        writer.Write((uint)(attributes?.Count ?? 0));

        foreach ((string key, object value) in attributes ?? [])
        {
            writer.WriteWiltString(key);
            writer.WriteWiltValue(value);
        }

        writer.EndWiltSection(section);
    }

    public static void ReadWiltMetadata(this BinaryReader reader, SceneNode node)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
        {
            string key = reader.ReadWiltString();
            (node.Attributes ??= [])[key] = reader.ReadWiltValue() ?? throw new InvalidDataException($"Wilt metadata '{key}' has no value.");
        }
    }

    public static void WriteWiltBuffer(this BinaryWriter writer, DataBuffer? buffer)
    {
        if (buffer is null)
        {
            writer.Write((byte)WiltBufferType.None);
            return;
        }

        bool hasRawData = buffer.TryGetRawData(out DataBufferRawData rawData);

        writer.Write((byte)(hasRawData ? GetBufferType(rawData.ComponentType) : WiltBufferType.F32));
        writer.Write(checked((byte)buffer.ComponentCount));
        writer.Write(checked((ushort)buffer.ValueCount));
        writer.Write((uint)buffer.ElementCount);

        if (hasRawData)
        {
            writer.Write(rawData.Bytes);
        }
        else
        {
            writer.Write(MemoryMarshal.AsBytes(buffer.AsReadOnlySpan<float>()));
        }
    }

    public static DataBuffer? ReadWiltBuffer(this BinaryReader reader)
    {
        WiltBufferType type = (WiltBufferType)reader.ReadByte();

        if (type == WiltBufferType.None)
            return null;

        int componentCount	= reader.ReadByte();
        int valueCount		= reader.ReadUInt16();
        int elementCount	= checked((int)reader.ReadUInt32());

        return type switch
        {
            WiltBufferType.U8	=> ReadBuffer<byte>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.S8	=> ReadBuffer<sbyte>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.U16	=> ReadBuffer<ushort>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.S16	=> ReadBuffer<short>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.U32	=> ReadBuffer<uint>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.S32	=> ReadBuffer<int>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.F16	=> ReadBuffer<Half>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.F32	=> ReadBuffer<float>(reader, elementCount, valueCount, componentCount),
            WiltBufferType.F64	=> ReadBuffer<double>(reader, elementCount, valueCount, componentCount),
            _					=> throw new InvalidDataException($"Unsupported Wilt buffer type: {type}."),
        };
    }

    private static WiltBufferType GetBufferType(Type componentType)
    {
        if (componentType == typeof(byte))		return WiltBufferType.U8;
        if (componentType == typeof(sbyte))		return WiltBufferType.S8;
        if (componentType == typeof(ushort))	return WiltBufferType.U16;
        if (componentType == typeof(short))		return WiltBufferType.S16;
        if (componentType == typeof(uint))		return WiltBufferType.U32;
        if (componentType == typeof(int))		return WiltBufferType.S32;
        if (componentType == typeof(Half))		return WiltBufferType.F16;
        if (componentType == typeof(float))		return WiltBufferType.F32;
        if (componentType == typeof(double))	return WiltBufferType.F64;

        throw new InvalidDataException($"Wilt cannot store buffers of type {componentType.Name}.");
    }

    private static DataBuffer<T> ReadBuffer<T>(BinaryReader reader, int elementCount, int valueCount, int componentCount) where T : unmanaged, INumber<T>
    {
        T[] values = new T[elementCount * valueCount * componentCount];
        reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));
        return new DataBuffer<T>(values, valueCount, componentCount);
    }
}
