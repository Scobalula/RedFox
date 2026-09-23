using System.Numerics;
using CallOfFile;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.XAsset;

internal static class XModelReader
{
    public static void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? cancellationToken)
    {
        using XAssetTokenStream tokens = new(stream);
        tokens.Expect("MODEL");
        tokens.Expect("VERSION");

        int boneCount = tokens.MoveTo("NUMBONES").GetInt32();
        SkeletonBone[] bones = new SkeletonBone[boneCount];
        int[] parentIndices = new int[boneCount];
        for (int i = 0; i < boneCount; i++)
        {
            TokenData boneData = i == 0 ? tokens.MoveTo("BONE") : tokens.Expect("BONE");
            if (boneData is not TokenDataBoneInfo bone)
                throw new InvalidDataException("Expected an XModel bone definition.");
            int boneIndex = bone.BoneIndex;
            ValidateIndex(boneIndex, boneCount, "bone");
            bones[boneIndex] = new SkeletonBone(bone.Name);
            parentIndices[boneIndex] = bone.BoneParentIndex;
        }

        while (tokens.Peek() is TokenDataUInt boneToken && boneToken.Token.Name == "BONE")
        {
            cancellationToken?.ThrowIfCancellationRequested();
            tokens.Read();
            int boneIndex = checked((int)boneToken.Value);
            ValidateIndex(boneIndex, boneCount, "bone transform");
            Vector3 offset = Vector3.Zero;
            Vector3 scale = Vector3.One;
            Vector3 x = Vector3.UnitX;
            Vector3 y = Vector3.UnitY;
            Vector3 z = Vector3.UnitZ;
            Quaternion? quaternion = null;
            while (tokens.Peek() is { } transform && transform.Token.Name is not "BONE" and not "NUMVERTS" and not "NUMVERTS32")
            {
                tokens.Read();
                switch (transform.Token.Name)
                {
                    case "OFFSET": offset = transform.GetVector3(); break;
                    case "SCALE": scale = transform.GetVector3(); break;
                    case "X": x = transform.GetVector3(); break;
                    case "Y": y = transform.GetVector3(); break;
                    case "Z": z = transform.GetVector3(); break;
                    case "QUATERNION":
                        Vector4 value = transform.GetVector4();
                        quaternion = Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W));
                        break;
                }
            }

            bones[boneIndex].BindTransform.WorldPosition = offset;
            bones[boneIndex].BindTransform.WorldRotation = quaternion ?? XAssetTransform.CreateRotation(x, y, z);
            bones[boneIndex].BindTransform.Scale = scale;
        }

        SkeletonBone skeleton = scene.RootNode.AddNode(new SkeletonBone($"{context.Name}_Skeleton"));
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] is null)
                throw new InvalidDataException($"XModel bone {i} was not defined.");
            int parentIndex = parentIndices[i];
            if (parentIndex >= 0)
            {
                ValidateIndex(parentIndex, boneCount, "parent bone");
                bones[i].MoveTo(bones[parentIndex], ReparentTransformMode.PreserveExisting);
            }
            else
            {
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            }
        }

        int vertexCount = tokens.MoveToEither("NUMVERTS", "NUMVERTS32").GetInt32();
        Vector3[] sourcePositions = new Vector3[vertexCount];
        List<(int BoneIndex, float Weight)>[] sourceWeights = new List<(int BoneIndex, float Weight)>[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            int vertexIndex = tokens.ExpectEither("VERT", "VERT32").GetInt32();
            ValidateIndex(vertexIndex, vertexCount, "vertex");
            sourcePositions[vertexIndex] = tokens.Expect("OFFSET").GetVector3();
            int influenceCount = tokens.Expect("BONES").GetInt32();
            List<(int BoneIndex, float Weight)> influences = new(influenceCount);
            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                if (tokens.Expect("BONE") is not TokenDataBoneWeight influence)
                    throw new InvalidDataException("Expected an XModel vertex bone weight.");
                ValidateIndex(influence.BoneIndex, boneCount, "vertex bone");
                influences.Add((influence.BoneIndex, influence.BoneWeight));
            }
            sourceWeights[vertexIndex] = influences;
        }

        int faceCount = tokens.MoveTo("NUMFACES").GetInt32();
        Dictionary<(int ObjectIndex, int MaterialIndex), (List<Vector3> Positions, List<Vector3> Normals, List<Vector4> Colors, List<Vector2[]> UVs, List<List<(int BoneIndex, float Weight)>> Weights)> groups = [];
        for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            if (tokens.ExpectEither("TRI", "TRI16") is not TokenDataTri triangle)
                throw new InvalidDataException("Expected an XModel triangle.");
            (int ObjectIndex, int MaterialIndex) key = (triangle.ObjectIndex, triangle.MaterialIndex);
            if (!groups.TryGetValue(key, out var group))
            {
                group = ([], [], [], [], []);
                groups.Add(key, group);
            }

            for (int corner = 0; corner < 3; corner++)
            {
                int sourceVertexIndex = tokens.ExpectEither("VERT", "VERT32").GetInt32();
                ValidateIndex(sourceVertexIndex, vertexCount, "face vertex");
                group.Positions.Add(sourcePositions[sourceVertexIndex]);
                group.Normals.Add(tokens.Expect("NORMAL").GetVector3());
                group.Colors.Add(tokens.Expect("COLOR").GetVector4());
                if (tokens.Expect("UV") is not TokenDataUVSet uv)
                    throw new InvalidDataException("Expected an XModel UV set.");
                group.UVs.Add([.. uv.UVs]);
                group.Weights.Add(sourceWeights[sourceVertexIndex] ?? []);
            }
        }

        int objectCount = tokens.MoveTo("NUMOBJECTS").GetInt32();
        string[] objectNames = new string[objectCount];
        for (int i = 0; i < objectCount; i++)
        {
            if (tokens.Expect("OBJECT") is not TokenDataUIntString definition)
                throw new InvalidDataException("Expected an XModel object definition.");
            int objectIndex = checked((int)definition.IntegerValue);
            ValidateIndex(objectIndex, objectCount, "object");
            objectNames[objectIndex] = definition.StringValue;
        }

        int materialCount = tokens.MoveTo("NUMMATERIALS").GetInt32();
        Material[] materials = new Material[materialCount];
        for (int i = 0; i < materialCount; i++)
        {
            if (tokens.Expect("MATERIAL") is not TokenDataUIntStringX3 definition)
                throw new InvalidDataException("Expected an XModel material definition.");
            int materialIndex = definition.IntegerValue;
            ValidateIndex(materialIndex, materialCount, "material");
            Material material = new(definition.StringValue1);
            string textureName = definition.StringValue3;
            if (!string.IsNullOrWhiteSpace(textureName))
            {
                Texture texture = material.AddNode(new Texture(textureName)
                {
                    FilePath = textureName,
                    ResolvedFilePath = ResolveTexturePath(textureName, context.SourceDirectoryPath),
                });
                material.Connect("diffuse", texture);
                material.DiffuseMapName = "diffuse";
            }

            while (tokens.Peek() is { } property && property.Token.Name != "MATERIAL")
            {
                tokens.Read();
                switch (property.Token.Name)
                {
                    case "COLOR": material.DiffuseColor = property.GetVector4(); break;
                    case "SPECULARCOLOR": material.SpecularColor = property.GetVector4(); break;
                    case "INCANDESCENCE": material.EmissiveColor = property.GetVector4(); break;
                    case "PHONG": material.Shininess = property.GetSingle(); break;
                }
            }
            materials[materialIndex] = material;
        }

        MeshGroup model = scene.RootNode.AddNode<MeshGroup>(context.Name);
        foreach (Material material in materials)
            material.MoveTo(model, ReparentTransformMode.PreserveExisting);

        foreach (((int objectIndex, int materialIndex), var group) in groups)
        {
            string objectName = (uint)objectIndex < (uint)objectNames.Length && !string.IsNullOrWhiteSpace(objectNames[objectIndex]) ? objectNames[objectIndex] : $"Object_{objectIndex}";
            string meshName = groups.Keys.Count(key => key.ObjectIndex == objectIndex) > 1 && (uint)materialIndex < (uint)materials.Length ? $"{objectName}_{materials[materialIndex].Name}" : objectName;
            Mesh mesh = model.AddNode<Mesh>(meshName);
            BuildMesh(mesh, group, bones);
            if ((uint)materialIndex < (uint)materials.Length)
                mesh.Materials = [materials[materialIndex]];
        }
    }

    private static void BuildMesh(Mesh mesh, (List<Vector3> Positions, List<Vector3> Normals, List<Vector4> Colors, List<Vector2[]> UVs, List<List<(int BoneIndex, float Weight)>> Weights) group, SkeletonBone[] bones)
    {
        int vertexCount = group.Positions.Count;
        int uvLayerCount = group.UVs.Count == 0 ? 0 : group.UVs.Max(static layers => layers.Length);
        int influenceCount = group.Weights.Count == 0 ? 0 : group.Weights.Max(static influences => influences.Count);
        DataBuffer<float> positions = new(vertexCount, 1, 3);
        DataBuffer<float> normals = new(vertexCount, 1, 3);
        DataBuffer<float> colors = new(vertexCount, 1, 4);
        DataBuffer<float>? uvLayers = uvLayerCount > 0 ? new DataBuffer<float>(vertexCount, uvLayerCount, 2) : null;
        DataBuffer<int>? boneIndices = influenceCount > 0 ? new DataBuffer<int>(vertexCount, influenceCount, 1) : null;
        DataBuffer<float>? boneWeights = influenceCount > 0 ? new DataBuffer<float>(vertexCount, influenceCount, 1) : null;
        DataBuffer<int> faceIndices = new(vertexCount, 1, 1);

        for (int i = 0; i < vertexCount; i++)
        {
            positions.Add(group.Positions[i]);
            normals.Add(group.Normals[i]);
            colors.Add(group.Colors[i]);
            faceIndices.Add(i);
            if (uvLayers is not null)
            {
                DataBufferElement element = uvLayers.Add();
                for (int layer = 0; layer < uvLayerCount; layer++)
                    element.Add(layer < group.UVs[i].Length ? group.UVs[i][layer] : Vector2.Zero);
            }
            if (boneIndices is not null && boneWeights is not null)
            {
                DataBufferElement indexElement = boneIndices.Add();
                DataBufferElement weightElement = boneWeights.Add();
                for (int influence = 0; influence < influenceCount; influence++)
                {
                    indexElement.Add(influence < group.Weights[i].Count ? group.Weights[i][influence].BoneIndex : 0);
                    weightElement.Add(influence < group.Weights[i].Count ? group.Weights[i][influence].Weight : 0f);
                }
            }
        }

        mesh.Positions = positions;
        mesh.Normals = normals;
        mesh.ColorLayers = colors;
        mesh.UVLayers = uvLayers;
        mesh.FaceIndices = faceIndices;
        if (boneIndices is not null && boneWeights is not null)
        {
            mesh.BoneIndices = boneIndices;
            mesh.BoneWeights = boneWeights;
            mesh.SkinnedBones = bones;
        }
    }

    private static string? ResolveTexturePath(string textureName, string? sourceDirectoryPath)
    {
        if (Path.IsPathRooted(textureName))
            return Path.GetFullPath(textureName);
        return string.IsNullOrWhiteSpace(sourceDirectoryPath) ? null : Path.GetFullPath(Path.Combine(sourceDirectoryPath, textureName));
    }

    private static void ValidateIndex(int index, int count, string description)
    {
        if ((uint)index >= (uint)count)
            throw new InvalidDataException($"The XModel {description} index {index} is outside the valid range 0-{count - 1}.");
    }
}
