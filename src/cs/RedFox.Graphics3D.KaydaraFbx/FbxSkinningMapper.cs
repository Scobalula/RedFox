using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Maps FBX skinning deformers to and from RedFox skinning data.
/// </summary>
public static class FbxSkinningMapper
{
    /// <summary>
    /// Imports skinning deformers into mesh skin palettes and influence buffers.
    /// </summary>
    /// <param name="meshesByModelId">Mesh map keyed by FBX model id.</param>
    /// <param name="objectsById">Object map keyed by FBX object id.</param>
    /// <param name="connections">FBX connection list.</param>
    /// <param name="bonesByModelId">Bone map keyed by FBX model id.</param>
    public static void ImportSkinning(Dictionary<long, Mesh> meshesByModelId, Dictionary<long, FbxNode> objectsById, IReadOnlyList<FbxConnection> connections, Dictionary<long, SkeletonBone> bonesByModelId)
    {
        Dictionary<long, long> skinToGeometry = [];
        Dictionary<long, List<long>> skinToClusters = [];
        Dictionary<long, long> clusterToBone = [];

        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal))
            {
                continue;
            }

            if (objectsById.TryGetValue(connection.ChildId, out FbxNode? childNode)
                && string.Equals(childNode.Name, "Deformer", StringComparison.Ordinal)
                && childNode.Properties.Count > 2)
            {
                string deformerType = childNode.Properties[2].AsString();
                if (string.Equals(deformerType, "Skin", StringComparison.OrdinalIgnoreCase))
                {
                    skinToGeometry[connection.ChildId] = connection.ParentId;
                    continue;
                }

                if (string.Equals(deformerType, "Cluster", StringComparison.OrdinalIgnoreCase)
                    && objectsById.TryGetValue(connection.ParentId, out FbxNode? parentNode)
                    && string.Equals(parentNode.Name, "Deformer", StringComparison.Ordinal)
                    && parentNode.Properties.Count > 2
                    && string.Equals(parentNode.Properties[2].AsString(), "Skin", StringComparison.OrdinalIgnoreCase))
                {
                    if (!skinToClusters.TryGetValue(connection.ParentId, out List<long>? clusters))
                    {
                        clusters = [];
                        skinToClusters[connection.ParentId] = clusters;
                    }

                    clusters.Add(connection.ChildId);
                    continue;
                }
            }

            if (objectsById.TryGetValue(connection.ChildId, out FbxNode? clusterNode)
                && string.Equals(clusterNode.Name, "Deformer", StringComparison.Ordinal)
                && clusterNode.Properties.Count > 2
                && string.Equals(clusterNode.Properties[2].AsString(), "Cluster", StringComparison.OrdinalIgnoreCase)
                && bonesByModelId.ContainsKey(connection.ParentId))
            {
                clusterToBone[connection.ChildId] = connection.ParentId;
                continue;
            }

            if (bonesByModelId.ContainsKey(connection.ChildId)
                && objectsById.TryGetValue(connection.ParentId, out FbxNode? reverseClusterNode)
                && string.Equals(reverseClusterNode.Name, "Deformer", StringComparison.Ordinal)
                && reverseClusterNode.Properties.Count > 2
                && string.Equals(reverseClusterNode.Properties[2].AsString(), "Cluster", StringComparison.OrdinalIgnoreCase))
            {
                clusterToBone[connection.ParentId] = connection.ChildId;
            }
        }

        foreach ((long skinId, long geometryId) in skinToGeometry)
        {
            if (!skinToClusters.TryGetValue(skinId, out List<long>? clusterIds) || clusterIds.Count == 0)
            {
                continue;
            }

            long meshModelId = ResolveGeometryOwnerModelId(geometryId, connections);
            if (!meshesByModelId.TryGetValue(meshModelId, out Mesh? mesh) || mesh.VertexCount == 0)
            {
                continue;
            }

            List<SkeletonBone> palette = [];
            List<Matrix4x4> inverseBindMatrices = [];
            Dictionary<int, List<(int PaletteIndex, float Weight)>> vertexInfluences = [];

            for (int clusterIndex = 0; clusterIndex < clusterIds.Count; clusterIndex++)
            {
                long clusterId = clusterIds[clusterIndex];
                if (!clusterToBone.TryGetValue(clusterId, out long boneModelId) || !bonesByModelId.TryGetValue(boneModelId, out SkeletonBone? bone))
                {
                    continue;
                }

                if (!objectsById.TryGetValue(clusterId, out FbxNode? clusterNode))
                {
                    continue;
                }

                int paletteIndex = palette.Count;
                palette.Add(bone);
                inverseBindMatrices.Add(ReadClusterInverseBindMatrix(clusterNode));

                int[] indices = FbxSceneMapper.GetNodeArray<int>(clusterNode, "Indexes");
                double[] weights = FbxSceneMapper.GetNodeArray<double>(clusterNode, "Weights");
                int influenceCount = Math.Min(indices.Length, weights.Length);

                for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
                {
                    int vertexIndex = indices[influenceIndex];
                    if ((uint)vertexIndex >= (uint)mesh.VertexCount)
                    {
                        continue;
                    }

                    float weight = (float)weights[influenceIndex];
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    if (!vertexInfluences.TryGetValue(vertexIndex, out List<(int PaletteIndex, float Weight)>? influences))
                    {
                        influences = [];
                        vertexInfluences[vertexIndex] = influences;
                    }

                    influences.Add((paletteIndex, weight));
                }
            }

            if (palette.Count == 0 || vertexInfluences.Count == 0)
            {
                continue;
            }

            int maxInfluenceCount = 1;
            foreach (List<(int PaletteIndex, float Weight)> influences in vertexInfluences.Values)
            {
                maxInfluenceCount = Math.Max(maxInfluenceCount, influences.Count);
            }

            ushort[] boneIndices = new ushort[mesh.VertexCount * maxInfluenceCount];
            float[] boneWeights = new float[mesh.VertexCount * maxInfluenceCount];

            foreach ((int vertexIndex, List<(int PaletteIndex, float Weight)> influences) in vertexInfluences)
            {
                influences.Sort(static (left, right) => right.Weight.CompareTo(left.Weight));
                int writeCount = Math.Min(maxInfluenceCount, influences.Count);

                float totalWeight = 0f;
                for (int i = 0; i < writeCount; i++)
                {
                    totalWeight += influences[i].Weight;
                }

                if (totalWeight <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < writeCount; i++)
                {
                    int targetIndex = (vertexIndex * maxInfluenceCount) + i;
                    boneIndices[targetIndex] = (ushort)influences[i].PaletteIndex;
                    boneWeights[targetIndex] = influences[i].Weight / totalWeight;
                }
            }

            mesh.BoneIndices = new DataBuffer<ushort>(boneIndices, maxInfluenceCount, 1);
            mesh.BoneWeights = new DataBuffer<float>(boneWeights, maxInfluenceCount, 1);
            mesh.SetSkinBinding(palette, inverseBindMatrices);
        }
    }

    /// <summary>
    /// Exports mesh skinning data into FBX skin and cluster deformers.
    /// </summary>
    /// <param name="objectsNode">The FBX Objects node.</param>
    /// <param name="connectionsNode">The FBX Connections node.</param>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="geometryId">The target geometry object id.</param>
    /// <param name="boneIds">Bone map keyed by skeleton bone.</param>
    /// <param name="nextId">The mutable object id counter.</param>
    public static void ExportSkinning(FbxNode objectsNode, FbxNode connectionsNode, Mesh mesh, long geometryId, Dictionary<SkeletonBone, long> boneIds, ref long nextId)
    {
        if (mesh.BoneIndices is null || mesh.BoneWeights is null || mesh.SkinnedBones is null)
        {
            return;
        }

        long skinId = nextId++;
        FbxNode skin = new("Deformer");
        skin.Properties.Add(new FbxProperty('L', skinId));
        skin.Properties.Add(new FbxProperty('S', mesh.Name + "_Skin\0\u0001Deformer"));
        skin.Properties.Add(new FbxProperty('S', "Skin"));
        skin.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 101) } });
        skin.Children.Add(new FbxNode("Link_DeformAcuracy") { Properties = { new FbxProperty('D', 50.0) } });
        objectsNode.Children.Add(skin);

        FbxSceneMapper.AddConnection(connectionsNode, "OO", skinId, geometryId);

        int skinnedVertexCount = Math.Min(mesh.VertexCount, Math.Min(mesh.BoneIndices.ElementCount, mesh.BoneWeights.ElementCount));

        for (int paletteIndex = 0; paletteIndex < mesh.SkinnedBones.Count; paletteIndex++)
        {
            SkeletonBone bone = mesh.SkinnedBones[paletteIndex];
            if (!boneIds.TryGetValue(bone, out long boneModelId))
            {
                continue;
            }

            List<int> vertexIndices = [];
            List<double> vertexWeights = [];

            for (int vertexIndex = 0; vertexIndex < skinnedVertexCount; vertexIndex++)
            {
                for (int influenceIndex = 0; influenceIndex < mesh.SkinInfluenceCount; influenceIndex++)
                {
                    int sourcePaletteIndex = mesh.BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                    if (sourcePaletteIndex != paletteIndex)
                    {
                        continue;
                    }

                    float weight = mesh.BoneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    vertexIndices.Add(vertexIndex);
                    vertexWeights.Add(weight);
                }
            }

            long clusterId = nextId++;
            FbxNode cluster = new("Deformer");
            cluster.Properties.Add(new FbxProperty('L', clusterId));
            cluster.Properties.Add(new FbxProperty('S', bone.Name + "_Cluster\0\u0001Deformer"));
            cluster.Properties.Add(new FbxProperty('S', "Cluster"));
            cluster.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });
            cluster.Children.Add(new FbxNode("UserData")
            {
                Properties = { new FbxProperty('S', string.Empty), new FbxProperty('S', string.Empty) },
            });
            cluster.Children.Add(new FbxNode("Indexes") { Properties = { new FbxProperty('i', vertexIndices.ToArray()) } });
            cluster.Children.Add(new FbxNode("Weights") { Properties = { new FbxProperty('d', vertexWeights.ToArray()) } });

            Matrix4x4 inverseBind = Matrix4x4.Identity;
            if (mesh.InverseBindMatrices is { Count: > 0 } inverseBindMatrices && paletteIndex < inverseBindMatrices.Count)
            {
                inverseBind = inverseBindMatrices[paletteIndex];
            }

            Matrix4x4 meshBindWorld = mesh.GetBindWorldMatrix();
            Matrix4x4 boneBindWorld = Matrix4x4.Invert(inverseBind, out Matrix4x4 inverted) ? inverted : bone.GetBindWorldMatrix();
            Matrix4x4 armatureBindWorld = GetArmatureBindWorldMatrix(bone);
            Matrix4x4 boneBindInverse = Matrix4x4.Invert(boneBindWorld, out Matrix4x4 boneInverse) ? boneInverse : Matrix4x4.Identity;
            Matrix4x4 transformLink = boneBindWorld;
            Matrix4x4 transform = boneBindInverse * meshBindWorld;

            cluster.Children.Add(new FbxNode("TransformLink")
            {
                Properties = { new FbxProperty('d', MatrixToArray(transformLink)) },
            });

            cluster.Children.Add(new FbxNode("Transform")
            {
                Properties = { new FbxProperty('d', MatrixToArray(transform)) },
            });

            cluster.Children.Add(new FbxNode("TransformAssociateModel")
            {
                Properties = { new FbxProperty('d', MatrixToArray(armatureBindWorld)) },
            });

            cluster.Children.Add(new FbxNode("Mode") { Properties = { new FbxProperty('S', "Normalize") } });

            objectsNode.Children.Add(cluster);

            FbxSceneMapper.AddConnection(connectionsNode, "OO", clusterId, skinId);
            FbxSceneMapper.AddConnection(connectionsNode, "OO", boneModelId, clusterId);
        }
    }

    /// <summary>
    /// Resolves the model identifier of the mesh that owns a given geometry object.
    /// </summary>
    /// <param name="geometryId">The geometry object identifier.</param>
    /// <param name="connections">The full FBX connection list.</param>
    /// <returns>The model identifier of the owning mesh, or <c>-1</c> when not found.</returns>
    public static long ResolveGeometryOwnerModelId(long geometryId, IReadOnlyList<FbxConnection> connections)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];

            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal))
                continue;
            if (connection.ChildId != geometryId)
                continue;
            
            return connection.ParentId;
        }

        return -1;
    }

    /// <summary>
    /// Reads the inverse bind matrix from a Cluster deformer node.
    /// </summary>
    /// <param name="clusterNode">The Cluster FBX node.</param>
    /// <returns>The inverse bind matrix for use in runtime skinning.</returns>
    public static Matrix4x4 ReadClusterInverseBindMatrix(FbxNode clusterNode)
    {
        Matrix4x4 transformLink = ReadNodeMatrix(clusterNode, "TransformLink");
        Matrix4x4 transform = ReadNodeMatrix(clusterNode, "Transform");

        if (!Matrix4x4.Invert(transformLink, out Matrix4x4 inverseLink))
        {
            return Matrix4x4.Identity;
        }

        return inverseLink * transform;
    }

    /// <summary>
    /// Reads a 4×4 matrix stored as a flat double array in a named child node.
    /// </summary>
    /// <param name="parent">The parent FBX node.</param>
    /// <param name="childName">The child node name that holds the double array property.</param>
    /// <returns>The parsed matrix, or <see cref="Matrix4x4.Identity"/> when not found or incomplete.</returns>
    public static Matrix4x4 ReadNodeMatrix(FbxNode parent, string childName)
    {
        double[] values = FbxSceneMapper.GetNodeArray<double>(parent, childName);
        if (values.Length < 16)
        {
            return Matrix4x4.Identity;
        }

        return new Matrix4x4(
            (float)values[0],   (float)values[1],   (float)values[2],   (float)values[3],
            (float)values[4],   (float)values[5],   (float)values[6],   (float)values[7],
            (float)values[8],   (float)values[9],   (float)values[10],  (float)values[11],
            (float)values[12],  (float)values[13],  (float)values[14],  (float)values[15]);
    }

    /// <summary>
    /// Returns the world bind matrix of the skeleton (armature) that owns the specified bone.
    /// </summary>
    /// <param name="bone">The bone whose armature bind matrix is needed.</param>
    /// <returns>The armature world bind matrix, or <see cref="Matrix4x4.Identity"/> when not found.</returns>
    public static Matrix4x4 GetArmatureBindWorldMatrix(SkeletonBone bone)
    {
        SceneNode? current = bone.Parent;
        while (current is not null)
        {
            if (current is Skeleton skeleton && current is not SkeletonBone)
            {
                return skeleton.GetBindWorldMatrix();
            }

            current = current.Parent;
        }

        return Matrix4x4.Identity;
    }

    /// <summary>
    /// Flattens a <see cref="Matrix4x4"/> into a row-major double array for FBX serialisation.
    /// </summary>
    /// <param name="matrix">The source matrix.</param>
    /// <returns>A sixteen-element double array in row-major order.</returns>
    public static double[] MatrixToArray(Matrix4x4 matrix)
    {
        return
        [
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        ];
    }
}
