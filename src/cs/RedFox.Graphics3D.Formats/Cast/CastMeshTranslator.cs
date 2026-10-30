using CastNet;
using CastNet.Nodes;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.Skeletal;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastMeshTranslator
{
    public static Mesh Read(MeshGroup model, MeshNode meshNode, Dictionary<MaterialNode, Material> materials, SkeletonBone[]? bones)
    {
        var mesh			= model.AddNode<Mesh>();
        var vertexCount		= meshNode.VertexCount;
        var uvLayerCount	= meshNode.UVLayerCount;
        var colorLayerCount	= meshNode.ColorLayerCount;
        var influences		= meshNode.MaximumWeightInfluence;

        if (meshNode.Positions is CastArrayProperty positions)
        {
            if (positions.Type != CastPropertyType.Vector3)
                throw new InvalidDataException("Invalid Cast position array type.");
            mesh.Positions = new DataBuffer<float>(positions.AsBytes().ToArray(), 1, 3);
        }
        if (meshNode.Normals is CastArrayProperty normals)
        {
            if (normals.Type != CastPropertyType.Vector3 || normals.Count != vertexCount)
                throw new InvalidDataException("Inconsistent Cast normal array length.");
            mesh.Normals = new DataBuffer<float>(normals.AsBytes().ToArray(), 1, 3);
        }
        if (meshNode.Tangents is CastArrayProperty tangents)
        {
            if (tangents.Type != CastPropertyType.Vector3 || tangents.Count != vertexCount)
                throw new InvalidDataException("Inconsistent Cast tangent array length.");
            mesh.Tangents = new DataBuffer<float>(tangents.AsBytes().ToArray(), 1, 3);
        }

        if (uvLayerCount > 0)
        {
            var uvs = new Vector2[checked(vertexCount * uvLayerCount)];

            for (var layer = 0; layer < uvLayerCount; layer++)
            {
                if (meshNode.GetUVLayer(layer) is not { Type: CastPropertyType.Vector2 } uvLayer)
                    throw new InvalidDataException("Invalid Cast UV array type.");

                var values = uvLayer.AsSpan<Vector2>();
                if (values.Length != vertexCount)
                    throw new InvalidDataException("Inconsistent Cast UV array length.");

                for (var vertex = 0; vertex < vertexCount; vertex++)
                    uvs[vertex * uvLayerCount + layer] = values[vertex];
            }

            mesh.UVLayers = new DataBuffer<float>(MemoryMarshal.AsBytes(uvs.AsSpan()).ToArray(), uvLayerCount, 2);
        }

        if (colorLayerCount > 0)
        {
            var colors = new float[checked(vertexCount * colorLayerCount * 4)];

            for (var layer = 0; layer < colorLayerCount; layer++)
            {
                var values = meshNode.GetColors(layer) ?? [];
                if (values.Length != vertexCount)
                    throw new InvalidDataException("Inconsistent Cast color array length.");

                for (var vertex = 0; vertex < vertexCount; vertex++)
                    values[vertex].CopyTo(colors, (vertex * colorLayerCount + layer) * 4);
            }

            mesh.ColorLayers = new DataBuffer<float>(colors, colorLayerCount, 4);
        }

        if (influences > 0)
        {
            if (bones is null || meshNode.WeightBones is not CastArrayProperty weightBones || meshNode.WeightValues is not CastArrayProperty weightValues)
                throw new InvalidDataException("Missing Cast skin arrays or skeleton.");
            int count = checked(vertexCount * influences);
            if (weightBones.Count != count || weightValues.Count != count)
                throw new InvalidDataException("Inconsistent Cast skin array length.");
            var boneIndices= new int[count];
            var boneWeights= new float[count];

            weightBones.CopyTo<int>(boneIndices.AsSpan());
            weightValues.CopyTo<float>(boneWeights.AsSpan());
            mesh.Skin = new Skin(bones, new DataBuffer<int>(boneIndices, influences, 1), new DataBuffer<float>(boneWeights, influences, 1)) { SkinningMode = meshNode.SkinningMethod == "quaternion" ? SkinningMode.DualQuaternion : SkinningMode.Linear };
        }

        mesh.FaceIndices = meshNode.Faces switch
        {
            { Type: CastPropertyType.Byte } faces => new DataBuffer<byte>(faces.AsSpan<byte>().ToArray(), 1, 1),
            { Type: CastPropertyType.Short } faces => new DataBuffer<ushort>(faces.AsSpan<ushort>().ToArray(), 1, 1),
            CastArrayProperty faces => new DataBuffer<uint>(faces.ToArray<uint>(), 1, 1),
            null => null,
        };

        if (meshNode.Material is MaterialNode materialNode && materials.TryGetValue(materialNode, out var material))
            mesh.Materials = [material];

        return mesh;
    }

    public static MeshNode? Write(ModelNode modelNode, Mesh mesh, Dictionary<SkeletonBone, int> boneTable, Dictionary<Material, MaterialNode> materials)
    {
        if (mesh.Positions is null)
            return null;

        var vertexCount	= mesh.Positions.ElementCount;
        var meshNode	= modelNode.AddNode(new MeshNode
        {
            Name		= mesh.Name,
            Positions	= CreateVector3Property(mesh.Positions),
        });

        if (mesh.Normals is not null)
            meshNode.Normals = CreateVector3Property(mesh.Normals);
        if (mesh.Tangents is not null)
            meshNode.Tangents = CreateVector3Property(mesh.Tangents);

        if (mesh.UVLayers is { } uvLayers)
        {
            for (var layer = 0; layer < uvLayers.ValueCount; layer++)
            {
                var uvLayer = new CastArrayProperty(CastPropertyType.Vector2, vertexCount);

                for (var vertex = 0; vertex < vertexCount; vertex++)
                    uvLayer.Add(uvLayers.GetVector2(vertex, layer));

                meshNode.SetUVLayer(layer, uvLayer);
            }
        }

        if (mesh.ColorLayers is { } colorLayers)
        {
            for (var layer = 0; layer < colorLayers.ValueCount; layer++)
            {
                var scale		= colorLayers is DataBuffer<byte> ? 1.0f : byte.MaxValue;
                var colorLayer	= new CastArrayProperty(CastPropertyType.Integer32, vertexCount);

                for (var vertex = 0; vertex < vertexCount; vertex++)
                {
                    var color = Vector4.Clamp(colorLayers.GetVector4(vertex, layer) * scale, Vector4.Zero, new Vector4(byte.MaxValue));
                    colorLayer.Add((uint)color.X | (uint)color.Y << 8 | (uint)color.Z << 16 | (uint)color.W << 24);
                }

                meshNode.SetColorLayer(layer, colorLayer);
            }
        }

        if (mesh.Skin is Skin skin)
        {
            var influences		= skin.BoneIndices.ValueCount;
            var tableIndices	= skin.GetBoneTableIndices(boneTable);
            var boneIndices		= new int[vertexCount * influences];
            var boneWeights		= new float[vertexCount * influences];

            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                for (var influence = 0; influence < influences; influence++)
                {
                    boneIndices[vertex * influences + influence] = tableIndices[skin.BoneIndices.Get<int>(vertex, influence, 0)];
                    boneWeights[vertex * influences + influence] = skin.BoneWeights.Get<float>(vertex, influence, 0);
                }
            }

            meshNode.MaximumWeightInfluence = influences;

            if (skin.SkinningMode == SkinningMode.DualQuaternion)
                meshNode.SkinningMethod = "quaternion";
            meshNode.WeightBones = CastArrayProperty.CreateIndices<int>(boneIndices);
            meshNode.WeightValues = CastArrayProperty.Create<float>(boneWeights);
        }

        if (mesh.FaceIndices is not null)
        {
            var faces = new int[mesh.FaceIndices.ElementCount];

            for (var i = 0; i < faces.Length; i++)
                faces[i] = mesh.FaceIndices.Get<int>(i, 0, 0);

            meshNode.Faces = CastArrayProperty.CreateIndices<int>(faces);
        }

        if (mesh.Materials is [Material material, ..])
            meshNode.Material = materials[material];

        return meshNode;
    }

    internal static CastArrayProperty CreateVector3Property(DataBuffer buffer)
    {
        var property = new CastArrayProperty(CastPropertyType.Vector3, buffer.ElementCount);

        for (var i = 0; i < buffer.ElementCount; i++)
            property.Add(buffer.GetVector3(i, 0));

        return property;
    }
}
