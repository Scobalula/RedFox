using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Writes mesh geometry, skin deformers and blend shapes.
/// </summary>
public static class FbxMeshExporter
{
    /// <summary>
    /// Writes the geometry, materials, skin and morph targets of an exported mesh model.
    /// </summary>
    /// <param name="exporter">The active scene exporter.</param>
    /// <param name="mesh">The mesh to write.</param>
    /// <param name="modelId">The identifier of the mesh's model.</param>
    public static void Export(FbxSceneExporter exporter, Mesh mesh, long modelId)
    {
        if (mesh.Positions is null || mesh.VertexCount == 0)
            return;

        int[]	indices		= GetTriangleIndices(mesh);
        long	geometryId	= exporter.AddObject("Geometry", mesh.Name, "Mesh", out FbxNode geometry);

        geometry.AddChild("Properties70");
        geometry.AddChild("GeometryVersion", new FbxProperty(124));
        geometry.AddChild("Vertices", new FbxProperty(GetVectors(mesh.VertexCount, vertex => mesh.GetVertexPosition(vertex, true))));
        geometry.AddChild("PolygonVertexIndex", new FbxProperty(GetPolygonVertexIndices(indices)));

        WriteLayers(geometry, mesh, indices);

        exporter.Connect(geometryId, modelId);

        foreach (Material material in mesh.Materials ?? [])
        {
            if (exporter.MaterialIds.TryGetValue(material, out long materialId))
                exporter.Connect(materialId, modelId);
        }

        if (mesh.Skin is not null)
            WriteSkin(exporter, mesh, mesh.Skin, geometryId, modelId);

        if (mesh.Morph is not null)
            WriteMorph(exporter, mesh, mesh.Morph, geometryId);
    }

    private static int[] GetTriangleIndices(Mesh mesh)
    {
        if (mesh.FaceIndices is null)
            return [.. Enumerable.Range(0, mesh.VertexCount / 3 * 3)];

        int[] indices = new int[mesh.FaceIndices.ElementCount / 3 * 3];

        for (int i = 0; i < indices.Length; i++)
            indices[i] = mesh.FaceIndices.Get<int>(i, 0, 0);

        return indices;
    }

    private static int[] GetPolygonVertexIndices(int[] indices)
    {
        int[] polygonVertices = (int[])indices.Clone();

        for (int i = 2; i < polygonVertices.Length; i += 3)
            polygonVertices[i] = ~polygonVertices[i];

        return polygonVertices;
    }

    private static double[] GetVectors(int count, Func<int, Vector3> getValue)
    {
        double[] values = new double[count * 3];

        for (int i = 0; i < count; i++)
        {
            Vector3 value = getValue(i);

            values[i * 3]		= value.X;
            values[i * 3 + 1]	= value.Y;
            values[i * 3 + 2]	= value.Z;
        }

        return values;
    }

    private static void WriteLayers(FbxNode geometry, Mesh mesh, int[] indices)
    {
        int			layerCount	= Math.Max(1, Math.Max(mesh.UVLayerCount, mesh.ColorLayerCount));
        FbxNode[]	layers		= new FbxNode[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            layers[i] = new FbxNode("Layer") { Properties = { new FbxProperty(i) } };
            layers[i].AddChild("Version", new FbxProperty(100));
        }

        if (mesh.Normals is not null)
        {
            FbxNode element = AddElement(geometry, layers[0], "LayerElementNormal", 0, string.Empty, "ByVertice", "Direct");
            element.AddChild("Normals", new FbxProperty(GetVectors(mesh.VertexCount, vertex => mesh.GetVertexNormal(vertex, true))));
        }

        if (mesh.Tangents is not null)
        {
            FbxNode element = AddElement(geometry, layers[0], "LayerElementTangent", 0, string.Empty, "ByVertice", "Direct");
            element.AddChild("Tangents", new FbxProperty(GetVectors(mesh.VertexCount, vertex => mesh.GetVertexTangent(vertex, true).AsVector3())));
        }

        if (mesh.BiTangents is not null || (mesh.Tangents is { ComponentCount: > 3 } && mesh.Normals is not null))
        {
            FbxNode element = AddElement(geometry, layers[0], "LayerElementBinormal", 0, string.Empty, "ByVertice", "Direct");
            element.AddChild("Binormals", new FbxProperty(GetVectors(mesh.VertexCount, vertex => GetBinormal(mesh, vertex))));
        }

        for (int layer = 0; layer < mesh.UVLayerCount; layer++)
        {
            FbxNode		element	= AddElement(geometry, layers[layer], "LayerElementUV", layer, $"map{layer + 1}", "ByPolygonVertex", "IndexToDirect");
            double[]	values	= new double[mesh.VertexCount * 2];

            for (int vertex = 0; vertex < mesh.VertexCount; vertex++)
            {
                values[vertex * 2]		= mesh.UVLayers!.Get<float>(vertex, layer, 0);
                values[vertex * 2 + 1]	= mesh.UVLayers.Get<float>(vertex, layer, 1);
            }

            element.AddChild("UV", new FbxProperty(values));
            element.AddChild("UVIndex", new FbxProperty((int[])indices.Clone()));
        }

        for (int layer = 0; layer < mesh.ColorLayerCount; layer++)
        {
            FbxNode		element	= AddElement(geometry, layers[layer], "LayerElementColor", layer, $"colorSet{layer + 1}", "ByPolygonVertex", "IndexToDirect");
            double[]	values	= new double[mesh.VertexCount * 4];

            for (int vertex = 0; vertex < mesh.VertexCount; vertex++)
            {
                for (int component = 0; component < 4; component++)
                    values[vertex * 4 + component] = component < mesh.ColorLayers!.ComponentCount ? mesh.ColorLayers.Get<float>(vertex, layer, component) : 1.0;
            }

            element.AddChild("Colors", new FbxProperty(values));
            element.AddChild("ColorIndex", new FbxProperty((int[])indices.Clone()));
        }

        int[] materials = GetPolygonMaterials(mesh, indices.Length / 3);

        if (mesh.Materials is { Count: > 0 })
        {
            FbxNode element = AddElement(geometry, layers[0], "LayerElementMaterial", 0, string.Empty, materials.Length == 1 ? "AllSame" : "ByPolygon", "IndexToDirect");
            element.AddChild("Materials", new FbxProperty(materials));
        }

        geometry.Children.AddRange(layers);
    }

