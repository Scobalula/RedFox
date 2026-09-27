using System.Numerics;
using CallOfFile;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.IwEngine;

internal static class XModelWriter
{
    private static readonly int[] ClockwiseCorners = [0, 2, 1];

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

        writer.WriteSection("MODEL");
        writer.WriteUShort("VERSION", 6);
        writer.WriteUShort("NUMBONES", checked((ushort)Math.Max(bones.Length, 1)));

        if (bones.Length == 0)
            writer.WriteBoneInfo("BONE", 0, -1, "tag_origin");
        else
            for (int i = 0; i < bones.Length; i++)
                writer.WriteBoneInfo("BONE", i, FindParentIndex(bones[i], boneIndices), bones[i].Name);

        if (bones.Length == 0)
        {
            writer.WriteUShort("BONE", 0);
            writer.WriteVector3("OFFSET", Vector3.Zero);
            writer.WriteVector3("SCALE", Vector3.One);
            writer.WriteVector316Bit("X", Vector3.UnitX);
            writer.WriteVector316Bit("Y", Vector3.UnitY);
            writer.WriteVector316Bit("Z", Vector3.UnitZ);
        }

        for (int i = 0; i < bones.Length; i++)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(bones[i].GetActiveWorldRotation());
            Vector3 scale = bones[i].BindTransform.Scale ?? Vector3.One;

