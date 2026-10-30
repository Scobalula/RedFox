using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace RedFox.Graphics3D.Formats.SEModel;

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
    public override SceneTranslatorContent Content => SceneTranslatorContent.Model;

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "SEModel"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        using var reader = new BinaryReader(stream, Encoding.Default, true);

        var magic = ReadPayload(reader, MagicValue.Length);

        if (!magic.AsSpan().SequenceEqual(MagicValue))
            throw new InvalidDataException("Invalid SEModel file: incorrect magic number.");

        var	version		= reader.ReadUInt16();
        var	headerSize	= reader.ReadUInt16();

        if (version != 1)
            throw new InvalidDataException($"Unsupported SEModel version: {version}.");
        if (headerSize < 20)
            throw new InvalidDataException($"Invalid SEModel header size: {headerSize}.");

        long headerStart = stream.Position;
        if (headerSize > stream.Length - headerStart)
            throw new InvalidDataException("Truncated SEModel header.");

        var	dataPresence		= reader.ReadByte();
        var	boneDataPresence	= reader.ReadByte();
        var	meshDataPresence	= reader.ReadByte();

        var	boneCount	= reader.ReadInt32();
        var	meshCount	= reader.ReadInt32();
        var	matCount	= reader.ReadInt32();

        reader.ReadByte();
        reader.ReadByte();
        reader.ReadByte();

        if (boneCount < 0 || meshCount < 0 || matCount < 0 || (long)boneCount + meshCount + matCount > stream.Length - stream.Position)
            throw new InvalidDataException("Invalid SEModel counts.");
        stream.Position = headerStart + headerSize - 2;

        var boneNames = new string[boneCount];
        for (int i = 0; i < boneCount; i++)
            boneNames[i] = reader.ReadUTF8NullTerminatedString();

        bool hasWorldTransforms = (boneDataPresence & (1 << 0)) != 0;
        bool hasLocalTransforms = (boneDataPresence & (1 << 1)) != 0;
        bool hasScaleTransforms = (boneDataPresence & (1 << 2)) != 0;

        var boneParents	= new int[boneCount];
        var bones		= new SkeletonBone[boneCount];

        for (int i = 0; i < boneCount; i++)
        {
            token?.ThrowIfCancellationRequested();
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

        var skeleton	= scene.RootNode.AddNode(new Skeleton($"{context.Name}_Skeleton"));
        byte[] states	= new byte[boneParents.Length];
        for (int i = 0; i < boneParents.Length; i++)
        {
            int current = i;
            while (current != -1 && states[current] == 0)
            {
                states[current] = 1;
                int parent = boneParents[current];
                if (parent < -1 || parent >= boneParents.Length)
                    throw new InvalidDataException("Invalid SEModel parent index.");
                current = parent;
            }
            if (current != -1 && states[current] == 1)
                throw new InvalidDataException("Cyclic SEModel hierarchy.");
            current = i;
            while (current != -1 && states[current] == 1)
            {
                states[current]	= 2;
                current			= boneParents[current];
            }
        }
        skeleton.LinkHierarchyUnsafe(bones, boneParents);

        bool	hasUVs		= (meshDataPresence & (1 << 0)) != 0;
        bool	hasNormals	= (meshDataPresence & (1 << 1)) != 0;
        bool	hasColours	= (meshDataPresence & (1 << 2)) != 0;
        bool	hasWeights	= (meshDataPresence & (1 << 3)) != 0;

        var model			= scene.RootNode.AddNode<MeshGroup>(context.Name);
        var materialIndices	= new List<int[]>(meshCount);

        for (int i = 0; i < meshCount; i++)
        {
            token?.ThrowIfCancellationRequested();
            if (reader.ReadByte() != 0)
            {
                throw new InvalidDataException("Invalid SEModel file: expected mesh flag to be 0.");
            }

            int	layerCount	= reader.ReadByte();
            int	influences	= reader.ReadByte();
            int	vertexCount	= reader.ReadInt32();
            int	faceCount	= reader.ReadInt32();

            if (vertexCount < 0 || faceCount < 0)
                throw new InvalidDataException("Invalid SEModel mesh dimensions.");
            var mesh = model.AddNode<Mesh>();

            mesh.Positions = new DataBuffer<float>(ReadPayload(reader, checked(12 * vertexCount)), 1, 3);

            if (hasUVs)
            {
                mesh.UVLayers = new DataBuffer<float>(ReadPayload(reader, checked(8 * vertexCount * layerCount)), layerCount, 2);
            }
            if (hasNormals)
            {
                mesh.Normals = new DataBuffer<float>(ReadPayload(reader, checked(12 * vertexCount)), 1, 3);
            }
            if (hasColours)
            {
                mesh.ColorLayers = new DataBuffer<byte>(ReadPayload(reader, checked(4 * vertexCount)), 1, 4);
            }

            if (hasWeights && influences > 0)
            {
                int indexSize;
                if (boneCount <= byte.MaxValue)
                    indexSize = 1;
                else if (boneCount <= ushort.MaxValue)
                    indexSize = 2;
                else
                    indexSize = 4;
                int	influenceStride	= indexSize + sizeof(float);
                int	vertexStride	= influences * influenceStride;

                byte[] weightData = ReadPayload(reader, checked(vertexCount * vertexStride));

                DataBuffer boneIndices = indexSize switch
                {
                    1 => DataBufferPacking.CreateStrided<byte>(weightData, vertexCount, byteOffset: 0, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride),
                    2 => DataBufferPacking.CreateStrided<ushort>(weightData, vertexCount, byteOffset: 0, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride),
                    _ => DataBufferPacking.CreateStrided<int>(weightData, vertexCount, byteOffset: 0, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride),
                };
                DataBuffer boneWeights = DataBufferPacking.CreateStrided<float>(weightData, vertexCount, byteOffset: indexSize, byteStride: vertexStride, valueCount: influences, componentCount: 1, byteValueStride: influenceStride);

                mesh.Skin = new Skin(bones, boneIndices, boneWeights);
            }

            if (vertexCount <= byte.MaxValue)
            {
                mesh.FaceIndices = new DataBuffer<byte>(ReadPayload(reader, checked(faceCount * 3)), 1, 1);
            }
            else if (vertexCount <= ushort.MaxValue)
            {
                mesh.FaceIndices = new DataBuffer<ushort>(ReadPayload(reader, checked(2 * faceCount * 3)), 1, 1);

            }
            else
            {
                mesh.FaceIndices = new DataBuffer<int>(ReadPayload(reader, checked(4 * faceCount * 3)), 1, 1);
            }

            var perMeshMaterialIndices = new int[layerCount];

            for (int m = 0; m < layerCount; m++)
            {
                perMeshMaterialIndices[m] = reader.ReadInt32();
                if (perMeshMaterialIndices[m] < -1 || perMeshMaterialIndices[m] >= matCount)
                    throw new InvalidDataException("Invalid SEModel material index.");
            }
            materialIndices.Add(perMeshMaterialIndices);
        }

        for (int m = 0; m < matCount; m++)
        {
            token?.ThrowIfCancellationRequested();
            var material = model.AddNode<Material>(reader.ReadUTF8NullTerminatedString());

            if (reader.ReadBoolean())
            {
                material.DiffuseMapName		= AssignMaterialTexture(material, "diffuse",	reader.ReadUTF8NullTerminatedString(), context.SourceDirectoryPath);
                material.NormalMapName		= AssignMaterialTexture(material, "normal",	reader.ReadUTF8NullTerminatedString(), context.SourceDirectoryPath);
                material.SpecularMapName	= AssignMaterialTexture(material, "specular",	reader.ReadUTF8NullTerminatedString(), context.SourceDirectoryPath);
            }
        }

        Material[] allMaterials	= model.GetDescendants<Material>();
        Mesh[] allMeshes		= model.GetDescendants<Mesh>();

        for (int i = 0; i < allMeshes.Length && i < materialIndices.Count; i++)
        {
            token?.ThrowIfCancellationRequested();
            int[] matIndices				= materialIndices[i];
            List<Material> meshMaterials	= new(matIndices.Length);

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
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        using var writer = new BinaryWriter(stream, Encoding.Default, true);

        SceneTranslationSelection selection	= context.GetSelection(scene);
        SceneTranslatorOptions options		= context.Options;
        string? targetDirectoryPath			= context.TargetDirectoryPath;
        var meshes							= selection.GetDescendants<Mesh>();
        var materials						= selection.GetDescendants<Material>();
        var bones							= selection.GetDescendants<SkeletonBone>();
        var boneTable						= new Dictionary<SkeletonBone, int>();
        var materialTable					= new Dictionary<Material, int>();
        SceneNode[] exportedBoneNodes		= Array.ConvertAll(bones, static bone => (SceneNode)bone);
        Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(bones, exportedBoneNodes);

        bool	hasBones	= bones.Length > 0;
        bool	hasMeshes	= meshes.Length > 0;
        bool	hasMats		= materials.Length > 0;

        byte dataPresence = 0;
        if (hasBones)	dataPresence |= 1;
        if (hasMeshes) dataPresence |= 2;
        if (hasMats)	dataPresence |= 4;

        var meshDataPresence = ComputeMeshDataPresence(meshes);

        writer.Write(MagicValue);
        writer.Write((ushort)1);
        writer.Write((ushort)0x14);
        writer.Write(dataPresence);
        writer.Write((byte)0x7);
        writer.Write(meshDataPresence);

        writer.Write(bones.Length);
        writer.Write(meshes.Length);
        writer.Write(materials.Length);

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);

        int boneIndex = 0;
        foreach (var bone in bones)
        {
            token?.ThrowIfCancellationRequested();
            writer.WriteNullTerminatedString(bone.Name);
            boneTable.Add(bone, boneIndex++);
        }

        foreach (var bone in bones)
        {
            token?.ThrowIfCancellationRequested();
            writer.Write((byte)0);
            SkeletonBone? exportedParent = parents[bone];
            writer.Write(exportedParent is null ? -1 : boneTable[exportedParent]);

            writer.WriteStruct(bone.GetActiveWorldPosition());
            writer.WriteStruct(bone.GetActiveWorldRotation());
            SceneNode? parent	= exportedParent;
            Matrix4x4 matrix	= bone.GetActiveWorldMatrix();
            if (parent is not null)
            {
                if (!Matrix4x4.Invert(parent.GetActiveWorldMatrix(), out Matrix4x4 inverse))
                    throw new InvalidDataException("Singular SEModel parent transform.");
                matrix *= inverse;
            }
            SkeletonExportTransforms.DecomposeMatrix(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position);
            writer.WriteStruct(position);
            writer.WriteStruct(rotation);
            writer.WriteStruct(scale);

        }

        for (int i = 0; i < materials.Length; i++)
            materialTable[materials[i]] = i;

        bool	hasUVs		= (meshDataPresence & (1 << 0)) != 0;
        bool	hasNormals	= (meshDataPresence & (1 << 1)) != 0;
        bool	hasColours	= (meshDataPresence & (1 << 2)) != 0;
        bool	hasWeights	= (meshDataPresence & (1 << 3)) != 0;

        foreach (var mesh in meshes)
        {
            token?.ThrowIfCancellationRequested();
            if (mesh.Positions is null)
                throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' has no position data.");
            if (mesh.FaceIndices is null)
                throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' has no face index data.");

            int		vertexCount	= mesh.Positions.ElementCount;
            int		faceCount	= mesh.FaceIndices.ElementCount / 3;
            int		layerCount	= Math.Max(mesh.UVLayers?.ValueCount ?? 0, 1);
            Skin?	skin		= mesh.Skin;
            int		influences	= skin?.BoneIndices.ValueCount ?? 0;

            if (layerCount > byte.MaxValue || influences > byte.MaxValue || mesh.IndexCount % 3 != 0)
                throw new InvalidDataException("SEModel layer, influence, or triangle count is out of range.");

            int[] globalBoneIndexTable			= skin is not null ? GetExportBoneIndices(mesh, skin, boneTable) : [];
            Matrix4x4[] skinTransforms			= [];
            Matrix4x4[] skinNormalTransforms	= [];
            if (!options.WriteRawVertices && skin is not null)
            {
                skinTransforms = new Matrix4x4[skin.Bones.Count];
                mesh.CopySkinTransforms(skinTransforms);
                skinNormalTransforms = new Matrix4x4[skinTransforms.Length];
                for (int i = 0; i < skinTransforms.Length; i++)
                    skinNormalTransforms[i] = Matrix4x4.Invert(skinTransforms[i], out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : skinTransforms[i];
            }

            writer.Write((byte)0);
            writer.Write((byte)layerCount);
            writer.Write((byte)influences);
            writer.Write(vertexCount);
            writer.Write(faceCount);

            for (int v = 0; v < vertexCount; v++)
                writer.WriteStruct(options.WriteRawVertices ? mesh.GetVertexPosition(v, raw: true) : mesh.GetVertexPosition(v, skinTransforms));

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

                for (int v = 0; v < vertexCount; v++)
                {
                    for (int l = 0; l < layerCount; l++)
                    {
                        writer.Write(0f);
                        writer.Write(0f);
                    }
                }
            }

            if (mesh.Normals is not null)
            {
                for (int v = 0; v < vertexCount; v++)
                    writer.WriteStruct(options.WriteRawVertices ? mesh.GetVertexNormal(v, raw: true) : mesh.GetVertexNormal(v, skinTransforms, skinNormalTransforms));
            }
            else if (hasNormals)
            {

                for (int v = 0; v < vertexCount; v++)
                    writer.WriteStruct(Vector3.Zero);
            }

            if (mesh.ColorLayers is not null)
            {
                float colorScale = mesh.ColorLayers is DataBuffer<byte> ? 1f : byte.MaxValue;
                for (int v = 0; v < vertexCount; v++)
                {
                    Vector4 color = Vector4.Clamp(mesh.ColorLayers.GetVector4(v, 0, 1f) * colorScale, Vector4.Zero, new Vector4(byte.MaxValue));
                    writer.Write((byte)color.X);
                    writer.Write((byte)color.Y);
                    writer.Write((byte)color.Z);
                    writer.Write((byte)color.W);
                }
            }
            else if (hasColours)
            {

                for (int v = 0; v < vertexCount; v++)
                {
                    writer.Write((byte)255);
                    writer.Write((byte)255);
                    writer.Write((byte)255);
                    writer.Write((byte)255);
                }
            }

            if (skin is not null)
            {
                for (int v = 0; v < vertexCount; v++)
                {
                    for (int j = 0; j < influences; j++)
                    {
                        int boneIdx = skin.GetBoneIndex(v, j);

                        if (bones.Length <= byte.MaxValue)
                            writer.Write((byte)globalBoneIndexTable[boneIdx]);
                        else if (bones.Length <= ushort.MaxValue)
                            writer.Write((ushort)globalBoneIndexTable[boneIdx]);
                        else
                            writer.Write(globalBoneIndexTable[boneIdx]);

                        writer.Write(skin.BoneWeights.Get<float>(v, j, 0));
                    }
                }
            }

            for (int f = 0; f < mesh.FaceIndices.ElementCount; f++)
            {
                if ((uint)mesh.FaceIndices.Get<int>(f, 0, 0) >= (uint)vertexCount)
                    throw new InvalidDataException("Invalid SEModel face index.");
                if (vertexCount <= byte.MaxValue)
                    writer.Write(mesh.FaceIndices.Get<byte>(f, 0, 0));
                else if (vertexCount <= ushort.MaxValue)
                    writer.Write(mesh.FaceIndices.Get<ushort>(f, 0, 0));
                else
                    writer.Write(mesh.FaceIndices.Get<int>(f, 0, 0));
            }

            int[] meshMaterialIndices = GetExportMaterialIndices(mesh, layerCount, materialTable);

            for (int l = 0; l < layerCount; l++)
                writer.Write(meshMaterialIndices[l]);
        }

        foreach (var material in materials)
        {
            token?.ThrowIfCancellationRequested();
            writer.WriteNullTerminatedString(material.Name.Replace('/', '_').Replace('\\', '_').Replace('.', '_'));

            string?	diffuseMapName	= ResolveMaterialTextureName(material, material.DiffuseMapName,		targetDirectoryPath);
            string?	normalMapName	= ResolveMaterialTextureName(material, material.NormalMapName,		targetDirectoryPath);
            string?	specularMapName	= ResolveMaterialTextureName(material, material.SpecularMapName, targetDirectoryPath);

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

    private static byte[] ReadPayload(BinaryReader reader, int size)
    {
        if (size < 0 || size > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Truncated SEModel payload.");
        byte[] data = new byte[size];
        reader.BaseStream.ReadExactly(data);
        return data;
    }

    private static byte ComputeMeshDataPresence(Mesh[] meshes)
    {
        byte flags = 0;
        foreach (var mesh in meshes)
        {
            if (mesh.UVLayers is not null)		flags |= 1;
            if (mesh.Normals is not null)		flags |= 2;
            if (mesh.ColorLayers is not null) flags |= 4;
            if (mesh.Skin is not null)			flags |= 8;
        }
        return flags;
    }

    private static string? AssignMaterialTexture(Material material, string slot, string? textureReference, string? sourceDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(textureReference))
            return null;

        string? resolvedPath = FilePathResolver.ResolveReferencePath(textureReference, sourceDirectoryPath);

        if (material.TryGetTexture(slot, out Texture? existing))
        {
            existing.FilePath = textureReference;
            existing.ResolvedFilePath = resolvedPath;
            return slot;
        }

        var texture = material.AddNode(new Texture(textureReference)
        {
            ResolvedFilePath = resolvedPath,
        });
        material.Connect(slot, texture);
        return slot;
    }

    private static string? ResolveMaterialTextureName(Material material, string? slotKey, string? targetDirectoryPath)
    {
        if (slotKey is null)
            return null;

        if (material.TryGetTexture(slotKey, out Texture? texture))
            return texture.GetPortableFilePath(targetDirectoryPath);

        return null;
    }

    private static int[] GetExportBoneIndices(Mesh mesh, Skin skin, Dictionary<SkeletonBone, int> boneTable)
    {
        List<string> missingBones = [];
        foreach (SkeletonBone skinnedBone in skin.Bones)
        {
            if (!boneTable.ContainsKey(skinnedBone))
                missingBones.Add(skinnedBone.Name);
        }

        if (missingBones.Count > 0)
        {
            throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' references skinned bones that are not included in the export selection: {string.Join(", ", missingBones)}.");
        }

        return skin.GetBoneTableIndices(boneTable);
    }

    private static int[] GetExportMaterialIndices(Mesh mesh, int layerCount, IReadOnlyDictionary<Material, int> materialTable)
    {
        if (layerCount <= 0)
            return [];

        int[] indices = new int[layerCount];

        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            indices[layerIndex] = ResolveExportMaterialIndex(mesh, layerIndex, materialTable);

        return indices;
    }

    private static int ResolveExportMaterialIndex(Mesh mesh, int layerIndex, IReadOnlyDictionary<Material, int> materialTable)
    {
        if (mesh.Materials is null || mesh.Materials.Count == 0)
            return -1;

        if (layerIndex < mesh.Materials.Count)
        {
            Material exactMaterial = mesh.Materials[layerIndex];
            if (materialTable.TryGetValue(exactMaterial, out int exactIndex))
                return exactIndex;

            throw new InvalidDataException($"Cannot write SEModel: mesh '{mesh.Name}' references material '{exactMaterial.Name}' for layer {layerIndex} that is not included in the export selection.");
        }

        foreach (Material material in mesh.Materials)
        {
            if (materialTable.TryGetValue(material, out int fallbackIndex))
                return fallbackIndex;
        }

        return -1;
    }
}
