using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.IO.SceneFile;

/// <summary>
/// Provides binary read and write operations for scene file data.
/// </summary>
public static class SceneFileBinaryExtensions
{
    private static readonly Dictionary<Type, SceneBufferType> BufferTypes = new()
    {
        [typeof(sbyte)]		= SceneBufferType.Int8,
        [typeof(byte)]		= SceneBufferType.UInt8,
        [typeof(short)]		= SceneBufferType.Int16,
        [typeof(ushort)]	= SceneBufferType.UInt16,
        [typeof(int)]		= SceneBufferType.Int32,
        [typeof(uint)]		= SceneBufferType.UInt32,
        [typeof(long)]		= SceneBufferType.Int64,
        [typeof(ulong)]		= SceneBufferType.UInt64,
        [typeof(Half)]		= SceneBufferType.Half,
        [typeof(float)]		= SceneBufferType.Single,
        [typeof(double)]	= SceneBufferType.Double,
    };

    /// <summary>
    /// Writes an unmanaged value as its in-memory bytes.
    /// </summary>
    public static void WriteValue<T>(this BinaryWriter writer, T value) where T : unmanaged => writer.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)));

    /// <summary>
    /// Reads an unmanaged value from its in-memory bytes.
    /// </summary>
    public static T ReadValue<T>(this BinaryReader reader) where T : unmanaged
    {
        T value = default;
        reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(new Span<T>(ref value)));
        return value;
    }

    /// <summary>
    /// Writes an optional unmanaged value with a presence flag.
    /// </summary>
    public static void WriteOptional<T>(this BinaryWriter writer, T? value) where T : unmanaged
    {
        writer.Write(value.HasValue);

        if (value.HasValue)
            writer.WriteValue(value.Value);
    }

    /// <summary>
    /// Reads an optional unmanaged value with a presence flag.
    /// </summary>
    public static T? ReadOptional<T>(this BinaryReader reader) where T : unmanaged => reader.ReadBoolean() ? reader.ReadValue<T>() : null;

    /// <summary>
    /// Writes an optional string with a presence flag.
    /// </summary>
    public static void WriteOptional(this BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);

        if (value is not null)
            writer.Write(value);
    }

    /// <summary>
    /// Reads an optional string with a presence flag.
    /// </summary>
    public static string? ReadOptionalString(this BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;

    /// <summary>
    /// Writes an unmanaged array with its element count.
    /// </summary>
    public static void WriteArray<T>(this BinaryWriter writer, ReadOnlySpan<T> values) where T : unmanaged
    {
        writer.Write(values.Length);
        writer.Write(MemoryMarshal.AsBytes(values));
    }

    /// <summary>
    /// Reads an unmanaged array with its element count.
    /// </summary>
    public static T[] ReadArray<T>(this BinaryReader reader) where T : unmanaged
    {
        T[] values = new T[reader.ReadInt32()];
        reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }

    /// <summary>
    /// Writes a string list with its element count.
    /// </summary>
    public static void WriteStrings(this BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);

        foreach (string value in values)
            writer.Write(value);
    }

    /// <summary>
    /// Reads a string list with its element count.
    /// </summary>
    public static string[] ReadStrings(this BinaryReader reader)
    {
        string[] values = new string[reader.ReadInt32()];

        for (int i = 0; i < values.Length; i++)
            values[i] = reader.ReadString();

        return values;
    }

    /// <summary>
    /// Writes a scene node reference as its index, or -1 when it is not present.
    /// </summary>
    public static void WriteNode(this BinaryWriter writer, SceneNode? node, IReadOnlyDictionary<SceneNode, int> nodeIndices) => writer.Write(node is not null && nodeIndices.TryGetValue(node, out int index) ? index : -1);

    /// <summary>
    /// Reads a scene node reference by index.
    /// </summary>
    public static T? ReadNode<T>(this BinaryReader reader, IReadOnlyList<SceneNode> nodes) where T : SceneNode
    {
        int index = reader.ReadInt32();
        return index < 0 ? null : nodes[index] as T;
    }

    /// <summary>
    /// Writes scene node references as indices.
    /// </summary>
    public static void WriteNodes(this BinaryWriter writer, IReadOnlyList<SceneNode> values, IReadOnlyDictionary<SceneNode, int> nodeIndices)
    {
        writer.Write(values.Count);

        foreach (SceneNode value in values)
            writer.WriteNode(value, nodeIndices);
    }

    /// <summary>
    /// Reads a list of scene node references by index.
    /// </summary>
    public static List<T> ReadNodes<T>(this BinaryReader reader, IReadOnlyList<SceneNode> nodes) where T : SceneNode
    {
        int count = reader.ReadInt32();
        List<T> values = new(count);

        for (int i = 0; i < count; i++)
            values.Add(reader.ReadNode<T>(nodes)!);

        return values;
    }

    /// <summary>
    /// Writes a data buffer and its component type, or a null marker.
    /// </summary>
    public static void WriteBuffer(this BinaryWriter writer, DataBuffer? buffer)
    {
        if (buffer is null)
        {
            writer.Write((byte)SceneBufferType.None);
            return;
        }

        bool hasRawData = buffer.TryGetRawData(out DataBufferRawData rawData) && BufferTypes.ContainsKey(rawData.ComponentType);

        writer.Write((byte)(hasRawData ? BufferTypes[rawData.ComponentType] : SceneBufferType.Single));
        writer.Write(buffer.ElementCount);
        writer.Write(buffer.ValueCount);
        writer.Write(buffer.ComponentCount);

        if (hasRawData)
        {
            writer.Write(rawData.Bytes);
        }
        else
        {
            writer.Write(MemoryMarshal.AsBytes(buffer.AsReadOnlySpan<float>()));
        }
    }

    /// <summary>
    /// Reads a data buffer and its component type.
    /// </summary>
    public static DataBuffer? ReadBuffer(this BinaryReader reader)
    {
        SceneBufferType type = (SceneBufferType)reader.ReadByte();

        if (type == SceneBufferType.None)
            return null;

        int elementCount	= reader.ReadInt32();
        int valueCount		= reader.ReadInt32();
        int componentCount	= reader.ReadInt32();

        return type switch
        {
            SceneBufferType.Int8	=> ReadBuffer<sbyte>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.UInt8	=> ReadBuffer<byte>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.Int16	=> ReadBuffer<short>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.UInt16	=> ReadBuffer<ushort>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.Int32	=> ReadBuffer<int>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.UInt32	=> ReadBuffer<uint>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.Int64	=> ReadBuffer<long>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.UInt64	=> ReadBuffer<ulong>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.Half	=> ReadBuffer<Half>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.Single	=> ReadBuffer<float>(reader, elementCount, valueCount, componentCount),
            SceneBufferType.Double	=> ReadBuffer<double>(reader, elementCount, valueCount, componentCount),
            _						=> throw new InvalidDataException($"Unsupported buffer type: {type}."),
        };
    }

    /// <summary>
    /// Writes an attribute value with its type marker.
    /// </summary>
    public static void WriteAttribute(this BinaryWriter writer, object value)
    {
        switch (value)
        {
            case bool v:		writer.Write((byte)SceneAttributeType.Bool);			writer.Write(v); break;
            case int v:			writer.Write((byte)SceneAttributeType.Int32);		writer.Write(v); break;
            case uint v:		writer.Write((byte)SceneAttributeType.UInt32);		writer.Write(v); break;
            case long v:		writer.Write((byte)SceneAttributeType.Int64);		writer.Write(v); break;
            case ulong v:		writer.Write((byte)SceneAttributeType.UInt64);		writer.Write(v); break;
            case UInt128 v:		writer.Write((byte)SceneAttributeType.UInt128);		writer.WriteValue(v); break;
            case float v:		writer.Write((byte)SceneAttributeType.Single);		writer.Write(v); break;
            case double v:		writer.Write((byte)SceneAttributeType.Double);		writer.Write(v); break;
            case Vector2 v:		writer.Write((byte)SceneAttributeType.Vector2);		writer.WriteValue(v); break;
            case Vector3 v:		writer.Write((byte)SceneAttributeType.Vector3);		writer.WriteValue(v); break;
            case Vector4 v:		writer.Write((byte)SceneAttributeType.Vector4);		writer.WriteValue(v); break;
            case Quaternion v:	writer.Write((byte)SceneAttributeType.Quaternion);	writer.WriteValue(v); break;
            case Matrix4x4 v:	writer.Write((byte)SceneAttributeType.Matrix4x4);	writer.WriteValue(v); break;
            default:			writer.Write((byte)SceneAttributeType.String);		writer.Write(value.ToString() ?? string.Empty); break;
        }
    }

    /// <summary>
    /// Reads an attribute value using its type marker.
    /// </summary>
    public static object ReadAttribute(this BinaryReader reader)
    {
        SceneAttributeType type = (SceneAttributeType)reader.ReadByte();

        return type switch
        {
            SceneAttributeType.Bool			=> reader.ReadBoolean(),
            SceneAttributeType.Int32		=> reader.ReadInt32(),
            SceneAttributeType.UInt32		=> reader.ReadUInt32(),
            SceneAttributeType.Int64		=> reader.ReadInt64(),
            SceneAttributeType.UInt64		=> reader.ReadUInt64(),
            SceneAttributeType.UInt128		=> reader.ReadValue<UInt128>(),
            SceneAttributeType.Single		=> reader.ReadSingle(),
            SceneAttributeType.Double		=> reader.ReadDouble(),
            SceneAttributeType.String		=> reader.ReadString(),
            SceneAttributeType.Vector2		=> reader.ReadValue<Vector2>(),
            SceneAttributeType.Vector3		=> reader.ReadValue<Vector3>(),
            SceneAttributeType.Vector4		=> reader.ReadValue<Vector4>(),
            SceneAttributeType.Quaternion	=> reader.ReadValue<Quaternion>(),
            SceneAttributeType.Matrix4x4	=> reader.ReadValue<Matrix4x4>(),
            _								=> throw new InvalidDataException($"Unsupported attribute type: {type}."),
        };
    }

    private static DataBuffer<T> ReadBuffer<T>(BinaryReader reader, int elementCount, int valueCount, int componentCount) where T : unmanaged, INumber<T>
    {
        T[] values = new T[elementCount * valueCount * componentCount];
        reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));
        return new DataBuffer<T>(values, valueCount, componentCount);
    }
}
