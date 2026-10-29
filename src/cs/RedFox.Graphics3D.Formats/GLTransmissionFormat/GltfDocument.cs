using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Formats.GLTransmissionFormat;

/// <summary>
/// Represents a complete glTF 2.0 document in memory, containing all top-level arrays (scenes, nodes, meshes, accessors, materials, etc.) that define the asset.
/// </summary>
public sealed class GltfDocument
{
    /// <summary>
    /// Gets or sets the index of the default scene to display, or -1 if not specified.
    /// </summary>
    public int Scene { get; set; } = -1;

    /// <summary>
    /// Gets or sets the list of scenes in this document.
    /// </summary>
    public List<GltfScene> Scenes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of nodes that form the scene graph.
    /// </summary>
    public List<GltfNode> Nodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of meshes.
    /// </summary>
    public List<GltfMesh> Meshes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of accessors.
    /// </summary>
    public List<GltfAccessor> Accessors { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of buffer views.
    /// </summary>
    public List<GltfBufferView> BufferViews { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of buffers.
    /// </summary>
    public List<GltfBuffer> Buffers { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of materials.
    /// </summary>
    public List<GltfMaterial> Materials { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of textures.
    /// </summary>
    public List<GltfTexture> Textures { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of images.
    /// </summary>
    public List<GltfImage> Images { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of texture samplers.
    /// </summary>
    public List<GltfSampler> Samplers { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of skins for skeletal animation.
    /// </summary>
    public List<GltfSkin> Skins { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of animations.
    /// </summary>
    public List<GltfAnimation> Animations { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of cameras.
    /// </summary>
    public List<GltfCamera> Cameras { get; set; } = [];

    /// <summary>
    /// Reads an accessor into tightly packed bytes.
    /// </summary>
    public byte[] ReadAccessorData(int accessorIndex)
    {
        GltfAccessor accessor	= GetAccessorSource(accessorIndex, out ReadOnlyMemory<byte> source, out int stride);
        byte[] result			= new byte[checked(accessor.Count * accessor.ElementSize)];
        if (stride == accessor.ElementSize)
        {
            source.Span.CopyTo(result);
        }
        else if (!source.IsEmpty)
        {
            for (int i = 0; i < accessor.Count; i++)
                source.Span.Slice(i * stride, accessor.ElementSize).CopyTo(result.AsSpan(i * accessor.ElementSize));
        }
        return result;
    }

    /// <summary>
    /// Reads and converts an accessor directly into float storage.
    /// </summary>
    public float[] ReadAccessorAsFloats(int accessorIndex)
    {
        GltfAccessor accessor	= GetAccessorSource(accessorIndex, out ReadOnlyMemory<byte> source, out int stride);
        float[] result			= new float[checked(accessor.Count * accessor.ComponentCount)];
        if (source.IsEmpty)
            return result;

        int componentCount	= accessor.ComponentCount;
        int componentSize	= accessor.ComponentSize;
        if (accessor.ComponentType == GltfConstants.ComponentTypeFloat && BitConverter.IsLittleEndian)
        {
            ReadOnlySpan<float> values = MemoryMarshal.Cast<byte, float>(source.Span);
            if (stride == accessor.ElementSize)
            {
                values.CopyTo(result);
            }
            else
            {
                int floatStride = stride / sizeof(float);
                for (int i = 0; i < accessor.Count; i++)
                    values.Slice(i * floatStride, componentCount).CopyTo(result.AsSpan(i * componentCount));
            }
            return result;
        }

        for (int i = 0; i < accessor.Count; i++)
        {
            for (int c = 0; c < componentCount; c++)
            {
                ReadOnlySpan<byte> value = source.Span[(i * stride + c * componentSize)..];
                result[i * componentCount + c] = accessor.ComponentType switch
                {
                    GltfConstants.ComponentTypeFloat		 => System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(value),
                    GltfConstants.ComponentTypeByte			 => accessor.Normalized ? Math.Max((sbyte)value[0] / 127f, -1f) : (sbyte)value[0],
                    GltfConstants.ComponentTypeUnsignedByte	 => accessor.Normalized ? value[0] / 255f : value[0],
                    GltfConstants.ComponentTypeShort		 => accessor.Normalized ? Math.Max(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(value) / 32767f, -1f) : System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(value),
                    GltfConstants.ComponentTypeUnsignedShort => accessor.Normalized ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value) / 65535f : System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value),
                    GltfConstants.ComponentTypeUnsignedInt	 => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value),
                    _										 => throw new InvalidDataException("Unsupported accessor component type."),
                };
            }
        }
        return result;
    }

    /// <summary>
    /// Reads and converts an accessor directly into integer storage.
    /// </summary>
    public int[] ReadAccessorAsInts(int accessorIndex)
    {
        GltfAccessor accessor	= GetAccessorSource(accessorIndex, out ReadOnlyMemory<byte> source, out int stride);
        int componentCount		= accessor.ComponentCount;
        int componentSize		= accessor.ComponentSize;
        int[] result			= new int[checked(accessor.Count * accessor.ComponentCount)];
        if (source.IsEmpty)
            return result;

        for (int i = 0; i < accessor.Count; i++)
        {
            for (int c = 0; c < componentCount; c++)
            {
                ReadOnlySpan<byte> value = source.Span[(i * stride + c * componentSize)..];
                result[i * componentCount + c] = accessor.ComponentType switch
                {
                    GltfConstants.ComponentTypeByte			 => (sbyte)value[0],
                    GltfConstants.ComponentTypeUnsignedByte	 => value[0],
                    GltfConstants.ComponentTypeShort		 => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(value),
                    GltfConstants.ComponentTypeUnsignedShort => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value),
                    GltfConstants.ComponentTypeUnsignedInt	 => checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value)),
                    GltfConstants.ComponentTypeFloat		 => checked((int)System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(value)),
                    _										 => throw new InvalidDataException("Unsupported accessor component type."),
                };
            }
        }
        return result;
    }

    private GltfAccessor GetAccessorSource(int accessorIndex, out ReadOnlyMemory<byte> source, out int stride)
    {
        if ((uint)accessorIndex >= (uint)Accessors.Count)
            throw new InvalidDataException("Invalid accessor index.");

        GltfAccessor accessor = Accessors[accessorIndex];
        if (accessor.Count < 0 || accessor.ByteOffset < 0 || accessor.ComponentSize == 0 || accessor.ComponentCount == 0 || (long)accessor.Count * accessor.ElementSize > int.MaxValue)
            throw new InvalidDataException("Invalid accessor dimensions.");
        if ((accessor.Type is "MAT2" or "MAT3") && accessor.ComponentSize < 4)
            throw new NotSupportedException("Packed matrix accessors are not supported.");

        stride	= accessor.ElementSize;
        source	= ReadOnlyMemory<byte>.Empty;
        if (accessor.BufferView == -1 && accessor.ByteOffset == 0)
            return accessor;
        if ((uint)accessor.BufferView >= (uint)BufferViews.Count)
            throw new InvalidDataException("Invalid accessor buffer view.");

        GltfBufferView view = BufferViews[accessor.BufferView];
        if ((uint)view.Buffer >= (uint)Buffers.Count || view.ByteOffset < 0 || view.ByteLength < 0 || view.ByteStride < 0)
            throw new InvalidDataException("Invalid buffer view.");

        GltfBuffer buffer = Buffers[view.Buffer];
        if (buffer.Data is null || buffer.ByteLength < 0 || buffer.Data.Length < buffer.ByteLength || (long)view.ByteOffset + view.ByteLength > buffer.ByteLength)
            throw new InvalidDataException("Buffer view exceeds its buffer.");

        stride = view.ByteStride > 0 ? view.ByteStride : accessor.ElementSize;
        long length = accessor.Count == 0 ? 0 : (long)(accessor.Count - 1) * stride + accessor.ElementSize;
        if (stride < accessor.ElementSize || stride % accessor.ComponentSize != 0 || (view.ByteStride != 0 && (stride < 4 || stride > 252 || stride % 4 != 0)) || accessor.ByteOffset % accessor.ComponentSize != 0 || ((long)view.ByteOffset + accessor.ByteOffset) % accessor.ComponentSize != 0 || accessor.ByteOffset + length > view.ByteLength)
            throw new InvalidDataException("Accessor exceeds its buffer view or has an invalid stride.");

        source = buffer.Data.AsMemory(checked(view.ByteOffset + accessor.ByteOffset), checked((int)length));
        return accessor;
    }
}
