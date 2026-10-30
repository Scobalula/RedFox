using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Imports FBX skin deformers, cluster weights and the bind pose of the bones they reference.
/// </summary>
public static class FbxSkinImporter
{
    private static readonly Comparer<float> DescendingWeights = Comparer<float>.Create(static (a, b) => b.CompareTo(a));

    /// <summary>
    /// Imports every skin deformer and applies bind poses to the skinned bones.
    /// </summary>
    /// <param name="importer">The active scene importer.</param>
    public static void Import(FbxSceneImporter importer)
    {
        Dictionary<long, Matrix4x4> bindWorlds = ReadBindPoses(importer);

        foreach (FbxObject skin in importer.Graph.Objects)
        {
            if (skin.ClassName != "Deformer" || skin.SubType != "Skin")
                continue;

            List<SkeletonBone>	bones			= [];
            List<Matrix4x4>		inverseBinds	= [];
            List<int[]>			indices			= [];
            List<double[]>		weights			= [];

            foreach (FbxObject cluster in importer.Graph.GetChildren(skin.Id, "Deformer"))
            {
                FbxObject? link = importer.Graph.GetFirstChild(cluster.Id, "Model") ?? importer.Graph.GetFirstParent(cluster.Id, "Model");

                if (link is null || importer.GetNode(link.Id) is not SkeletonBone bone)
                    continue;

                if (ReadMatrix(cluster.Node, "TransformLink") is Matrix4x4 linkWorld)
                    bindWorlds[link.Id] = linkWorld;

                bones.Add(bone);
                inverseBinds.Add(ReadMatrix(cluster.Node, "Transform") ?? Matrix4x4.Identity);
                indices.Add(cluster.GetChildInt32s("Indexes"));
                weights.Add(cluster.GetChildDoubles("Weights"));
            }

            if (bones.Count == 0)
                continue;

            SkinningMode mode = skin.Node.TryGetChildProperty("SkinningType", out FbxProperty type) && type.Type == FbxPropertyType.String && type.AsString() == "DualQuaternion" ? SkinningMode.DualQuaternion : SkinningMode.Linear;

            foreach (long modelId in GetOwnerModels(importer, skin))
            {
                foreach (Mesh mesh in importer.Meshes.GetValueOrDefault(modelId) ?? [])
                {
                    if (importer.ControlPoints.TryGetValue(mesh, out int[]? controlPoints))
                        mesh.Skin = CreateSkin(skin.Name, mode, bones, inverseBinds, indices, weights, controlPoints);
                }
            }
        }

        ApplyBindPoses(importer, bindWorlds);
    }

    private static IEnumerable<long> GetOwnerModels(FbxSceneImporter importer, FbxObject skin)
    {
        foreach (FbxObject parent in importer.Graph.GetParents(skin.Id, "Geometry"))
        {
            foreach (FbxObject model in importer.Graph.GetParents(parent.Id, "Model"))
                yield return model.Id;
        }

        foreach (FbxObject model in importer.Graph.GetParents(skin.Id, "Model"))
            yield return model.Id;
    }

    private static Skin CreateSkin(string name, SkinningMode mode, List<SkeletonBone> bones, List<Matrix4x4> inverseBinds, List<int[]> indices, List<double[]> weights, int[] vertexControlPoints)
    {
        int controlPointCount = 0;

        foreach (int controlPoint in vertexControlPoints)
            controlPointCount = Math.Max(controlPointCount, controlPoint + 1);

        int[] counts = new int[controlPointCount + 1];

        for (int bone = 0; bone < bones.Count; bone++)
        {
            for (int i = 0; i < Math.Min(indices[bone].Length, weights[bone].Length); i++)
            {
                if ((uint)indices[bone][i] < (uint)controlPointCount && weights[bone][i] > 0.0)
                    counts[indices[bone][i] + 1]++;
            }
        }

        for (int i = 1; i < counts.Length; i++)
            counts[i] += counts[i - 1];

        int[]	cursor			= counts[..^1];
        int[]	influenceBones	= new int[counts[^1]];
        float[]	influenceWeight	= new float[counts[^1]];

        for (int bone = 0; bone < bones.Count; bone++)
        {
            for (int i = 0; i < Math.Min(indices[bone].Length, weights[bone].Length); i++)
            {
                int controlPoint = indices[bone][i];

                if ((uint)controlPoint >= (uint)controlPointCount || weights[bone][i] <= 0.0)
                    continue;

                influenceBones[cursor[controlPoint]]	= bone;
                influenceWeight[cursor[controlPoint]]	= (float)weights[bone][i];
                cursor[controlPoint]++;
            }
        }

        int stride = 1;

        foreach (int controlPoint in vertexControlPoints)
            stride = Math.Max(stride, counts[controlPoint + 1] - counts[controlPoint]);

        int[]	vertexBones		= new int[vertexControlPoints.Length * stride];
        float[]	vertexWeights	= new float[vertexControlPoints.Length * stride];

        for (int vertex = 0; vertex < vertexControlPoints.Length; vertex++)
        {
            int start	= counts[vertexControlPoints[vertex]];
            int count	= counts[vertexControlPoints[vertex] + 1] - start;

            Array.Sort(influenceWeight, influenceBones, start, count, DescendingWeights);

            float total = 0.0f;

            for (int i = 0; i < count; i++)
                total += influenceWeight[start + i];

            for (int i = 0; i < count; i++)
            {
                vertexBones[vertex * stride + i]	= influenceBones[start + i];
                vertexWeights[vertex * stride + i]	= total > 0.0f ? influenceWeight[start + i] / total : 0.0f;
            }
        }

        return new Skin(bones, new DataBuffer<int>(vertexBones, stride, 1), new DataBuffer<float>(vertexWeights, stride, 1), inverseBinds)
        {
            Name			= name,
            SkinningMode	= mode,
        };
    }

