using System.Globalization;
using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Imports FBX blend shapes as morph targets, keeping in-between shapes as additional targets.
/// </summary>
public static class FbxMorphImporter
{
    /// <summary>
    /// Imports the blend shapes of every mesh model.
    /// </summary>
    /// <param name="importer">The active scene importer.</param>
    public static void Import(FbxSceneImporter importer)
    {
        foreach ((long modelId, List<Mesh> meshes) in importer.Meshes)
        {
            if (importer.Graph.GetObject(modelId) is not FbxObject model)
                continue;

            FbxObject			geometry	= importer.Graph.GetFirstChild(modelId, "Geometry") ?? model;
            List<FbxMorphShape>	shapes		= ReadShapes(importer, geometry);

            if (shapes.Count == 0)
                continue;

            Matrix4x4 geometric = importer.GetTransform(modelId).Geometric;

            foreach (Mesh mesh in meshes)
            {
                if (importer.ControlPoints.TryGetValue(mesh, out int[]? controlPoints))
                    mesh.Morph = CreateMorph(geometry.Name, shapes, controlPoints, geometric);
            }
        }
    }

    /// <summary>
    /// Returns the target name used for a blend shape channel at the given full weight.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="fullWeight">The full weight percentage of the shape.</param>
    /// <param name="isLast">Whether the shape is the channel's final, fully weighted shape.</param>
    /// <returns>The target name.</returns>
    public static string GetTargetName(string channel, double fullWeight, bool isLast)
    {
        return isLast ? channel : string.Create(CultureInfo.InvariantCulture, $"{channel}_{fullWeight:0.##}");
    }

    private static List<FbxMorphShape> ReadShapes(FbxSceneImporter importer, FbxObject geometry)
    {
        List<FbxMorphShape> shapes = [];

        foreach (FbxObject blendShape in importer.Graph.GetChildren(geometry.Id, "Deformer"))
        {
            if (blendShape.SubType != "BlendShape")
                continue;

            foreach (FbxObject channel in importer.Graph.GetChildren(blendShape.Id, "Deformer"))
            {
                List<FbxObject>	channelShapes	= [.. importer.Graph.GetChildren(channel.Id, "Geometry")];
                double[]		fullWeights		= channel.GetChildDoubles("FullWeights");
                float			weight			= channel.GetSingle("DeformPercent", channel.Node.TryGetChildProperty("DeformPercent", out FbxProperty percent) && percent.IsNumber ? percent.AsSingle() : 0.0f) / 100.0f;

                for (int i = 0; i < channelShapes.Count; i++)
                {
                    double fullWeight = i < fullWeights.Length ? fullWeights[i] : 100.0;
                    shapes.Add(new FbxMorphShape(GetTargetName(channel.Name, fullWeight, i == channelShapes.Count - 1), channelShapes[i].Node, i == channelShapes.Count - 1 ? weight : 0.0f));
                }
            }
        }

        if (shapes.Count > 0)
            return shapes;

        foreach (FbxNode shape in geometry.Node.ChildrenNamed("Shape"))
        {
            string name = shape.Properties.Count > 0 && shape.Properties[0].Type == FbxPropertyType.String ? FbxObject.ParseName(shape.Properties[0].AsString()) : $"Shape{shapes.Count}";
            shapes.Add(new FbxMorphShape(name, shape, 0.0f));
        }

        return shapes;
    }

    private static Morph CreateMorph(string name, List<FbxMorphShape> shapes, int[] vertexControlPoints, Matrix4x4 geometric)
    {
        int			vertexCount		= vertexControlPoints.Length;
        int			targetCount		= shapes.Count;
        float[]		positions		= new float[vertexCount * targetCount * 3];
        float[]?	normals			= null;
        Matrix4x4	normalMatrix	= Matrix4x4.Invert(geometric, out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : geometric;
        int			controlPoints	= 0;

        foreach (int controlPoint in vertexControlPoints)
            controlPoints = Math.Max(controlPoints, controlPoint + 1);

        int[] vertexHeads	= new int[controlPoints];
        int[] vertexNext	= new int[vertexCount];

        Array.Fill(vertexHeads, -1);

        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            vertexNext[vertex]							= vertexHeads[vertexControlPoints[vertex]];
            vertexHeads[vertexControlPoints[vertex]]	= vertex;
        }

        for (int target = 0; target < targetCount; target++)
        {
            int[]		indices		= shapes[target].Node.FirstChild("Indexes")?.GetInt32s() ?? [];
            double[]	deltas		= shapes[target].Node.FirstChild("Vertices")?.GetDoubles() ?? [];
            double[]	normalDelta	= shapes[target].Node.FirstChild("Normals")?.GetDoubles() ?? [];

            if (normalDelta.Length > 0)
                normals ??= new float[vertexCount * targetCount * 3];

            for (int i = 0; i < indices.Length && i * 3 + 2 < deltas.Length; i++)
            {
                if ((uint)indices[i] >= (uint)controlPoints)
                    continue;

                Vector3 position	= Vector3.TransformNormal(new Vector3((float)deltas[i * 3], (float)deltas[i * 3 + 1], (float)deltas[i * 3 + 2]), geometric);
                Vector3 normal		= i * 3 + 2 < normalDelta.Length ? Vector3.TransformNormal(new Vector3((float)normalDelta[i * 3], (float)normalDelta[i * 3 + 1], (float)normalDelta[i * 3 + 2]), normalMatrix) : Vector3.Zero;

                for (int vertex = vertexHeads[indices[i]]; vertex >= 0; vertex = vertexNext[vertex])
                {
                    int offset = (vertex * targetCount + target) * 3;

                    position.CopyTo(positions, offset);

                    if (normals is not null)
                        normal.CopyTo(normals, offset);
                }
            }
        }

        Morph morph = new(shapes.ConvertAll(static shape => shape.Name), new DataBuffer<float>(positions, targetCount, 3), normals is null ? null : new DataBuffer<float>(normals, targetCount, 3), null)
        {
            Name = name,
        };

        for (int i = 0; i < targetCount; i++)
            morph.Weights[i] = shapes[i].Weight;

        return morph;
    }
}