            writer.WriteUShort("BONE", checked((ushort)i));
            writer.WriteVector3("OFFSET", bones[i].GetActiveWorldPosition());
            writer.WriteVector3("SCALE", scale);
            writer.WriteVector316Bit("X", new Vector3(rotation.M11, rotation.M12, rotation.M13));
            writer.WriteVector316Bit("Y", new Vector3(rotation.M21, rotation.M22, rotation.M23));
            writer.WriteVector316Bit("Z", new Vector3(rotation.M31, rotation.M32, rotation.M33));
        }

        bool wideVertices = vertexCount > ushort.MaxValue;

        if (wideVertices)
            writer.WriteUInt("NUMVERTS32", checked((uint)vertexCount));
        else
            writer.WriteUShort("NUMVERTS", checked((ushort)vertexCount));

        Dictionary<Mesh, (Matrix4x4[] Skin, Matrix4x4[] Normal)> skinTransformsByMesh = [];
        List<(int BoneIndex, float Weight)> weights = [];
        foreach ((Mesh mesh, int baseVertex) in meshVertices)
        {
            int[] skinBoneIndices = GetSkinBoneIndices(mesh, boneIndices);
            Matrix4x4[] skinTransforms = [];
            Matrix4x4[] normalTransforms = [];
            if (!context.Options.WriteRawVertices && mesh.Skin is { } skin)
            {
                skinTransforms = new Matrix4x4[skin.Bones.Count];
                mesh.CopySkinTransforms(skinTransforms);
                normalTransforms = new Matrix4x4[skinTransforms.Length];
                for (int i = 0; i < skinTransforms.Length; i++)
                    normalTransforms[i] = Matrix4x4.Invert(skinTransforms[i], out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : skinTransforms[i];
            }
            skinTransformsByMesh[mesh] = (skinTransforms, normalTransforms);

            for (int vertexIndex = 0; vertexIndex < mesh.Positions!.ElementCount; vertexIndex++)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                weights.Clear();

                WriteVertexIndex(writer, wideVertices, baseVertex + vertexIndex);
                Vector3 position = context.Options.WriteRawVertices ? mesh.GetVertexPosition(vertexIndex, raw: true) : mesh.GetVertexPosition(vertexIndex, skinTransforms);
                writer.WriteVector3("OFFSET", position);
                GetVertexWeights(mesh, skinBoneIndices, vertexIndex, weights);
                writer.WriteUShort("BONES", checked((ushort)weights.Count));

                foreach ((int boneIndex, float weight) in weights)
                    writer.WriteBoneWeight("BONE", boneIndex, weight);
            }
        }

        writer.WriteUInt("NUMFACES", checked((uint)faceCount));
        for (int objectIndex = 0; objectIndex < meshVertices.Count; objectIndex++)
        {
            (Mesh mesh, int baseVertex) = meshVertices[objectIndex];
            (Matrix4x4[] skinTransforms, Matrix4x4[] normalTransforms) = skinTransformsByMesh[mesh];
            int materialIndex = mesh.Materials is { Count: > 0 } && materialIndices.TryGetValue(mesh.Materials[0], out int mappedMaterialIndex) ? mappedMaterialIndex : 0;
            Vector2[] uvValues = new Vector2[Math.Max(mesh.UVLayers?.ValueCount ?? 0, 1)];
            for (int faceIndex = 0; faceIndex < mesh.FaceIndices!.ElementCount; faceIndex += 3)
            {
                if (objectIndex <= byte.MaxValue && materialIndex <= byte.MaxValue)
                    writer.WriteTri("TRI", objectIndex, materialIndex);
                else
                    writer.WriteTri16("TRI16", objectIndex, materialIndex);
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertexIndex = mesh.FaceIndices.Get<int>(faceIndex + ClockwiseCorners[corner], 0, 0);
                    if ((uint)vertexIndex >= (uint)mesh.Positions!.ElementCount)
                        throw new InvalidDataException($"Mesh '{mesh.Name}' face index {vertexIndex} is outside its vertex range.");
                    WriteVertexIndex(writer, wideVertices, baseVertex + vertexIndex);
                    Vector3 normal = mesh.Normals is null ? Vector3.UnitZ : context.Options.WriteRawVertices ? mesh.GetVertexNormal(vertexIndex, raw: true) : mesh.GetVertexNormal(vertexIndex, skinTransforms, normalTransforms);
                    writer.WriteVector316Bit("NORMAL", normal);
                    writer.WriteVector48Bit("COLOR", mesh.ColorLayers is null ? Vector4.One : mesh.ColorLayers.GetVector4(vertexIndex, 0));
                    for (int layer = 0; layer < uvValues.Length; layer++)
                        uvValues[layer] = mesh.UVLayers is null ? Vector2.Zero : mesh.UVLayers.GetVector2(vertexIndex, layer);
                    writer.WriteUVSet("UV", uvValues);
                }
            }
        }

        writer.WriteUShort("NUMOBJECTS", checked((ushort)meshes.Length));
        for (int i = 0; i < meshes.Length; i++)
            writer.WriteUShortString("OBJECT", checked((ushort)i), meshes[i].Name);

        writer.WriteUShort("NUMMATERIALS", checked((ushort)materialList.Count));
        for (int i = 0; i < materialList.Count; i++)
        {
            Material material = materialList[i];
            writer.WriteUShortStringX3("MATERIAL", checked((ushort)i), material.Name, "Lambert", GetDiffuseTextureName(material));
            writer.WriteVector48Bit("COLOR", material.DiffuseColor ?? Vector4.One);
            writer.WriteVector4("TRANSPARENCY", Vector4.Zero);
            writer.WriteVector4("AMBIENTCOLOR", Vector4.Zero);
            writer.WriteVector4("INCANDESCENCE", material.EmissiveColor ?? Vector4.Zero);
            writer.WriteVector2("COEFFS", new Vector2(0.8f, 0f));
            writer.WriteVector2("GLOW", Vector2.Zero);
            writer.WriteVector2("REFRACTIVE", new Vector2(6f, 1f));
            writer.WriteVector4("SPECULARCOLOR", material.SpecularColor ?? Vector4.Zero);
            writer.WriteVector4("REFLECTIVECOLOR", Vector4.Zero);
            writer.WriteVector2("REFLECTIVE", Vector2.Zero);
            writer.WriteVector2("BLINN", Vector2.Zero);
            writer.WriteFloat("PHONG", material.Shininess ?? 0f);
        }
    }

    private static void WriteVertexIndex(TokenWriter writer, bool wide, int index)
    {
        if (wide)
            writer.WriteUInt("VERT32", checked((uint)index));
        else
            writer.WriteUShort("VERT", checked((ushort)index));
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
        if (mesh.Skin is not { } skin)
            return [];
        int[] result = new int[skin.Bones.Count];
        for (int i = 0; i < result.Length; i++)
        {
            if (!boneIndices.TryGetValue(skin.Bones[i], out result[i]))
                throw new InvalidDataException($"Mesh '{mesh.Name}' references bone '{skin.Bones[i].Name}' outside the XModel skeleton.");
        }
        return result;
    }

    private static void GetVertexWeights(Mesh mesh, int[] skinBoneIndices, int vertexIndex, List<(int BoneIndex, float Weight)> weights)
    {
        if (mesh.Skin is { } skin && skinBoneIndices.Length > 0)
        {
            for (int influence = 0; influence < skin.BoneIndices.ValueCount; influence++)
            {
                float weight = skin.BoneWeights.Get<float>(vertexIndex, influence, 0);

                if (!float.IsFinite(weight) || weight <= 0f)
                    continue;

                int localBoneIndex = skin.BoneIndices.Get<int>(vertexIndex, influence, 0);

                if ((uint)localBoneIndex >= (uint)skinBoneIndices.Length)
                    throw new InvalidDataException($"Mesh '{mesh.Name}' vertex {vertexIndex} references skin bone {localBoneIndex} outside its skin table.");

                weights.Add((skinBoneIndices[localBoneIndex], weight));
            }
        }

        if (weights.Count == 0)
        {
            weights.Add((0, 1f));
            return;
        }

        double totalWeight = weights.Sum(static weight => weight.Weight);

        for (int i = 0; i < weights.Count; i++)
            weights[i] = (weights[i].BoneIndex, (float)(weights[i].Weight / totalWeight));
    }

    private static string GetDiffuseTextureName(Material material)
    {
        if (material.DiffuseMapName is not null && material.TryGetTexture(material.DiffuseMapName, out Texture? texture))
            return texture.FilePath ?? texture.Name;
        return string.Empty;
    }

}
