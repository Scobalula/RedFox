using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Writes a <see cref="Scene"/> selection to an ActorX PSK file, automatically upgrading to the
/// PSKX 32-bit chunk layout when wedge counts exceed the 16-bit limit.
/// </summary>
public sealed class PskWriter
{
    private readonly Stream _stream;

    /// <summary>
    /// Initializes a new <see cref="PskWriter"/>.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    public PskWriter(Stream stream) => _stream = stream;

    /// <summary>
    /// Writes the meshes and skeleton contained in the selection as PSK/PSKX.
    /// </summary>
    /// <param name="selection">The scene selection to export.</param>
    public void Write(SceneTranslationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var meshes = selection.GetDescendants<Mesh>();
        if (meshes.Length == 0)
            throw new InvalidOperationException("Scene must contain at least one mesh to be written as PSK.");

        var bones = selection.GetDescendants<SkeletonBone>();
        SceneNode[] boneNodes = Array.ConvertAll(bones, static bone => (SceneNode)bone);
        var boneIndexMap = new Dictionary<SkeletonBone, int>(bones.Length);
        for (int i = 0; i < bones.Length; i++)
            boneIndexMap[bones[i]] = i;

        var materials = new List<Material>();
        var materialIndices = new Dictionary<Material, int>();

        var points = new List<Vector3>();
        var wedges = new List<ActorXVertex>();
        var faces = new List<ActorXTriangle>();
        var influences = new List<ActorXInfluence>();

        foreach (var mesh in meshes)
        {
            if (mesh.Positions is null || mesh.FaceIndices is null)
                continue;

            byte materialIndex = ResolveMaterialIndex(mesh, selection, materials, materialIndices);
            int baseWedge = wedges.Count;
            int vertexCount = mesh.Positions.ElementCount;
            int influenceCount = mesh.BoneIndices?.ValueCount ?? 0;
            int[]? globalBones = BuildGlobalBoneTable(mesh, boneIndexMap, influenceCount);

            for (int v = 0; v < vertexCount; v++)
            {
                int pointIndex = points.Count;
                points.Add(mesh.Positions.GetVector3(v, 0));

                Vector2 uv = mesh.UVLayers is not null ? mesh.UVLayers.GetVector2(v, 0) : Vector2.Zero;
                wedges.Add(new ActorXVertex(pointIndex, uv, materialIndex));

                if (globalBones is null || mesh.BoneIndices is null || mesh.BoneWeights is null)
                    continue;

                for (int j = 0; j < influenceCount; j++)
                {
                    float weight = mesh.BoneWeights.Get<float>(v, j, 0);
                    if (weight <= 0f)
                        continue;

                    int local = mesh.BoneIndices.Get<int>(v, j, 0);
                    int global = (uint)local < (uint)globalBones.Length ? globalBones[local] : 0;
                    influences.Add(new ActorXInfluence(weight, pointIndex, global));
                }
            }

            int faceCount = mesh.FaceIndices.ElementCount / 3;
            for (int f = 0; f < faceCount; f++)
            {
                int w0 = baseWedge + mesh.FaceIndices.Get<int>(f * 3, 0, 0);
                int w1 = baseWedge + mesh.FaceIndices.Get<int>(f * 3 + 1, 0, 0);
                int w2 = baseWedge + mesh.FaceIndices.Get<int>(f * 3 + 2, 0, 0);
                faces.Add(new ActorXTriangle(w0, w1, w2, materialIndex, 0, 0));
            }
        }

        bool extended = wedges.Count > ushort.MaxValue;

        using var writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);

        ActorXChunkHeader.Write(writer, ActorXChunkId.MeshHeader, ActorXBinary.Version, 0, 0);

        ActorXChunkHeader.Write(writer, ActorXChunkId.Points, ActorXBinary.Version, 12, points.Count);
        foreach (var point in points)
            ActorXBinary.Write(writer, point);

        WriteWedges(writer, wedges, extended);
        WriteFaces(writer, faces, extended);
        WriteMaterials(writer, materials);
        WriteBones(writer, bones, boneNodes);

        ActorXChunkHeader.Write(writer, ActorXChunkId.Weights, ActorXBinary.Version, 12, influences.Count);
        foreach (var influence in influences)
        {
            writer.Write(influence.Weight);
            writer.Write(influence.PointIndex);
            writer.Write(influence.BoneIndex);
        }

