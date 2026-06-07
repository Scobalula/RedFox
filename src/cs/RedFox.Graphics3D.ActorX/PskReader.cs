using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Reads ActorX PSK and PSKX mesh files into a <see cref="Scene"/>, including geometry,
/// materials, the reference skeleton, and skin weights.
/// </summary>
public sealed class PskReader
{
    private readonly Stream _stream;
    private readonly string _name;

    /// <summary>
    /// Initializes a new <see cref="PskReader"/>.
    /// </summary>
    /// <param name="stream">The source stream positioned at the start of the file.</param>
    /// <param name="name">The logical name used for created nodes.</param>
    public PskReader(Stream stream, string name)
    {
        _stream = stream;
        _name = name;
    }

    /// <summary>
    /// Reads the PSK/PSKX content and populates the supplied scene.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    public void Read(Scene scene)
    {
        using var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true);

        var header = ActorXChunkHeader.Read(reader);
        if (!header.ChunkId.StartsWith(ActorXChunkId.MeshHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Invalid PSK file: missing ACTRHEAD chunk.");

        Vector3[] points = [];
        List<ActorXVertex> wedges = [];
        List<ActorXTriangle> faces = [];
        List<ActorXMaterial> materials = [];
        List<ActorXBone> boneRecords = [];
        List<ActorXInfluence> influences = [];

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var chunk = ActorXChunkHeader.Read(reader);

            if (chunk.ChunkId.StartsWith(ActorXChunkId.Points, StringComparison.Ordinal))
                points = ReadPoints(reader, chunk.DataCount);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.WedgesExtended, StringComparison.Ordinal))
                wedges = ReadWedges(reader, chunk.DataCount, extended: true);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.Wedges, StringComparison.Ordinal))
                wedges = ReadWedges(reader, chunk.DataCount, extended: false);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.FacesExtended, StringComparison.Ordinal))
                faces = ReadFaces(reader, chunk.DataCount, extended: true);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.Faces, StringComparison.Ordinal))
                faces = ReadFaces(reader, chunk.DataCount, extended: false);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.Materials, StringComparison.Ordinal))
                materials = ReadMaterials(reader, chunk.DataCount);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.Bones, StringComparison.Ordinal))
                boneRecords = ReadBones(reader, chunk.DataCount);
            else if (chunk.ChunkId.StartsWith(ActorXChunkId.Weights, StringComparison.Ordinal))
                influences = ReadInfluences(reader, chunk.DataCount);
            else
                reader.BaseStream.Seek(chunk.BodySize, SeekOrigin.Current);
        }

        var model = scene.RootNode.AddNode<MeshGroup>(_name);
        var bones = BuildSkeleton(model, boneRecords);
        BuildMeshes(model, points, wedges, faces, materials, bones, influences);
    }

    private static Vector3[] ReadPoints(BinaryReader reader, int count)
    {
        var points = new Vector3[count];
        for (int i = 0; i < count; i++)
            points[i] = ActorXBinary.ReadVector3(reader);
        return points;
    }

    private static List<ActorXVertex> ReadWedges(BinaryReader reader, int count, bool extended)
    {
        var wedges = new List<ActorXVertex>(count);
        for (int i = 0; i < count; i++)
        {
            int pointIndex;
            if (extended)
            {
                pointIndex = reader.ReadInt32();
            }
            else
            {
                pointIndex = reader.ReadUInt16();
                reader.ReadUInt16(); // padding
            }

            var uv = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            byte materialIndex = reader.ReadByte();
            reader.ReadByte();   // reserved
            reader.ReadUInt16(); // padding

            wedges.Add(new ActorXVertex(pointIndex, uv, materialIndex));
        }

        return wedges;
    }

    private static List<ActorXTriangle> ReadFaces(BinaryReader reader, int count, bool extended)
    {
        var faces = new List<ActorXTriangle>(count);
        for (int i = 0; i < count; i++)
        {
            int w0 = extended ? reader.ReadInt32() : reader.ReadUInt16();
            int w1 = extended ? reader.ReadInt32() : reader.ReadUInt16();
            int w2 = extended ? reader.ReadInt32() : reader.ReadUInt16();
            byte materialIndex = reader.ReadByte();
            byte auxMaterialIndex = reader.ReadByte();
            uint smoothingGroups = reader.ReadUInt32();

            faces.Add(new ActorXTriangle(w0, w1, w2, materialIndex, auxMaterialIndex, smoothingGroups));
        }

        return faces;
    }

    private static List<ActorXMaterial> ReadMaterials(BinaryReader reader, int count)
    {
        var materials = new List<ActorXMaterial>(count);
        for (int i = 0; i < count; i++)
        {
            materials.Add(new ActorXMaterial(
                ActorXBinary.ReadFixedString(reader, 64),
                reader.ReadInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()));
        }

        return materials;
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

    private static List<ActorXInfluence> ReadInfluences(BinaryReader reader, int count)
    {
        var influences = new List<ActorXInfluence>(count);
        for (int i = 0; i < count; i++)
            influences.Add(new ActorXInfluence(reader.ReadSingle(), reader.ReadInt32(), reader.ReadInt32()));
        return influences;
    }

    private static SkeletonBone[] BuildSkeleton(MeshGroup model, List<ActorXBone> boneRecords)
    {
        var bones = new SkeletonBone[boneRecords.Count];
        for (int i = 0; i < bones.Length; i++)
            bones[i] = new SkeletonBone(boneRecords[i].Name);

        for (int i = 0; i < bones.Length; i++)
        {
            var record = boneRecords[i];
            bool isRoot = i == 0 || record.ParentIndex < 0 || record.ParentIndex >= bones.Length || record.ParentIndex == i;

            bones[i].MoveTo(isRoot ? model : bones[record.ParentIndex], ReparentTransformMode.PreserveExisting);
            bones[i].BindTransform.LocalPosition = record.Position;
            bones[i].BindTransform.LocalRotation = ActorXBinary.ToLocalRotation(record.Orientation, i);
        }

        return bones;
    }

    private static void BuildMeshes(
        MeshGroup model,
        Vector3[] points,
        List<ActorXVertex> wedges,
        List<ActorXTriangle> faces,
        List<ActorXMaterial> materials,
        SkeletonBone[] bones,
        List<ActorXInfluence> influences)
    {
        var influencesByPoint = GroupInfluences(influences, out int maxInfluences);
        bool hasSkinning = maxInfluences > 0 && bones.Length > 0;

        var materialNodes = BuildMaterials(model, materials);

        var facesByMaterial = new Dictionary<int, List<ActorXTriangle>>();
        foreach (var face in faces)
        {
            int materialIndex = face.MaterialIndex >= 0 && face.MaterialIndex < materialNodes.Length ? face.MaterialIndex : 0;
            if (!facesByMaterial.TryGetValue(materialIndex, out var bucket))
                facesByMaterial[materialIndex] = bucket = [];
            bucket.Add(face);
        }

        foreach (var (materialIndex, materialFaces) in facesByMaterial)
        {
            var material = materialNodes[materialIndex];
            var mesh = model.AddNode<Mesh>(material.Name);

            var remap = new Dictionary<int, int>();
            var order = new List<int>();

            int Resolve(int wedgeIndex)
            {
                if (!remap.TryGetValue(wedgeIndex, out int local))
                {
                    local = order.Count;
                    remap[wedgeIndex] = local;
                    order.Add(wedgeIndex);
                }

                return local;
            }

            var faceIndices = new int[materialFaces.Count * 3];
            for (int f = 0; f < materialFaces.Count; f++)
            {
                var face = materialFaces[f];
                faceIndices[f * 3] = Resolve(face.Wedge0);
                faceIndices[f * 3 + 1] = Resolve(face.Wedge1);
                faceIndices[f * 3 + 2] = Resolve(face.Wedge2);
            }

            int vertexCount = order.Count;
            var positions = new float[vertexCount * 3];
            var uvs = new float[vertexCount * 2];
            int[]? boneIndices = hasSkinning ? new int[vertexCount * maxInfluences] : null;
            float[]? boneWeights = hasSkinning ? new float[vertexCount * maxInfluences] : null;

            for (int v = 0; v < vertexCount; v++)
            {
                var wedge = wedges[order[v]];
                var position = (uint)wedge.PointIndex < (uint)points.Length ? points[wedge.PointIndex] : Vector3.Zero;

                positions[v * 3] = position.X;
                positions[v * 3 + 1] = position.Y;
                positions[v * 3 + 2] = position.Z;
                uvs[v * 2] = wedge.Uv.X;
                uvs[v * 2 + 1] = wedge.Uv.Y;

                if (boneIndices is null || boneWeights is null)
                    continue;

                influencesByPoint.TryGetValue(wedge.PointIndex, out var links);
                for (int j = 0; j < maxInfluences; j++)
                {
                    bool present = links is not null && j < links.Count;
                    boneIndices[v * maxInfluences + j] = present ? links![j].BoneIndex : 0;
                    boneWeights[v * maxInfluences + j] = present ? links![j].Weight : 0f;
                }
            }

            mesh.Positions = new DataBuffer<float>(positions, 1, 3);
            mesh.UVLayers = new DataBuffer<float>(uvs, 1, 2);
            mesh.FaceIndices = new DataBuffer<int>(faceIndices, 1, 1);
            mesh.Materials = [material];

            if (boneIndices is not null && boneWeights is not null)
            {
                mesh.BoneIndices = new DataBuffer<int>(boneIndices, maxInfluences, 1);
                mesh.BoneWeights = new DataBuffer<float>(boneWeights, maxInfluences, 1);
                mesh.SetSkinBinding(bones);
            }
        }
    }

    private static Dictionary<int, List<(int BoneIndex, float Weight)>> GroupInfluences(List<ActorXInfluence> influences, out int maxInfluences)
    {
        var grouped = new Dictionary<int, List<(int BoneIndex, float Weight)>>();
        maxInfluences = 0;

        foreach (var influence in influences)
        {
            if (!grouped.TryGetValue(influence.PointIndex, out var list))
                grouped[influence.PointIndex] = list = [];

            list.Add((influence.BoneIndex, influence.Weight));
            if (list.Count > maxInfluences)
                maxInfluences = list.Count;
        }

        return grouped;
    }

    private static Material[] BuildMaterials(MeshGroup model, List<ActorXMaterial> materials)
    {
        if (materials.Count == 0)
            return [model.AddNode<Material>($"{model.Name}_Material")];

        var nodes = new Material[materials.Count];
        for (int i = 0; i < materials.Count; i++)
        {
            string name = string.IsNullOrWhiteSpace(materials[i].Name) ? $"Material_{i}" : materials[i].Name;
            nodes[i] = model.AddNode<Material>(name);
        }

        return nodes;
    }
}