    private static FbxNode AddElement(FbxNode geometry, FbxNode layer, string type, int index, string name, string mapping, string reference)
    {
        FbxNode element = geometry.AddChild(type, new FbxProperty(index));

        element.AddChild("Version", new FbxProperty(101));
        element.AddChild("Name", new FbxProperty(name));
        element.AddChild("MappingInformationType", new FbxProperty(mapping));
        element.AddChild("ReferenceInformationType", new FbxProperty(reference));

        FbxNode entry = layer.AddChild("LayerElement");
        entry.AddChild("Type", new FbxProperty(type));
        entry.AddChild("TypedIndex", new FbxProperty(index));

        return element;
    }

    private static Vector3 GetBinormal(Mesh mesh, int vertex)
    {
        if (mesh.BiTangents is not null)
            return mesh.GetVertexBiTangent(vertex, true);

        Vector4 tangent = mesh.GetVertexTangent(vertex, true);
        return Vector3.Cross(mesh.GetVertexNormal(vertex, true), tangent.AsVector3()) * (tangent.W < 0.0f ? -1.0f : 1.0f);
    }

    private static int[] GetPolygonMaterials(Mesh mesh, int triangleCount)
    {
        if (mesh.Materials is not { Count: > 1 } || mesh.MaterialIndexRanges is not { Count: > 1 } ranges)
            return [0];

        int[] materials = new int[triangleCount];

        for (int material = 0; material < ranges.Count; material++)
        {
            int start	= ranges[material].StartIndex / 3;
            int end		= Math.Min(triangleCount, (ranges[material].StartIndex + ranges[material].IndexCount) / 3);

            for (int triangle = start; triangle < end; triangle++)
                materials[triangle] = Math.Min(material, mesh.Materials.Count - 1);
        }

        return materials;
    }

