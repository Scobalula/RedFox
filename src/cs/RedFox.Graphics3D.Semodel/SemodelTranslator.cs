using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace RedFox.Graphics3D.Semodel;

/// <summary>
/// Provides functionality to read and write scenes in the SEModel file format.
/// </summary>
public class SemodelTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "SEModel";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".semodel"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "SEModel"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        using var reader = new BinaryReader(stream, Encoding.Default, true);

        var magic = reader.ReadBytes(MagicValue.Length);

        if (!magic.AsSpan().SequenceEqual(MagicValue))
            throw new InvalidDataException("Invalid SEModel file: incorrect magic number.");

        var version    = reader.ReadUInt16();
        var headerSize = reader.ReadUInt16();

        if (version != 1)
            throw new InvalidDataException($"Unsupported SEModel version: {version}.");
        if (headerSize < 20)
            throw new InvalidDataException($"Invalid SEModel header size: {headerSize}.");

        var dataPresence     = reader.ReadByte();
        var boneDataPresence = reader.ReadByte();
        var meshDataPresence = reader.ReadByte();

        var boneCount = reader.ReadInt32();
        var meshCount = reader.ReadInt32();
        var matCount  = reader.ReadInt32();

        reader.ReadBytes(3); // reserved

        var boneNames = new string[boneCount];
        for (int i = 0; i < boneCount; i++)
            boneNames[i] = ReadUTF8String(reader);

        bool hasWorldTransforms = (boneDataPresence & (1 << 0)) != 0;
        bool hasLocalTransforms = (boneDataPresence & (1 << 1)) != 0;
        bool hasScaleTransforms = (boneDataPresence & (1 << 2)) != 0;

        var boneParents = new int[boneCount];
        var bones = new SkeletonBone[boneCount];

        for (int i = 0; i < boneCount; i++)
        {
            if (reader.ReadByte() != 0)
                throw new InvalidDataException("Invalid SEModel file: expected bone flag to be 0.");

            boneParents[i] = reader.ReadInt32();

            var bone = new SkeletonBone(boneNames[i]);

            if (hasWorldTransforms)
            {
                bone.BindTransform.WorldPosition = ReadVector3(reader);
                bone.BindTransform.WorldRotation = ReadQuaternion(reader);
            }

            if (hasLocalTransforms)
            {
                bone.BindTransform.LocalPosition = ReadVector3(reader);
                bone.BindTransform.LocalRotation = ReadQuaternion(reader);
            }

            if (hasScaleTransforms)
            {
                bone.BindTransform.Scale = ReadVector3(reader);
            }

            bones[i] = bone;
        }

        // ---- Build skeleton hierarchy ----
        var skeleton = scene.RootNode.AddNode<Skeleton>($"{name}_Skeleton");

        for (int i = 0; i < bones.Length; i++)
        {
            if (boneParents[i] != -1)
                bones[i].MoveTo(bones[boneParents[i]], ReparentTransformMode.PreserveExisting);
            else
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
        }

        bool hasUVs     = (meshDataPresence & (1 << 0)) != 0;
        bool hasNormals = (meshDataPresence & (1 << 1)) != 0;
        bool hasColours = (meshDataPresence & (1 << 2)) != 0;
        bool hasWeights = (meshDataPresence & (1 << 3)) != 0;

        var model = scene.RootNode.AddNode<Model>(name);
        var materialIndices = new List<int[]>(meshCount);

        for (int i = 0; i < meshCount; i++)
        {
            if (reader.ReadByte() != 0)
                throw new InvalidDataException("Invalid SEModel file: expected mesh flag to be 0.");

            int layerCount  = reader.ReadByte();
            int influences  = reader.ReadByte();
            int vertexCount = reader.ReadInt32();
            int faceCount   = reader.ReadInt32();

            var mesh = model.AddNode<Mesh>();

            mesh.Positions = new DataBuffer<float>(reader.ReadBytes(12 * vertexCount), 1, 3);

            if (hasUVs)
                mesh.UVLayers = new DataBuffer<float>(reader.ReadBytes(8 * vertexCount * layerCount), layerCount, 2);
            if (hasNormals)
                mesh.Normals = new DataBuffer<float>(reader.ReadBytes(12 * vertexCount), 1, 3);
            if (hasColours)
                mesh.ColorLayers = new DataBuffer<byte>(reader.ReadBytes(4 * vertexCount), 1, 4);

            if (hasWeights && influences > 0)
            {
                int indexSize = boneCount <= byte.MaxValue ? 1 :
                                boneCount <= ushort.MaxValue ? 2 : 4;
                int influenceStride = indexSize + sizeof(float);
                int vertexStride    = influences * influenceStride;

                var weightData = reader.ReadBytes(vertexCount * vertexStride);

                mesh.BoneIndices = indexSize switch
                {
                    1 => new DataBufferView<byte>(weightData,   vertexCount, byteOffset: 0, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride),
                    2 => new DataBufferView<ushort>(weightData,  vertexCount, byteOffset: 0, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride),
                    _ => new DataBufferView<int>(weightData,     vertexCount, byteOffset: 0, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride),
                };
                mesh.BoneWeights = new DataBufferView<float>(weightData, vertexCount, byteOffset: indexSize, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride);

                AssignSkinBinding(mesh, skeleton, bones);
            }

            // Face indices — bulk read
            if (vertexCount <= byte.MaxValue)
                mesh.FaceIndices = new DataBuffer<byte>(reader.ReadBytes(faceCount * 3), 1, 1);
            else if (vertexCount <= ushort.MaxValue)
                mesh.FaceIndices = new DataBuffer<ushort>(reader.ReadBytes(2 * faceCount * 3), 1, 1);
            else
                mesh.FaceIndices = new DataBuffer<int>(reader.ReadBytes(4 * faceCount * 3), 1, 1);

            // Material indices per layer
            var perMeshMaterialIndices = new int[layerCount];

            for (int m = 0; m < layerCount; m++)
                perMeshMaterialIndices[m] = reader.ReadInt32();

            materialIndices.Add(perMeshMaterialIndices);
        }

        // ---- Materials ----
        for (int m = 0; m < matCount; m++)
        {
            var material = model.AddNode<Material>(ReadUTF8String(reader));

            if (reader.ReadBoolean())
            {
                material.DiffuseMapName  = AssignMaterialTexture(material, "diffuse", ReadUTF8String(reader));
                material.NormalMapName   = AssignMaterialTexture(material, "normal", ReadUTF8String(reader));
                material.SpecularMapName = AssignMaterialTexture(material, "specular", ReadUTF8String(reader));
            }
        }

        // Fix up materials
        Material[] allMaterials = model.GetDescendants<Material>();
        Mesh[] allMeshes = model.GetDescendants<Mesh>();

        for (int i = 0; i < allMeshes.Length && i < materialIndices.Count; i++)
        {
            int[] matIndices = materialIndices[i];
            List<Material> meshMaterials = new(matIndices.Length);

            foreach (int matIndex in matIndices)
            {
                if (matIndex >= 0 && matIndex < allMaterials.Length)
                {
                    meshMaterials.Add(allMaterials[matIndex]);
                }
            }

            if (meshMaterials.Count > 0)
            {
                allMeshes[i].Materials = meshMaterials;
            }
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        using var writer = new BinaryWriter(stream, Encoding.Default, true);

        var skeleton  = scene.TryGetFirstOfType<Skeleton>();
        var model     = scene.TryGetFirstOfType<Model>();
        var meshes    = model?.GetDescendants<Mesh>() ?? [];
        var materials = model?.GetDescendants<Material>() ?? [];
        var bones     = skeleton?.GetDescendants<SkeletonBone>() ?? [];

        // Determine data presence flags from actual data
        bool hasBones  = bones.Length > 0;
        bool hasMeshes = meshes.Length > 0;
        bool hasMats   = materials.Length > 0;

        byte dataPresence = 0;
        if (hasBones)  dataPresence |= 1;
        if (hasMeshes) dataPresence |= 2;
        if (hasMats)   dataPresence |= 4;

        var meshDataPresence = ComputeMeshDataPresence(meshes);

        // ---- Header ----
        writer.Write(MagicValue);
        writer.Write((ushort)1);    // version
        writer.Write((ushort)0x14); // headerSize
        writer.Write(dataPresence);
        writer.Write((byte)0x7); // boneDataPresence: world + local + scale
        writer.Write(meshDataPresence);

        writer.Write(bones.Length);
        writer.Write(meshes.Length);
        writer.Write(materials.Length);

        writer.Write((byte)0); // reserved
        writer.Write((byte)0);
        writer.Write((byte)0);

        // ---- Bone names ----
        foreach (var bone in bones)
        {
            writer.Write(Encoding.ASCII.GetBytes(bone.Name));
            writer.Write((byte)0);
        }

        // ---- Bone data ----
        foreach (var bone in bones)
        {
            writer.Write((byte)0); // flags
            writer.Write(bone.Parent is SkeletonBone parent ? Array.IndexOf(bones, parent) : -1);

            // World transforms
            var wp = bone.GetBindWorldPosition();
            var wr = bone.GetBindWorldRotation();
            WriteVector3(writer, wp);
            WriteQuaternion(writer, wr);

            // Local transforms
            var lp = bone.GetBindLocalPosition();
            var lr = bone.GetBindLocalRotation();
            WriteVector3(writer, lp);
            WriteQuaternion(writer, lr);

            // Scale
            var s = bone.GetBindLocalScale();
            WriteVector3(writer, s);
        }

        bool hasUVs     = (meshDataPresence & (1 << 0)) != 0;
        bool hasNormals = (meshDataPresence & (1 << 1)) != 0;
        bool hasColours = (meshDataPresence & (1 << 2)) != 0;
        bool hasWeights = (meshDataPresence & (1 << 3)) != 0;

        // ---- Meshes ----
        Span<Matrix4x4> stackSkinTransforms = stackalloc Matrix4x4[128];
        foreach (var mesh in meshes)
        {
            if (mesh.Positions is null)
                throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' has no position data.");
            if (mesh.FaceIndices is null)
                throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' has no face index data.");

            int vertexCount = mesh.Positions.ElementCount;
            int faceCount   = mesh.FaceIndices.ElementCount / 3;
            int layerCount  = mesh.UVLayers?.ValueCount ?? 0;
            int influences  = mesh.BoneIndices is not null && mesh.BoneWeights is not null
                ? mesh.BoneIndices.ValueCount : 0;
            int[] globalBoneIndexTable = BuildGlobalBoneIndexTable(mesh, bones);
            int skinnedBoneCount = options.WriteRawVertices ? 0 : mesh.SkinnedBones?.Count ?? 0;
            Matrix4x4[]? rentedSkinTransforms = null;
            Span<Matrix4x4> skinTransforms = skinnedBoneCount == 0
                ? []
                : skinnedBoneCount <= stackSkinTransforms.Length
                    ? stackSkinTransforms[..skinnedBoneCount]
                    : (rentedSkinTransforms = ArrayPool<Matrix4x4>.Shared.Rent(skinnedBoneCount)).AsSpan(0, skinnedBoneCount);

            if (skinnedBoneCount > 0)
                mesh.CopySkinTransforms(skinTransforms);

            try
            {
                writer.Write((byte)0); // flags
                writer.Write((byte)layerCount);
                writer.Write((byte)influences);
                writer.Write(vertexCount);
                writer.Write(faceCount);

                // Positions
                for (int v = 0; v < vertexCount; v++)
                    WriteVector3(writer, mesh.GetVertexPosition(v, skinTransforms));

                // UVs
                if (mesh.UVLayers is not null)
                {
                    for (int v = 0; v < vertexCount; v++)
                    {
                        for (int l = 0; l < layerCount; l++)
                        {
                            var uv = mesh.UVLayers.GetVector2(v, l);
                            writer.Write(uv.X);
                            writer.Write(uv.Y);
                        }
                    }
                }
                else if (hasUVs)
                {
                    // If no UVs, write zeroes
                    for (int v = 0; v < vertexCount; v++)
                    {
                        for (int l = 0; l < layerCount; l++)
                        {
                            writer.Write(0f);
                            writer.Write(0f);
                        }
                    }
                }

                // Normals
                if (mesh.Normals is not null)
                {
                    for (int v = 0; v < vertexCount; v++)
                        WriteVector3(writer, mesh.GetVertexNormal(v, skinTransforms));
                }
                else if (hasNormals)
                {
                    // If no normals, write zeroes
                    for (int v = 0; v < vertexCount; v++)
                        WriteVector3(writer, Vector3.Zero);
                }

                // Colors
                if (mesh.ColorLayers is not null)
                {
                    for (int v = 0; v < vertexCount; v++)
                    {
                        writer.Write(mesh.ColorLayers.Get<byte>(v, 0, 0));
                        writer.Write(mesh.ColorLayers.Get<byte>(v, 0, 1));
                        writer.Write(mesh.ColorLayers.Get<byte>(v, 0, 2));
                        writer.Write(mesh.ColorLayers.Get<byte>(v, 0, 3));
                    }
                }
                else if (hasColours)
                {
                    // If no vertex colors, write white with full alpha
                    for (int v = 0; v < vertexCount; v++)
                    {
                        writer.Write((byte)255);
                        writer.Write((byte)255);
                        writer.Write((byte)255);
                        writer.Write((byte)255);
                    }
                }

                // Bone influences
                if (influences > 0 && mesh.BoneWeights is not null)
                {
                    for (int v = 0; v < vertexCount; v++)
                    {
                        for (int j = 0; j < influences; j++)
                        {
                            int boneIdx = ResolveGlobalBoneIndex(mesh, globalBoneIndexTable, v, j);

                            if (bones.Length <= byte.MaxValue)
                                writer.Write((byte)boneIdx);
                            else if (bones.Length <= ushort.MaxValue)
                                writer.Write((ushort)boneIdx);
                            else
                                writer.Write(boneIdx);

                            writer.Write(mesh.BoneWeights!.Get<float>(v, j, 0));
                        }
                    }
                }

                // Face indices
                for (int f = 0; f < mesh.FaceIndices.ElementCount; f++)
                {
                    int idx = mesh.FaceIndices.Get<int>(f, 0, 0);

                    if (vertexCount <= byte.MaxValue)
                        writer.Write((byte)idx);
                    else if (vertexCount <= ushort.MaxValue)
                        writer.Write((ushort)idx);
                    else
                        writer.Write(idx);
                }

                // Material indices per layer (skip for now TODO)
                for (int l = 0; l < layerCount; l++)
                    writer.Write(0); // default material index
            }
            finally
            {
                if (rentedSkinTransforms is not null)
                    ArrayPool<Matrix4x4>.Shared.Return(rentedSkinTransforms);
            }
        }

        // ---- Materials ----
        foreach (var material in materials)
        {
            writer.Write(Encoding.ASCII.GetBytes(material.Name));
            writer.Write((byte)0);

            string? diffuseMapName = ResolveMaterialTextureName(material, material.DiffuseMapName, "diffuse");
            string? normalMapName = ResolveMaterialTextureName(material, material.NormalMapName, "normal");
            string? specularMapName = ResolveMaterialTextureName(material, material.SpecularMapName, "specular");

            bool hasImages = diffuseMapName is not null
                          || normalMapName is not null
                          || specularMapName is not null;

            writer.Write(hasImages);

            if (hasImages)
            {
                WriteNullTermString(writer, diffuseMapName ?? string.Empty);
                WriteNullTermString(writer, normalMapName ?? string.Empty);
                WriteNullTermString(writer, specularMapName ?? string.Empty);
            }
        }
    }

    private static byte ComputeMeshDataPresence(Mesh[] meshes)
    {
        byte flags = 0;
        foreach (var mesh in meshes)
        {
            if (mesh.UVLayers is not null)    flags |= 1;
            if (mesh.Normals is not null)     flags |= 2;
            if (mesh.ColorLayers is not null) flags |= 4;
            if (mesh.BoneWeights is not null) flags |= 8;
        }
        return flags;
    }

    private static string? AssignMaterialTexture(Material material, string slot, string? textureNodeName)
    {
        if (string.IsNullOrWhiteSpace(textureNodeName))
            return null;

        if (material.TryFindChild<Texture>(textureNodeName, StringComparison.OrdinalIgnoreCase, out var existingTexture))
        {
            existingTexture.Slot = slot;
            existingTexture.FilePath = textureNodeName;
            existingTexture.Name = textureNodeName;
            return existingTexture.Name;
        }

        var texture = material.AddNode(new Texture(textureNodeName, slot)
        {
            Name = textureNodeName,
            FilePath = textureNodeName,
        });

        return texture.Name;
    }

    private static string? ResolveMaterialTextureName(Material material, string? fallbackName, string slot)
    {
        foreach (var texture in material.EnumerateChildren<Texture>())
        {
            if (texture.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase))
                return texture.FilePath;
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? null : fallbackName;
    }

    private static void AssignSkinBinding(Mesh mesh, Skeleton skeleton, SkeletonBone[] bones)
    {
        if (mesh.BoneIndices is null || mesh.BoneWeights is null)
            return;

        mesh.SetSkinBinding(bones);
    }

    private static int[] BuildGlobalBoneIndexTable(Mesh mesh, SkeletonBone[] bones)
    {
        var skinnedBones = mesh.SkinnedBones ?? bones;
        var globalIndices = new Dictionary<SkeletonBone, int>(bones.Length);

        for (int i = 0; i < bones.Length; i++)
            globalIndices[bones[i]] = i;

        var table = new int[skinnedBones.Count];
        for (int i = 0; i < skinnedBones.Count; i++)
        {
            if (!globalIndices.TryGetValue(skinnedBones[i], out int globalBoneIndex))
                throw new InvalidDataException($"Mesh '{mesh.Name}' references a skinned bone that is not part of the exported skeleton.");

            table[i] = globalBoneIndex;
        }

        return table;
    }

    private static int ResolveGlobalBoneIndex(Mesh mesh, int[] globalBoneIndexTable, int vertexIndex, int influenceIndex)
    {
        if (mesh.BoneIndices is null)
            throw new InvalidOperationException($"Mesh '{mesh.Name}' has no bone index data.");

        int boneIndex = mesh.BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
        if (boneIndex < 0 || boneIndex >= globalBoneIndexTable.Length)
            throw new InvalidDataException($"Mesh '{mesh.Name}' contains an invalid skin index {boneIndex}.");

        return globalBoneIndexTable[boneIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 ReadVector3(BinaryReader reader) => new(
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quaternion ReadQuaternion(BinaryReader reader) => new(
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteVector3(BinaryWriter writer, Vector3 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteQuaternion(BinaryWriter writer, Quaternion q)
    {
        writer.Write(q.X);
        writer.Write(q.Y);
        writer.Write(q.Z);
        writer.Write(q.W);
    }

    private static void WriteNullTermString(BinaryWriter writer, string s)
    {
        writer.Write(Encoding.ASCII.GetBytes(s));
        writer.Write((byte)0);
    }

    internal static string ReadUTF8String(BinaryReader reader)
    {
        var sb = new StringBuilder(32);

        while (true)
        {
            var c = reader.ReadByte();
            if (c == 0) break;
            sb.Append((char)c);
        }

        return sb.ToString();
    }
}
