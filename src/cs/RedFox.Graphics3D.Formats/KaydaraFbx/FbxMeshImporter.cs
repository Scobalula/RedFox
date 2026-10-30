using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Converts FBX polygon geometry into triangulated meshes, splitting by material and welding identical corners.
/// </summary>
public static class FbxMeshImporter
{
    /// <summary>
    /// Imports the geometry of every mesh model.
    /// </summary>
    /// <param name="importer">The active scene importer.</param>
    /// <param name="materials">The imported materials keyed by FBX object identifier.</param>
    public static void Import(FbxSceneImporter importer, Dictionary<long, Material> materials)
    {
        foreach (long modelId in importer.ModelOrder)
        {
            SceneNode? node = importer.GetNode(modelId);

            if (node is not (Mesh or Group) || importer.Graph.GetObject(modelId) is not FbxObject model)
                continue;

            FbxObject? geometry = importer.Graph.GetFirstChild(modelId, "Geometry") ?? (model.Node.FirstChild("Vertices") is not null ? model : null);

            if (geometry is null)
                continue;

            List<Material> modelMaterials = [];

            foreach (FbxObject fbxMaterial in importer.Graph.GetChildren(modelId, "Material"))
            {
                if (materials.TryGetValue(fbxMaterial.Id, out Material? material))
                    modelMaterials.Add(material);
            }

            ImportGeometry(importer, modelId, node, geometry, modelMaterials);
        }
    }

    private static void ImportGeometry(FbxSceneImporter importer, long modelId, SceneNode node, FbxObject geometry, List<Material> materials)
    {
        double[]	vertices			= geometry.GetChildDoubles("Vertices");
        int[]		polygonVertices		= geometry.GetChildInt32s("PolygonVertexIndex");
        int			controlPointCount	= vertices.Length / 3;
        Matrix4x4	geometric			= importer.GetTransform(modelId).Geometric;
        Vector3[]	positions			= new Vector3[controlPointCount];

        for (int i = 0; i < controlPointCount; i++)
            positions[i] = Vector3.Transform(new Vector3((float)vertices[i * 3], (float)vertices[i * 3 + 1], (float)vertices[i * 3 + 2]), geometric);

        FbxMeshLayers	layers		= new(geometry.Node, geometric);
        List<int>		polygons	= GetPolygonStarts(polygonVertices);
        int				subMeshes	= Math.Max(materials.Count, 1);

        FbxMeshBuilder?[]	builders	= new FbxMeshBuilder?[subMeshes];
        int[]				corners		= new int[64];
        Vector3[]			polygon		= new Vector3[64];
        int[]				triangles	= new int[64 * 3];
        Span<float>			attributes	= stackalloc float[layers.Stride];

        for (int polygonIndex = 0; polygonIndex < polygons.Count - 1; polygonIndex++)
        {
            int start	= polygons[polygonIndex];
            int count	= polygons[polygonIndex + 1] - start;

            if (count < 3)
                continue;

            if (count > corners.Length)
            {
                corners		= new int[count];
                polygon		= new Vector3[count];
                triangles	= new int[(count - 2) * 3];
            }

            bool valid = true;

            for (int i = 0; i < count && valid; i++)
            {
                int controlPoint = DecodeIndex(polygonVertices[start + i]);

                valid		= (uint)controlPoint < (uint)controlPointCount;
                corners[i]	= start + i;
                polygon[i]	= valid ? positions[controlPoint] : Vector3.Zero;
            }

            if (!valid)
                continue;

            int				written		= MeshTriangulator.TriangulatePolygon(polygon.AsSpan(0, count), triangles);
            int				material	= Math.Clamp(layers.GetMaterial(polygonIndex), 0, subMeshes - 1);
            FbxMeshBuilder	builder		= builders[material] ??= new FbxMeshBuilder(controlPointCount, layers.Stride);

            for (int i = 0; i < written; i++)
            {
                int corner			= corners[triangles[i]];
                int controlPoint	= DecodeIndex(polygonVertices[corner]);

                layers.GetAttributes(corner, controlPoint, polygonIndex, attributes);
                builder.AddCorner(controlPoint, attributes);
            }
        }

        AttachMeshes(importer, modelId, node, builders, materials, positions, layers);
    }

    private static void AttachMeshes(FbxSceneImporter importer, long modelId, SceneNode node, FbxMeshBuilder?[] builders, List<Material> materials, Vector3[] positions, FbxMeshLayers layers)
    {
        List<Mesh> meshes = [];

        for (int i = 0; i < builders.Length; i++)
        {
            if (builders[i] is not FbxMeshBuilder builder)
                continue;

            Mesh mesh = node as Mesh ?? new Mesh { Name = importer.GetUniqueName(i < materials.Count ? $"{node.Name}_{materials[i].Name}" : $"{node.Name}_{i}") };

            builder.Build(mesh, positions, layers);

            if (i < materials.Count)
                mesh.Materials = [materials[i]];

            if (!ReferenceEquals(mesh, node))
                mesh.SetParentUnsafe(node);

            importer.ControlPoints[mesh] = [.. builder.ControlPoints];
            meshes.Add(mesh);
        }

        importer.Meshes[modelId] = meshes;
    }

    private static List<int> GetPolygonStarts(int[] polygonVertices)
    {
        List<int> starts = [0];

        for (int i = 0; i < polygonVertices.Length; i++)
        {
            if (polygonVertices[i] < 0 || i == polygonVertices.Length - 1)
                starts.Add(i + 1);
        }

        return starts;
    }

    private static int DecodeIndex(int value)
    {
        return value < 0 ? ~value : value;
    }
}