    private static void WriteSkin(FbxSceneExporter exporter, Mesh mesh, Skin skin, long geometryId, long modelId)
    {
        string[] missing = [.. skin.Bones.Where(bone => !exporter.ModelIds.ContainsKey(bone)).Select(static bone => bone.Name)];

        if (missing.Length > 0)
            throw new InvalidDataException($"Mesh '{mesh.Name}' references skinned bones that are not part of the export selection: {string.Join(", ", missing)}.");

        long		skinId			= exporter.AddObject("Deformer", skin.Name ?? mesh.Name, "Skin", out FbxNode skinNode);
        Matrix4x4	meshBindWorld	= exporter.GetBindWorld(mesh);

        skinNode.AddChild("Version", new FbxProperty(101));
        skinNode.AddChild("Link_DeformAcuracy", new FbxProperty(50.0));
        skinNode.AddChild("SkinningType", new FbxProperty(skin.SkinningMode == SkinningMode.DualQuaternion ? "DualQuaternion" : "Linear"));
        exporter.Connect(skinId, geometryId);
        exporter.BindPose[modelId] = meshBindWorld;

        List<int>[]		vertices	= new List<int>[skin.Bones.Count];
        List<double>[]	weights		= new List<double>[skin.Bones.Count];

        for (int bone = 0; bone < skin.Bones.Count; bone++)
        {
            vertices[bone]	= [];
            weights[bone]	= [];
        }

        for (int vertex = 0; vertex < mesh.VertexCount; vertex++)
        {
            for (int influence = 0; influence < skin.InfluenceCount; influence++)
            {
                int		bone	= skin.BoneIndices.Get<int>(vertex, influence, 0);
                float	weight	= skin.BoneWeights.Get<float>(vertex, influence, 0);

                if (weight <= 0.0f || (uint)bone >= (uint)skin.Bones.Count)
                    continue;

                if (vertices[bone].Count > 0 && vertices[bone][^1] == vertex)
                {
                    weights[bone][^1] += weight;
                    continue;
                }

                vertices[bone].Add(vertex);
                weights[bone].Add(weight);
            }
        }

        for (int bone = 0; bone < skin.Bones.Count; bone++)
        {
            long		boneId			= exporter.ModelIds[skin.Bones[bone]];
            Matrix4x4	boneBindWorld	= exporter.GetBindWorld(skin.Bones[bone]);
            long		clusterId		= exporter.AddObject("Deformer", skin.Bones[bone].Name, "Cluster", out FbxNode cluster);

            cluster.AddChild("Version", new FbxProperty(100));
            cluster.AddChild("UserData", new FbxProperty(string.Empty)).Properties.Add(new FbxProperty(string.Empty));
            cluster.AddChild("Indexes", new FbxProperty(vertices[bone].ToArray()));
            cluster.AddChild("Weights", new FbxProperty(weights[bone].ToArray()));
            cluster.AddChild("Transform", new FbxProperty(FbxSceneExporter.ToArray(skin.GetInverseBindMatrix(bone, meshBindWorld))));
            cluster.AddChild("TransformLink", new FbxProperty(FbxSceneExporter.ToArray(boneBindWorld)));

            exporter.Connect(clusterId, skinId);
            exporter.Connect(boneId, clusterId);
            exporter.BindPose[boneId] = boneBindWorld;
        }
    }

    private static void WriteMorph(FbxSceneExporter exporter, Mesh mesh, Morph morph, long geometryId)
    {
        long blendShapeId = exporter.AddObject("Deformer", morph.Name ?? mesh.Name, "BlendShape", out FbxNode blendShape);

        blendShape.AddChild("Version", new FbxProperty(100));
        exporter.Connect(blendShapeId, geometryId);

        for (int target = 0; target < morph.TargetCount; target++)
        {
            string			name		= morph.TargetNames[target];
            List<int>		vertices	= [];
            List<double>	positions	= [];
            List<double>	normals		= [];

            for (int vertex = 0; vertex < mesh.VertexCount; vertex++)
            {
                Vector3 position	= GetDelta(morph.DeltaPositions, vertex, target);
                Vector3 normal		= GetDelta(morph.DeltaNormals, vertex, target);

                if (position == Vector3.Zero && normal == Vector3.Zero)
                    continue;

                vertices.Add(vertex);
                positions.AddRange([position.X, position.Y, position.Z]);
                normals.AddRange([normal.X, normal.Y, normal.Z]);
            }

            long channelId	= exporter.AddObject("Deformer", name, "BlendShapeChannel", out FbxNode channel);
            long shapeId	= exporter.AddObject("Geometry", name, "Shape", out FbxNode shape);

            channel.AddChild("Version", new FbxProperty(100));
            channel.AddChild("DeformPercent", new FbxProperty(morph.Weights[target] * 100.0));
            channel.AddChild("FullWeights", new FbxProperty([100.0]));
            shape.AddChild("Version", new FbxProperty(100));
            shape.AddChild("Indexes", new FbxProperty(vertices.ToArray()));
            shape.AddChild("Vertices", new FbxProperty(positions.ToArray()));

            if (morph.DeltaNormals is not null)
                shape.AddChild("Normals", new FbxProperty(normals.ToArray()));

            exporter.Connect(channelId, blendShapeId);
            exporter.Connect(shapeId, channelId);

            if (!exporter.BlendShapeChannels.TryGetValue(name, out List<long>? channels))
                exporter.BlendShapeChannels[name] = channels = [];

            channels.Add(channelId);
        }
    }

    private static Vector3 GetDelta(DataBuffer? buffer, int vertex, int target)
    {
        if (buffer is null)
            return Vector3.Zero;

        return new Vector3(buffer.Get<float>(vertex, target, 0), buffer.Get<float>(vertex, target, 1), buffer.Get<float>(vertex, target, 2));
    }
}
