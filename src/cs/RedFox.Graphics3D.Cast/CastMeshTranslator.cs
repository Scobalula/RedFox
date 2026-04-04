using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Skeletal;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Cast;

internal static class CastMeshTranslator
{
    public static void Read(Model model, MeshNode meshNode, Dictionary<ulong, Material> materials, SkeletonBone[]? bones)
    {
        var mesh = model.AddNode<Mesh>();
        var posBuffer = meshNode.VertexPositionBuffer;
        int vertexCount = posBuffer.ValueCount;

        // Positions
        mesh.Positions = CreateDataBufferFromVector3Array(posBuffer.Values);

        // Normals
        if (meshNode.VertexNormalBuffer is { } normalBuffer)
        {
            mesh.Normals = CreateDataBufferFromVector3Array(normalBuffer.Values);
        }

        // Tangents
        if (meshNode.VertexTangentBuffer is { } tangentBuffer)
        {
            mesh.Tangents = CreateDataBufferFromVector3Array(tangentBuffer.Values);
        }

        // UV Layers
        int uvLayerCount = meshNode.UVLayerCount;
        if (uvLayerCount > 0)
        {
            var uvFloats = new float[vertexCount * uvLayerCount * 2];

            for (int layer = 0; layer < uvLayerCount; layer++)
            {
                if (meshNode.GetUVLayer(layer) is CastArrayProperty<Vector2> uvLayer)
                {
                    for (int v = 0; v < vertexCount && v < uvLayer.ValueCount; v++)
                    {
                        int offset = v * (uvLayerCount * 2) + layer * 2;
                        uvFloats[offset] = uvLayer.Values[v].X;
                        uvFloats[offset + 1] = uvLayer.Values[v].Y;
                    }
                }
            }

            mesh.UVLayers = new DataBuffer<float>(uvFloats, uvLayerCount, 2);
        }

        // Bone weights / influences
        int influences = meshNode.MaximumWeightInfluence;
        if (influences > 0 && bones is not null)
        {
            var boneIdxData = new int[vertexCount * influences];
            var boneWtData = new float[vertexCount * influences];

            int idx = 0;
            foreach (var (boneIndex, weight) in meshNode.EnumerateBoneWeights())
            {
                if (idx < boneIdxData.Length)
                {
                    boneIdxData[idx] = boneIndex;
                    boneWtData[idx] = weight;
                }
                idx++;
            }

            mesh.BoneIndices = new DataBuffer<int>(boneIdxData, influences, 1);
            mesh.BoneWeights = new DataBuffer<float>(boneWtData, influences, 1);
            mesh.SetSkinBinding(bones);
        }

        // Face indices
        mesh.FaceIndices = meshNode.FaceBuffer switch
        {
            CastArrayProperty<byte> byteFaces     => new DataBuffer<byte>([.. byteFaces.Values], 1, 1),
            CastArrayProperty<ushort> shortFaces   => new DataBuffer<ushort>([.. shortFaces.Values], 1, 1),
            CastArrayProperty<uint> uintFaces      => new DataBuffer<uint>([.. uintFaces.Values], 1, 1),
            CastArrayProperty<int> intFaces        => new DataBuffer<int>([.. intFaces.Values], 1, 1),
            _ => null
        };

        // Material assignment
        if (materials.TryGetValue(meshNode.MaterialHash, out var material))
        {
            mesh.Materials = [material];
        }
    }

    public static void Write(ModelNode modelNode, Mesh mesh, Dictionary<SkeletonBone, int> boneTable)
    {
        if (mesh.Positions is null)
            return;

        var meshNode = modelNode.AddNode<MeshNode>();
        int vertexCount = mesh.Positions.ElementCount;

        // Positions
        var posArray = meshNode.AddArray<Vector3>("vp", vertexCount);
        for (int v = 0; v < vertexCount; v++)
            posArray.Add(mesh.Positions.GetVector3(v, 0));

        // Normals
        if (mesh.Normals is not null)
        {
            var normalArray = meshNode.AddArray<Vector3>("vn", mesh.Normals.ElementCount);
            for (int v = 0; v < mesh.Normals.ElementCount; v++)
                normalArray.Add(mesh.Normals.GetVector3(v, 0));
        }

        // Tangents
        if (mesh.Tangents is not null)
        {
            var tangentArray = meshNode.AddArray<Vector3>("vt", mesh.Tangents.ElementCount);
            for (int v = 0; v < mesh.Tangents.ElementCount; v++)
                tangentArray.Add(mesh.Tangents.GetVector3(v, 0));
        }

        // UV layers
        if (mesh.UVLayers is not null)
        {
            int uvLayerCount = mesh.UVLayers.ValueCount;
            meshNode.AddValue<byte>("ul", (byte)uvLayerCount);

            for (int layer = 0; layer < uvLayerCount; layer++)
            {
                var uvLayer = meshNode.AddArray<Vector2>($"u{layer}", vertexCount);
                for (int v = 0; v < vertexCount; v++)
                    uvLayer.Add(mesh.UVLayers.GetVector2(v, layer));
            }
        }

        // Bone weights
        if (mesh.BoneIndices is not null && mesh.BoneWeights is not null && mesh.SkinnedBones is not null)
        {
            int influenceCount = mesh.BoneIndices.ValueCount;
            meshNode.AddValue<byte>("mi", (byte)influenceCount);

            int[] globalBoneIndices = mesh.GetBoneIndices(boneTable);

            var boneIndexArray = meshNode.AddArray<uint>("wb", vertexCount * influenceCount);
            var boneWeightArray = meshNode.AddArray<float>("wv", vertexCount * influenceCount);

            for (int v = 0; v < vertexCount; v++)
            {
                for (int j = 0; j < influenceCount; j++)
                {
                    int localIdx = mesh.BoneIndices.Get<int>(v, j, 0);
                    boneIndexArray.Add((uint)globalBoneIndices[localIdx]);
                    boneWeightArray.Add(mesh.BoneWeights.Get<float>(v, j, 0));
                }
            }
        }

        // Face indices
        if (mesh.FaceIndices is not null)
        {
            var faces = meshNode.AddArray<uint>("f", mesh.FaceIndices.ElementCount);
            for (int f = 0; f < mesh.FaceIndices.ElementCount; f++)
                faces.Add(mesh.FaceIndices.Get<uint>(f, 0, 0));
        }

        // Material hash
        if (mesh.Materials is [{ } firstMaterial, ..])
        {
            meshNode.AddValue("m", CastHasher.Compute(firstMaterial.Name));
        }
    }

    private static DataBuffer<float> CreateDataBufferFromVector3Array(List<Vector3> values)
    {
        var bytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(values)).ToArray();
        return new DataBuffer<float>(bytes, 1, 3);
    }
}