        writer.Flush();
    }

    private static byte ResolveMaterialIndex(
        Mesh mesh,
        SceneTranslationSelection selection,
        List<Material> materials,
        Dictionary<Material, int> materialIndices)
    {
        if (mesh.Materials is not [{ } material, ..])
            return 0;

        if (!selection.Includes(material))
            throw new InvalidDataException(
                $"Cannot write PSK: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");

        if (!materialIndices.TryGetValue(material, out int index))
        {
            index = materials.Count;
            materialIndices[material] = index;
            materials.Add(material);
        }

        return (byte)index;
    }

    private static int[]? BuildGlobalBoneTable(Mesh mesh, Dictionary<SkeletonBone, int> boneIndexMap, int influenceCount)
    {
        if (influenceCount <= 0 || mesh.SkinnedBones is not { Count: > 0 } skinnedBones)
            return null;

        var table = new int[skinnedBones.Count];
        for (int i = 0; i < skinnedBones.Count; i++)
        {
            if (!boneIndexMap.TryGetValue(skinnedBones[i], out int global))
                throw new InvalidDataException(
                    $"Cannot write PSK: mesh '{mesh.Name}' references skinned bone '{skinnedBones[i].Name}' that is not included in the export selection.");

            table[i] = global;
        }

        return table;
    }

    private static void WriteWedges(BinaryWriter writer, List<ActorXVertex> wedges, bool extended)
    {
        ActorXChunkHeader.Write(writer, extended ? ActorXChunkId.WedgesExtended : ActorXChunkId.Wedges, ActorXBinary.Version, 16, wedges.Count);

        foreach (var wedge in wedges)
        {
            if (extended)
            {
                writer.Write(wedge.PointIndex);
            }
            else
            {
                writer.Write((ushort)wedge.PointIndex);
                writer.Write((ushort)0);
            }

            writer.Write(wedge.Uv.X);
            writer.Write(wedge.Uv.Y);
            writer.Write(wedge.MaterialIndex);
            writer.Write((byte)0);
            writer.Write((ushort)0);
        }
    }

    private static void WriteFaces(BinaryWriter writer, List<ActorXTriangle> faces, bool extended)
    {
        ActorXChunkHeader.Write(writer, extended ? ActorXChunkId.FacesExtended : ActorXChunkId.Faces, ActorXBinary.Version, extended ? 18 : 12, faces.Count);

        foreach (var face in faces)
        {
            if (extended)
            {
                writer.Write(face.Wedge0);
                writer.Write(face.Wedge1);
                writer.Write(face.Wedge2);
            }
            else
            {
                writer.Write((ushort)face.Wedge0);
                writer.Write((ushort)face.Wedge1);
                writer.Write((ushort)face.Wedge2);
            }

            writer.Write(face.MaterialIndex);
            writer.Write(face.AuxMaterialIndex);
            writer.Write(face.SmoothingGroups);
        }
    }

    private static void WriteMaterials(BinaryWriter writer, List<Material> materials)
    {
        int count = Math.Max(materials.Count, 1);
        ActorXChunkHeader.Write(writer, ActorXChunkId.Materials, ActorXBinary.Version, 88, count);

        if (materials.Count == 0)
        {
            WriteMaterial(writer, "Material");
            return;
        }

        foreach (var material in materials)
            WriteMaterial(writer, material.Name);
    }

    private static void WriteMaterial(BinaryWriter writer, string? name)
    {
        ActorXBinary.WriteFixedString(writer, name, 64);
        writer.Write(0);  // TextureIndex
        writer.Write(0u); // PolyFlags
        writer.Write(0);  // AuxMaterial
        writer.Write(0u); // AuxFlags
        writer.Write(0);  // LodBias
        writer.Write(0);  // LodStyle
    }

    private static void WriteBones(BinaryWriter writer, SkeletonBone[] bones, SceneNode[] boneNodes)
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

        ActorXChunkHeader.Write(writer, ActorXChunkId.Bones, ActorXBinary.Version, 120, bones.Length);

        for (int i = 0; i < bones.Length; i++)
        {
            var bone = bones[i];
            ActorXBinary.WriteFixedString(writer, bone.Name, 64);
            writer.Write(0u);                         // Flags
            writer.Write(childCounts[i]);             // NumChildren
            writer.Write(parentIndices[i] < 0 ? 0 : parentIndices[i]);

            Quaternion stored = ActorXBinary.ToStoredRotation(bone.BindTransform.LocalRotation ?? Quaternion.Identity, i);
            ActorXBinary.Write(writer, stored);
            ActorXBinary.Write(writer, bone.BindTransform.LocalPosition ?? Vector3.Zero);

            writer.Write(0f); // Length
            writer.Write(0f); // XSize
            writer.Write(0f); // YSize
            writer.Write(0f); // ZSize
        }
    }
}