    private static Dictionary<long, Matrix4x4> ReadBindPoses(FbxSceneImporter importer)
    {
        Dictionary<long, Matrix4x4> bindWorlds = [];

        foreach (FbxObject pose in importer.Graph.Objects)
        {
            if (pose.ClassName != "Pose" || pose.SubType != "BindPose")
                continue;

            foreach (FbxNode poseNode in pose.Node.ChildrenNamed("PoseNode"))
            {
                if (!poseNode.TryGetChildProperty("Node", out FbxProperty node) || ReadMatrix(poseNode, "Matrix") is not Matrix4x4 matrix)
                    continue;

                long id = node.Type == FbxPropertyType.String ? (importer.Graph.TryGetIdByName(node.AsString(), out long namedId) ? namedId : -1) : node.AsInt64();

                if (importer.GetNode(id) is SkeletonBone)
                    bindWorlds.TryAdd(id, matrix);
            }
        }

        return bindWorlds;
    }

    private static void ApplyBindPoses(FbxSceneImporter importer, Dictionary<long, Matrix4x4> bindWorlds)
    {
        if (bindWorlds.Count == 0)
            return;

        Dictionary<long, Matrix4x4> worlds = [];

        foreach (long modelId in importer.ModelOrder)
        {
            long		parentId	= importer.GetParentId(modelId);
            Matrix4x4	parentWorld	= worlds.TryGetValue(parentId, out Matrix4x4 world) ? world : importer.GetWorldMatrix(parentId);

            if (!bindWorlds.TryGetValue(modelId, out Matrix4x4 bindWorld))
            {
                if (worlds.ContainsKey(parentId))
                    worlds[modelId] = importer.GetNode(modelId)!.GetBindLocalMatrix() * parentWorld;

                continue;
            }

            worlds[modelId] = bindWorld;

            SceneNode	node	= importer.GetNode(modelId)!;
            Matrix4x4	local	= Matrix4x4.Invert(parentWorld, out Matrix4x4 parentInverse) ? bindWorld * parentInverse : bindWorld;

            FbxTransform.Decompose(local, out Vector3 translation, out Quaternion rotation, out Vector3 scale);

            if (IsSame(node.BindTransform, translation, rotation, scale))
                continue;

            node.LiveTransform.LocalPosition	= node.BindTransform.LocalPosition;
            node.LiveTransform.LocalRotation	= node.BindTransform.LocalRotation;
            node.LiveTransform.Scale			= node.BindTransform.Scale;
            node.BindTransform.LocalPosition	= translation;
            node.BindTransform.LocalRotation	= rotation;
            node.BindTransform.Scale			= scale;
        }
    }

    private static bool IsSame(Transform transform, Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Vector3		currentTranslation	= transform.LocalPosition ?? Vector3.Zero;
        Quaternion	currentRotation		= transform.LocalRotation ?? Quaternion.Identity;
        Vector3		currentScale		= transform.Scale ?? Vector3.One;
        float		tolerance			= 1e-4f * MathF.Max(1.0f, currentTranslation.Length());

        return Vector3.Distance(currentTranslation, translation) <= tolerance && MathF.Abs(Quaternion.Dot(currentRotation, rotation)) >= 0.99999f && Vector3.Distance(currentScale, scale) <= 1e-4f;
    }

    private static Matrix4x4? ReadMatrix(FbxNode node, string name)
    {
        double[] values = node.FirstChild(name)?.GetDoubles() ?? [];

        if (values.Length < 16)
            return null;

        return new Matrix4x4(
            (float)values[0], (float)values[1], (float)values[2], (float)values[3],
            (float)values[4], (float)values[5], (float)values[6], (float)values[7],
            (float)values[8], (float)values[9], (float)values[10], (float)values[11],
            (float)values[12], (float)values[13], (float)values[14], (float)values[15]);
    }
}
