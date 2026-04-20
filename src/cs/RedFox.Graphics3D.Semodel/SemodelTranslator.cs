using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;
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
        => ReadInternal(scene, stream, name, options, options.SourceDirectoryPath, token);

    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
        => ReadInternal(scene, stream, context.Name, context.Options, context.SourceDirectoryPath, token);

    private void ReadInternal(Scene scene, Stream stream, string name, SceneTranslatorOptions options, string? sourceDirectoryPath, CancellationToken? token)
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

        reader.ReadByte(); // reserved
        reader.ReadByte(); // reserved
        reader.ReadByte(); // reserved

        var boneNames = new string[boneCount];
        for (int i = 0; i < boneCount; i++)
            boneNames[i] = reader.ReadUTF8NullTerminatedString();

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
                bone.BindTransform.WorldPosition = reader.ReadStruct<Vector3>();
                bone.BindTransform.WorldRotation = reader.ReadStruct<Quaternion>();
            }

            if (hasLocalTransforms)
            {
                bone.BindTransform.LocalPosition = reader.ReadStruct<Vector3>();
                bone.BindTransform.LocalRotation = reader.ReadStruct<Quaternion>();
            }

            if (hasScaleTransforms)
            {
                bone.BindTransform.Scale = reader.ReadStruct<Vector3>();
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
                int indexSize = boneCount <= byte.MaxValue ? 1 : boneCount <= ushort.MaxValue ? 2 : 4;
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
            var material = model.AddNode<Material>(reader.ReadUTF8NullTerminatedString());

            if (reader.ReadBoolean())
            {
                material.DiffuseMapName  = AssignMaterialTexture(material, "diffuse", reader.ReadUTF8NullTerminatedString(), sourceDirectoryPath);
                material.NormalMapName   = AssignMaterialTexture(material, "normal", reader.ReadUTF8NullTerminatedString(), sourceDirectoryPath);
                material.SpecularMapName = AssignMaterialTexture(material, "specular", reader.ReadUTF8NullTerminatedString(), sourceDirectoryPath);
            }
        }

        // Fix up materials
        Material[] allMaterials = model.GetDescendants<Material>(SceneNodeFlags.NoExport);
        Mesh[] allMeshes = model.GetDescendants<Mesh>(SceneNodeFlags.NoExport);

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
        => WriteInternal(scene, stream, name, options, targetDirectoryPath: null, token);

    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
        => WriteInternal(scene, stream, context.Name, context.Options, context.TargetDirectoryPath, token);

    private void WriteInternal(Scene scene, Stream stream, string name, SceneTranslatorOptions options, string? targetDirectoryPath, CancellationToken? token)
    {
        using var writer = new BinaryWriter(stream, Encoding.Default, true);

        var meshes    = scene.GetDescendants<Mesh>(SceneNodeFlags.NoExport) ?? [];
        var materials = scene.GetDescendants<Material>(SceneNodeFlags.NoExport) ?? [];
        var bones     = scene.GetDescendants<SkeletonBone>(SceneNodeFlags.NoExport) ?? [];
        var boneTable = new Dictionary<SkeletonBone, int>();

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
        var boneIndex = 0;

        foreach (var bone in bones)
        {
            writer.Write((byte)0); // flags
            writer.Write(bone.Parent is SkeletonBone parent ? Array.IndexOf(bones, parent) : -1);

            writer.WriteStruct(bone.GetActiveWorldPosition());
            writer.WriteStruct(bone.GetActiveWorldRotation());
            writer.WriteStruct(bone.GetLiveLocalPosition());
            writer.WriteStruct(bone.GetLiveLocalRotation());
            writer.WriteStruct(bone.GetLiveLocalScale());

            boneTable[bone] = boneIndex++;
        }

        bool hasUVs     = (meshDataPresence & (1 << 0)) != 0;
        bool hasNormals = (meshDataPresence & (1 << 1)) != 0;
        bool hasColours = (meshDataPresence & (1 << 2)) != 0;
        bool hasWeights = (meshDataPresence & (1 << 3)) != 0;

        foreach (var mesh in meshes)
        {
            if (mesh.Positions is null)
                throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' has no position data.");
            if (mesh.FaceIndices is null)
                throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' has no face index data.");

            int vertexCount = mesh.Positions.ElementCount;
            int faceCount   = mesh.FaceIndices.ElementCount / 3;
            int layerCount  = mesh.UVLayers?.ValueCount ?? 0;
            int influences  = mesh.BoneIndices is not null &&
                              mesh.BoneWeights is not null &&
                              mesh.SkinnedBones is not null ? mesh.BoneIndices.ValueCount : 0;

            int[] globalBoneIndexTable = influences > 0 ? mesh.GetBoneIndices(boneTable) : [];

            writer.Write((byte)0); // flags
            writer.Write((byte)layerCount);
            writer.Write((byte)influences);
            writer.Write(vertexCount);
            writer.Write(faceCount);

            // Positions
            for (int v = 0; v < vertexCount; v++)
                writer.WriteStruct(mesh.GetVertexPosition(v, options.WriteRawVertices));

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
                    writer.WriteStruct(mesh.GetVertexNormal(v, options.WriteRawVertices));
            }
            else if (hasNormals)
            {
                // If no normals, write zeroes
                for (int v = 0; v < vertexCount; v++)
                    writer.WriteStruct(Vector3.Zero);
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
            if (influences > 0 && mesh.BoneWeights is not null && mesh.BoneIndices is not null)
            {
                for (int v = 0; v < vertexCount; v++)
                {
                    for (int j = 0; j < influences; j++)
                    {
                        int boneIdx = mesh.BoneIndices.Get<int>(v, j, 0);

                        if (bones.Length <= byte.MaxValue)
                            writer.Write((byte)globalBoneIndexTable[boneIdx]);
                        else if (bones.Length <= ushort.MaxValue)
                            writer.Write((ushort)globalBoneIndexTable[boneIdx]);
                        else
                            writer.Write(globalBoneIndexTable[boneIdx]);

                        writer.Write(mesh.BoneWeights.Get<float>(v, j, 0));
                    }
                }
            }

            // Face indices
            for (int f = 0; f < mesh.FaceIndices.ElementCount; f++)
            {
                if (vertexCount <= byte.MaxValue)
                    writer.Write(mesh.FaceIndices.Get<byte>(f, 0, 0));
                else if (vertexCount <= ushort.MaxValue)
                    writer.Write(mesh.FaceIndices.Get<ushort>(f, 0, 0));
                else
                    writer.Write(mesh.FaceIndices.Get<int>(f, 0, 0));
            }

            // Material indices per layer (skip for now TODO)
            for (int l = 0; l < layerCount; l++)
                writer.Write(0); // default material index
        }

        // ---- Materials ----
        foreach (var material in materials)
        {
            writer.Write(Encoding.ASCII.GetBytes(material.Name));
            writer.Write((byte)0);

            string? diffuseMapName = ResolveMaterialTextureName(material, material.DiffuseMapName, "diffuse", targetDirectoryPath);
            string? normalMapName = ResolveMaterialTextureName(material, material.NormalMapName, "normal", targetDirectoryPath);
            string? specularMapName = ResolveMaterialTextureName(material, material.SpecularMapName, "specular", targetDirectoryPath);

            bool hasImages = diffuseMapName is not null || normalMapName is not null || specularMapName is not null;

            writer.Write(hasImages);

            if (hasImages)
            {
                writer.WriteNullTerminatedString(diffuseMapName ?? string.Empty);
                writer.WriteNullTerminatedString(normalMapName ?? string.Empty);
                writer.WriteNullTerminatedString(specularMapName ?? string.Empty);
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

    private static string? AssignMaterialTexture(Material material, string slot, string? textureReference, string? sourceDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(textureReference))
            return null;

        string textureName = Path.GetFileNameWithoutExtension(textureReference);
        if (string.IsNullOrWhiteSpace(textureName))
            textureName = textureReference;

        string? resolvedPath = ResolveTexturePath(textureReference, sourceDirectoryPath);

        if (material.TryFindChild<Texture>(textureName, StringComparison.OrdinalIgnoreCase, out var existingTexture))
        {
            existingTexture.Slot = slot;
            existingTexture.FilePath = textureReference;
            existingTexture.ResolvedFilePath = resolvedPath;
            existingTexture.Name = textureName;
            return existingTexture.Name;
        }

        var texture = material.AddNode(new Texture(textureReference, slot)
        {
            Name = textureName,
            FilePath = textureReference,
            ResolvedFilePath = resolvedPath,
        });

        return texture.Name;
    }

    private static string? ResolveTexturePath(string textureReference, string? sourceDirectoryPath)
    {
        if (Path.IsPathRooted(textureReference))
            return Path.GetFullPath(textureReference);

        if (!string.IsNullOrWhiteSpace(sourceDirectoryPath))
            return Path.GetFullPath(Path.Combine(sourceDirectoryPath, textureReference));

        return null;
    }

    private static string? ResolveMaterialTextureName(Material material, string? fallbackName, string slot, string? targetDirectoryPath)
    {
        foreach (var texture in material.EnumerateChildren<Texture>(SceneNodeFlags.NoExport))
        {
            if (texture.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase))
                return GetPortableTextureReference(texture, targetDirectoryPath);
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? null : fallbackName;
    }

    private static string GetPortableTextureReference(Texture texture, string? targetDirectoryPath)
    {
        string effectivePath = texture.EffectiveFilePath;
        if (Path.IsPathRooted(effectivePath) && !string.IsNullOrWhiteSpace(targetDirectoryPath))
            return Path.GetRelativePath(targetDirectoryPath, effectivePath);

        return texture.FilePath;
    }

    private static void AssignSkinBinding(Mesh mesh, Skeleton skeleton, SkeletonBone[] bones)
    {
        if (mesh.BoneIndices is null || mesh.BoneWeights is null)
            return;

        mesh.SetSkinBinding(bones);
    }
}
