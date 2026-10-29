using System.Text;
using RedFox.IO;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.IO.SceneFile;

/// <summary>
/// Reads and writes RedFox scene files.
/// </summary>
public sealed class SceneFileTranslator : SceneTranslator
{
    private const ushort MajorVersion = 1;
    private const ushort MinorVersion = 0;

    private readonly Dictionary<string, Func<SceneNode>>	_factories	= [];
    private readonly Dictionary<Type, string>				_typeNames	= [];

    /// <inheritdoc/>
    public override string Name => "RedFoxScene";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".rfscene"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "RFSC"u8;

    /// <summary>
    /// Initializes the translator with the built-in scene node types.
    /// </summary>
    public SceneFileTranslator()
    {
        RegisterNodeType("Group",				() => new Group());
        RegisterNodeType("MeshGroup",			() => new MeshGroup());
        RegisterNodeType("MaterialGroup",		() => new MaterialGroup());
        RegisterNodeType("Skeleton",			() => new Skeleton());
        RegisterNodeType("SkeletonBone",		() => new SkeletonBone(string.Empty));
        RegisterNodeType("Mesh",				() => new Mesh());
        RegisterNodeType("Material",			() => new Material());
        RegisterNodeType("Texture",				() => new Texture(string.Empty));
        RegisterNodeType("Light",				() => new Light());
        RegisterNodeType("Camera",				() => new Camera());
        RegisterNodeType("Hair",				() => new Hair());
        RegisterNodeType("SceneReference",		() => new SceneReference(string.Empty, string.Empty));
        RegisterNodeType("OrientConstraint",	() => new OrientConstraintNode(string.Empty, new Group(), new Group()));
        RegisterNodeType("ParentConstraint",	() => new ParentConstraintNode(string.Empty, new Group(), new Group()));
        RegisterNodeType("PointConstraint",		() => new PointConstraintNode(string.Empty, new Group(), new Group()));
        RegisterNodeType("ScaleConstraint",		() => new ScaleConstraintNode(string.Empty, new Group(), new Group()));
        RegisterNodeType("IKHandle",			() => new IKHandleNode(string.Empty, new Group(), new Group()));
        RegisterNodeType("SkeletonAnimation",	() => new SkeletonAnimation());
        RegisterNodeType("MorphAnimation",		() => new MorphAnimation(string.Empty));
    }

    /// <summary>
    /// Registers a factory for a scene node type stored in scene files.
    /// </summary>
    /// <typeparam name="T">The scene node type produced by the factory.</typeparam>
    /// <param name="typeName">The type name stored in the file.</param>
    /// <param name="factory">Creates a scene node instance.</param>
    public void RegisterNodeType<T>(string typeName, Func<T> factory) where T : SceneNode
    {
        _factories[typeName]	= factory;
        _typeNames[typeof(T)]	= typeName;
    }

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        using BinaryReader reader = new(stream, Encoding.UTF8, true);

        if (!reader.ReadBytes(MagicValue.Length).AsSpan().SequenceEqual(MagicValue))
            throw new InvalidDataException("Invalid RedFox Scene file: incorrect magic number.");

        ushort majorVersion = reader.ReadUInt16();
        reader.ReadUInt16();

        if (majorVersion != MajorVersion)
            throw new InvalidDataException($"Unsupported RedFox Scene version: {majorVersion}.");

        int count = reader.ReadInt32();
        if (count < 0 || count > stream.Length - stream.Position)
            throw new InvalidDataException("Invalid scene node count.");

        SceneNode[]	nodes			= new SceneNode[count];
        int[]		parentIndices	= new int[nodes.Length];

        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i]			= _factories.TryGetValue(reader.ReadString(), out Func<SceneNode>? factory) ? factory() : new Group();
            parentIndices[i]	= reader.ReadInt32();
        }

        byte[] states = new byte[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            int current = i;
            while (current != -1 && states[current] == 0)
            {
                states[current] = 1;
                int parent = parentIndices[current];
                if (parent < -1 || parent >= nodes.Length)
                    throw new InvalidDataException("Invalid scene parent index.");
                current = parent;
            }
            if (current != -1 && states[current] == 1)
                throw new InvalidDataException("Cyclic scene hierarchy.");
            current = i;
            while (current != -1 && states[current] == 1)
            {
                states[current]	= 2;
                current			= parentIndices[current];
            }
        }
        scene.LinkHierarchyUnsafe(nodes, parentIndices);

        ReadRecord(reader, scene, nodes);

        foreach (SceneNode node in nodes)
        {
            token?.ThrowIfCancellationRequested();
            ReadRecord(reader, node, nodes);
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        List<SceneNode> nodes = [];
        CollectNodes(scene, nodes);

        Dictionary<SceneNode, int> nodeIndices = new(ReferenceEqualityComparer.Instance);

        for (int i = 0; i < nodes.Count; i++)
            nodeIndices.Add(nodes[i], i);

        using BinaryWriter writer = new(stream, Encoding.UTF8, true);

        writer.Write(MagicValue);
        writer.Write(MajorVersion);
        writer.Write(MinorVersion);
        writer.Write(nodes.Count);

        foreach (SceneNode node in nodes)
        {
            writer.Write(GetTypeName(node.GetType()));
            writer.Write(node.Parent is { } parent && nodeIndices.TryGetValue(parent, out int parentIndex) ? parentIndex : -1);
        }

        WriteRecord(writer, scene, nodeIndices);

        foreach (SceneNode node in nodes)
        {
            token?.ThrowIfCancellationRequested();
            WriteRecord(writer, node, nodeIndices);
        }
    }

    private void CollectNodes(SceneNode parent, List<SceneNode> nodes)
    {
        if (parent.Children is null)
            return;

        foreach (SceneNode child in parent.Children)
        {
            nodes.Add(child);
            CollectNodes(child, nodes);
        }
    }

    private string GetTypeName(Type? type)
    {
        while (type is not null)
        {
            if (_typeNames.TryGetValue(type, out string? typeName))
                return typeName;

            type = type.BaseType;
        }

        return "Group";
    }

    private static void ReadRecord(BinaryReader reader, SceneNode node, IReadOnlyList<SceneNode> nodes)
    {
        int size	= reader.ReadInt32();
        long start	= reader.BaseStream.Position;
        if (size < 0 || size > reader.BaseStream.Length - start)
            throw new InvalidDataException("Invalid scene record length.");
        using SubStream record = new(reader.BaseStream, start, size, true);
        using BinaryReader recordReader = new(record, Encoding.UTF8, true);
        node.Read(recordReader, nodes);
        if (record.Position > record.Length)
            throw new InvalidDataException("Scene record exceeded its boundary.");
        reader.BaseStream.Position = start + size;
    }

    private static void WriteRecord(BinaryWriter writer, SceneNode node, IReadOnlyDictionary<SceneNode, int> nodeIndices)
    {
        long sizePosition = writer.BaseStream.Position;
        writer.Write(0);

        node.Write(writer, nodeIndices);

        long end = writer.BaseStream.Position;
        writer.BaseStream.Position = sizePosition;
        writer.Write(checked((int)(end - sizePosition - sizeof(int))));
        writer.BaseStream.Position = end;
    }
}
