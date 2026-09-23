using System.Numerics;
using CallOfFile;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.XAsset;

internal static class XModelWriter
{
    public static void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? cancellationToken)
    {
        SceneTranslationSelection selection = context.GetSelection(scene);

        Mesh[] meshes = selection.GetDescendants<Mesh>();

        if (meshes.Length == 0)
            throw new InvalidDataException("The scene selection does not contain any meshes to write as XModel.");

        SkeletonBone[] bones = selection.GetDescendants<SkeletonBone>();

        if (bones.Length > ushort.MaxValue)
            throw new NotSupportedException("XModel does not support more than 65535 bones.");

        Dictionary<SkeletonBone, int> boneIndices = [];

        for (int i = 0; i < bones.Length; i++)
            boneIndices.Add(bones[i], i);

        List<Material> materialList = [];

        foreach (Mesh mesh in meshes)
        {
            if (mesh.Materials is null)
                continue;

            foreach (Material material in mesh.Materials)
            {
                if (!materialList.Contains(material))
                    materialList.Add(material);
            }
        }

        if (materialList.Count == 0)
            materialList.Add(new Material("default"));
        if (materialList.Count > ushort.MaxValue || meshes.Length > ushort.MaxValue)
            throw new NotSupportedException("XModel does not support more than 65535 materials or objects.");

        Dictionary<Material, int> materialIndices = [];

        for (int i = 0; i < materialList.Count; i++)
            materialIndices.Add(materialList[i], i);

        List<(Mesh Mesh, int BaseVertex)> meshVertices = [];

        int vertexCount = 0;
        int faceCount = 0;

        foreach (Mesh mesh in meshes)
        {
            if (mesh.Positions is null || mesh.FaceIndices is null)
                throw new InvalidDataException($"Mesh '{mesh.Name}' must contain positions and triangle indices to write as XModel.");
            if (mesh.FaceIndices.ElementCount % 3 != 0)
                throw new InvalidDataException($"Mesh '{mesh.Name}' has a non-triangular index count.");

            meshVertices.Add((mesh, vertexCount));

            vertexCount = checked(vertexCount + mesh.Positions.ElementCount);
            faceCount = checked(faceCount + mesh.FaceIndices.ElementCount / 3);
        }

        bool binary = string.Equals(Path.GetExtension(context.TargetFilePath), ".xmodel_bin", StringComparison.OrdinalIgnoreCase);

        using XAssetWriter output = new(stream, binary);
        TokenWriter writer = output.Writer;

        writer.WriteSection("MODEL", 0x46C8);
        writer.WriteUShort("VERSION", 0x24D1, 6);
        writer.WriteUShort("NUMBONES", 0x76BA, checked((ushort)Math.Max(bones.Length, 1)));

        if (bones.Length == 0)
            writer.WriteBoneInfo("BONE", 0xF099, 0, -1, "tag_origin");
        else
            for (int i = 0; i < bones.Length; i++)
                writer.WriteBoneInfo("BONE", 0xF099, i, FindParentIndex(bones[i], boneIndices), bones[i].Name);

        if (bones.Length == 0)
        {
            writer.WriteUShort("BONE", 0xDD9A, 0);
            writer.WriteVector3("OFFSET", 0x9383, Vector3.Zero);
            writer.WriteVector3("SCALE", 0x1C56, Vector3.One);
            writer.WriteVector316Bit("X", 0xDCFD, Vector3.UnitX);
            writer.WriteVector316Bit("Y", 0xCCDC, Vector3.UnitY);
            writer.WriteVector316Bit("Z", 0xFCBF, Vector3.UnitZ);
        }

        for (int i = 0; i < bones.Length; i++)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(bones[i].GetActiveWorldRotation());
            Vector3 scale = bones[i].BindTransform.Scale ?? Vector3.One;

            writer.WriteUShort("BONE", 0xDD9A, checked((ushort)i));
            writer.WriteVector3("OFFSET", 0x9383, bones[i].GetActiveWorldPosition());
            writer.WriteVector3("SCALE", 0x1C56, scale);
            writer.WriteVector316Bit("X", 0xDCFD, new Vector3(rotation.M11, rotation.M12, rotation.M13));
            writer.WriteVector316Bit("Y", 0xCCDC, new Vector3(rotation.M21, rotation.M22, rotation.M23));
            writer.WriteVector316Bit("Z", 0xFCBF, new Vector3(rotation.M31, rotation.M32, rotation.M33));
        }

        bool wideVertices = vertexCount > ushort.MaxValue;

        if (wideVertices)
            writer.WriteUInt("NUMVERTS32", 0x2AEC, checked((uint)vertexCount));
        else
            writer.WriteUShort("NUMVERTS", 0x950D, checked((ushort)vertexCount));

        foreach ((Mesh mesh, int baseVertex) in meshVertices)
        {
            int[] skinBoneIndices = GetSkinBoneIndices(mesh, boneIndices);

            for (int vertexIndex = 0; vertexIndex < mesh.Positions!.ElementCount; vertexIndex++)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                WriteVertexIndex(writer, wideVertices, baseVertex + vertexIndex);
                writer.WriteVector3("OFFSET", 0x9383, mesh.GetVertexPosition(vertexIndex, context.Options.WriteRawVertices));
                List<(int BoneIndex, float Weight)> weights = GetVertexWeights(mesh, skinBoneIndices, vertexIndex);
                writer.WriteUShort("BONES", 0xEA46, checked((ushort)weights.Count));

                foreach ((int boneIndex, float weight) in weights)
                    writer.WriteBoneWeight("BONE", 0xF1AB, boneIndex, weight);
            }
        }

        writer.WriteUInt("NUMFACES", 0xBE92, checked((uint)faceCount));
        for (int objectIndex = 0; objectIndex < meshVertices.Count; objectIndex++)
        {
            (Mesh mesh, int baseVertex) = meshVertices[objectIndex];
            int materialIndex = mesh.Materials is { Count: > 0 } && materialIndices.TryGetValue(mesh.Materials[0], out int mappedMaterialIndex) ? mappedMaterialIndex : 0;
            for (int faceIndex = 0; faceIndex < mesh.FaceIndices!.ElementCount; faceIndex += 3)
            {
                if (objectIndex <= byte.MaxValue && materialIndex <= byte.MaxValue)
                    writer.WriteTri("TRI", 0x562F, objectIndex, materialIndex);
                else
                    writer.WriteTri16("TRI16", 0x6711, objectIndex, materialIndex);
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertexIndex = mesh.FaceIndices.Get<int>(faceIndex + corner, 0, 0);
                    if ((uint)vertexIndex >= (uint)mesh.Positions!.ElementCount)
                        throw new InvalidDataException($"Mesh '{mesh.Name}' face index {vertexIndex} is outside its vertex range.");
                    WriteVertexIndex(writer, wideVertices, baseVertex + vertexIndex);
                    writer.WriteVector316Bit("NORMAL", 0x89EC, mesh.Normals is null ? Vector3.UnitZ : mesh.GetVertexNormal(vertexIndex, context.Options.WriteRawVertices));
                    writer.WriteVector48Bit("COLOR", 0x6DD8, mesh.ColorLayers is null ? Vector4.One : mesh.ColorLayers.GetVector4(vertexIndex, 0));
                    int uvLayerCount = mesh.UVLayers?.ValueCount ?? 0;
                    List<Vector2> uvValues = new(Math.Max(uvLayerCount, 1));
                    for (int layer = 0; layer < Math.Max(uvLayerCount, 1); layer++)
                        uvValues.Add(mesh.UVLayers is null ? Vector2.Zero : mesh.UVLayers.GetVector2(vertexIndex, layer));
                    writer.WriteUVSet("UV", 0x1AD4, uvLayerCount, uvValues);
                }
            }
        }

        writer.WriteUShort("NUMOBJECTS", 0x62AF, checked((ushort)meshes.Length));
        for (int i = 0; i < meshes.Length; i++)
            writer.WriteUShortString("OBJECT", 0x87D4, checked((ushort)i), meshes[i].Name);

        writer.WriteUShort("NUMMATERIALS", 0xA1B2, checked((ushort)materialList.Count));
        for (int i = 0; i < materialList.Count; i++)
        {
            Material material = materialList[i];
            writer.WriteUShortStringX3("MATERIAL", 0xA700, checked((ushort)i), material.Name, "Lambert", GetDiffuseTextureName(material));
            writer.WriteVector48Bit("COLOR", 0x6DD8, material.DiffuseColor ?? Vector4.One);
            writer.WriteVector4("TRANSPARENCY", 0x6DAB, Vector4.Zero);
            writer.WriteVector4("AMBIENTCOLOR", 0x37FF, Vector4.Zero);
            writer.WriteVector4("INCANDESCENCE", 0x4265, material.EmissiveColor ?? Vector4.Zero);
            writer.WriteVector2("COEFFS", 0xC835, new Vector2(0.8f, 0f));
            writer.WriteVector2("GLOW", 0xFE0C, Vector2.Zero);
            writer.WriteVector2("REFRACTIVE", 0x7E24, new Vector2(6f, 1f));
            writer.WriteVector4("SPECULARCOLOR", 0x317C, material.SpecularColor ?? Vector4.Zero);
            writer.WriteVector4("REFLECTIVECOLOR", 0xE593, Vector4.Zero);
            writer.WriteVector2("REFLECTIVE", 0x7D76, Vector2.Zero);
            writer.WriteVector2("BLINN", 0x83C7, Vector2.Zero);
            writer.WriteFloat("PHONG", 0x5CD2, material.Shininess ?? 0f);
        }
    }

    private static void WriteVertexIndex(TokenWriter writer, bool wide, int index)
    {
        if (wide)
            writer.WriteUInt("VERT32", 0xB097, checked((uint)index));
        else
            writer.WriteUShort("VERT", 0x8F03, checked((ushort)index));
    }

    private static int FindParentIndex(SkeletonBone bone, IReadOnlyDictionary<SkeletonBone, int> boneIndices)
    {
        SceneNode? parent = bone.Parent;
        while (parent is not null)
        {
            if (parent is SkeletonBone parentBone && boneIndices.TryGetValue(parentBone, out int index))
                return index;
            parent = parent.Parent;
        }
        return -1;
    }

    private static int[] GetSkinBoneIndices(Mesh mesh, IReadOnlyDictionary<SkeletonBone, int> boneIndices)
    {
        if (mesh.SkinnedBones is null)
            return [];
        int[] result = new int[mesh.SkinnedBones.Count];
        for (int i = 0; i < result.Length; i++)
        {
            if (!boneIndices.TryGetValue(mesh.SkinnedBones[i], out result[i]))
                throw new InvalidDataException($"Mesh '{mesh.Name}' references bone '{mesh.SkinnedBones[i].Name}' outside the XModel skeleton.");
        }
        return result;
    }

    private static List<(int BoneIndex, float Weight)> GetVertexWeights(Mesh mesh, int[] skinBoneIndices, int vertexIndex)
    {
        List<(int BoneIndex, float Weight)> weights = [];

        if (mesh.BoneIndices is not null && mesh.BoneWeights is not null && skinBoneIndices.Length > 0)
        {
            for (int influence = 0; influence < mesh.BoneIndices.ValueCount; influence++)
            {
                float weight = mesh.BoneWeights.Get<float>(vertexIndex, influence, 0);

                if (!float.IsFinite(weight) || weight <= 0f)
                    continue;

                int localBoneIndex = mesh.BoneIndices.Get<int>(vertexIndex, influence, 0);

                if ((uint)localBoneIndex >= (uint)skinBoneIndices.Length)
                    throw new InvalidDataException($"Mesh '{mesh.Name}' vertex {vertexIndex} references skin bone {localBoneIndex} outside its skin table.");

                weights.Add((skinBoneIndices[localBoneIndex], weight));
            }
        }

        if (weights.Count == 0)
        {
            weights.Add((0, 1f));
            return weights;
        }

        double totalWeight = weights.Sum(static weight => weight.Weight);

        for (int i = 0; i < weights.Count; i++)
            weights[i] = (weights[i].BoneIndex, (float)(weights[i].Weight / totalWeight));

        return weights;
    }

    private static string GetDiffuseTextureName(Material material)
    {
        if (material.DiffuseMapName is not null && material.TryGetTexture(material.DiffuseMapName, out Texture? texture))
            return texture.FilePath ?? texture.Name;
        return string.Empty;
    }

}
