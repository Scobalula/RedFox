namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents a complete glTF 2.0 document in memory, containing all top-level
/// arrays (scenes, nodes, meshes, accessors, materials, etc.) that define the asset.
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
    /// Reads the raw binary data for the specified accessor, returning the bytes as a
    /// contiguous array regardless of buffer view stride.
    /// </summary>
    /// <param name="accessorIndex">The index of the accessor to read.</param>
    /// <returns>A byte array containing the accessor's data in tightly packed form.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the accessor's buffer view or buffer data has not been resolved.
    /// </exception>
    public byte[] ReadAccessorData(int accessorIndex)
    {
        GltfAccessor accessor = Accessors[accessorIndex];

        if (accessor.BufferView < 0)
            return new byte[accessor.Count * accessor.ElementSize];

        GltfBufferView view = BufferViews[accessor.BufferView];
        GltfBuffer buffer = Buffers[view.Buffer];

        if (buffer.Data is null)
            throw new InvalidOperationException($"Buffer {view.Buffer} data has not been loaded.");

        int elementSize = accessor.ElementSize;
        int stride = view.ByteStride > 0 ? view.ByteStride : elementSize;
        int sourceOffset = view.ByteOffset + accessor.ByteOffset;
        int totalBytes = accessor.Count * elementSize;

        byte[] result = new byte[totalBytes];

        if (stride == elementSize)
        {
            Buffer.BlockCopy(buffer.Data, sourceOffset, result, 0, totalBytes);
        }
        else
        {
            for (int i = 0; i < accessor.Count; i++)
            {
                Buffer.BlockCopy(buffer.Data, sourceOffset + i * stride, result, i * elementSize, elementSize);
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the accessor data as an array of single-precision floats, converting from
    /// the accessor's native component type as needed.
    /// </summary>
    /// <param name="accessorIndex">The index of the accessor to read.</param>
    /// <returns>An array of floats containing the accessor's element data.</returns>
    public float[] ReadAccessorAsFloats(int accessorIndex)
    {
        GltfAccessor accessor = Accessors[accessorIndex];
        byte[] raw = ReadAccessorData(accessorIndex);
        int componentCount = accessor.ComponentCount;
        int totalComponents = accessor.Count * componentCount;
        float[] result = new float[totalComponents];

        switch (accessor.ComponentType)
        {
            case GltfConstants.ComponentTypeFloat:
                System.Buffer.BlockCopy(raw, 0, result, 0, totalComponents * 4);
                break;

            case GltfConstants.ComponentTypeByte:
                for (int i = 0; i < totalComponents; i++)
                    result[i] = accessor.Normalized ? Math.Max((sbyte)raw[i] / 127f, -1f) : (sbyte)raw[i];
                break;

            case GltfConstants.ComponentTypeUnsignedByte:
                for (int i = 0; i < totalComponents; i++)
                    result[i] = accessor.Normalized ? raw[i] / 255f : raw[i];
                break;

            case GltfConstants.ComponentTypeShort:
                for (int i = 0; i < totalComponents; i++)
                {
                    short v = BitConverter.ToInt16(raw, i * 2);
                    result[i] = accessor.Normalized ? Math.Max(v / 32767f, -1f) : v;
                }
                break;

            case GltfConstants.ComponentTypeUnsignedShort:
                for (int i = 0; i < totalComponents; i++)
                {
                    ushort v = BitConverter.ToUInt16(raw, i * 2);
                    result[i] = accessor.Normalized ? v / 65535f : v;
                }
                break;

            case GltfConstants.ComponentTypeUnsignedInt:
                for (int i = 0; i < totalComponents; i++)
                    result[i] = BitConverter.ToUInt32(raw, i * 4);
                break;

            default:
                throw new InvalidOperationException($"Unsupported accessor component type: {accessor.ComponentType}");
        }

        return result;
    }

    /// <summary>
    /// Reads the accessor data as an array of integers, converting from the accessor's
    /// native component type as needed. Typically used for face indices and joint indices.
    /// </summary>
    /// <param name="accessorIndex">The index of the accessor to read.</param>
    /// <returns>An array of integers containing the accessor's element data.</returns>
    public int[] ReadAccessorAsInts(int accessorIndex)
    {
        GltfAccessor accessor = Accessors[accessorIndex];
        byte[] raw = ReadAccessorData(accessorIndex);
        int totalElements = accessor.Count * accessor.ComponentCount;
        int[] result = new int[totalElements];

        switch (accessor.ComponentType)
        {
            case GltfConstants.ComponentTypeUnsignedByte:
                for (int i = 0; i < totalElements; i++)
                    result[i] = raw[i];
                break;

            case GltfConstants.ComponentTypeByte:
                for (int i = 0; i < totalElements; i++)
                    result[i] = (sbyte)raw[i];
                break;

            case GltfConstants.ComponentTypeUnsignedShort:
                for (int i = 0; i < totalElements; i++)
                    result[i] = BitConverter.ToUInt16(raw, i * 2);
                break;

            case GltfConstants.ComponentTypeShort:
                for (int i = 0; i < totalElements; i++)
                    result[i] = BitConverter.ToInt16(raw, i * 2);
                break;

            case GltfConstants.ComponentTypeUnsignedInt:
                for (int i = 0; i < totalElements; i++)
                    result[i] = (int)BitConverter.ToUInt32(raw, i * 4);
                break;

            case GltfConstants.ComponentTypeFloat:
                for (int i = 0; i < totalElements; i++)
                    result[i] = (int)BitConverter.ToSingle(raw, i * 4);
                break;

            default:
                throw new InvalidOperationException($"Unsupported accessor component type: {accessor.ComponentType}");
        }

        return result;
    }
}
