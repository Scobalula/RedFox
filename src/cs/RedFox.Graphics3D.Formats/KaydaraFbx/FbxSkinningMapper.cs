using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

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

            if (objectsById.TryGetValue(connection.ChildId, out FbxNode? childNode))
            {
                if (IsDeformerOfType(childNode, "Skin"))
                {
                    skinToGeometry[connection.ChildId] = connection.ParentId;
                    continue;
                }

                if (IsDeformerOfType(childNode, "Cluster"))
                {
                    if (objectsById.TryGetValue(connection.ParentId, out FbxNode? parentNode) && IsDeformerOfType(parentNode, "Skin"))
                    {
                        if (!skinToClusters.TryGetValue(connection.ParentId, out List<long>? clusters))
                        {
                            clusters = [];
                            skinToClusters[connection.ParentId] = clusters;
                        }

                        clusters.Add(connection.ChildId);
                        continue;
                    }

                    if (bonesByModelId.ContainsKey(connection.ParentId))
                    {
                        clusterToBone[connection.ChildId] = connection.ParentId;
                        continue;
                    }
                }
            }

            if (bonesByModelId.ContainsKey(connection.ChildId)
                && objectsById.TryGetValue(connection.ParentId, out FbxNode? reverseClusterNode)
                && IsDeformerOfType(reverseClusterNode, "Cluster"))
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
            List<(int VertexIndex, int PaletteIndex, float Weight)> allInfluences = [];

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

                    allInfluences.Add((vertexIndex, paletteIndex, weight));
                }
            }

            if (palette.Count == 0 || allInfluences.Count == 0)
            {
                continue;
            }

            int[] influenceCounts = new int[mesh.VertexCount];
            foreach (var influence in allInfluences)
                influenceCounts[influence.VertexIndex]++;

            int[] offsets = new int[mesh.VertexCount + 1];
            for (int vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++)
                offsets[vertexIndex + 1] = offsets[vertexIndex] + influenceCounts[vertexIndex];

            int[] nextOffsets = new int[mesh.VertexCount];
            offsets.AsSpan(0, mesh.VertexCount).CopyTo(nextOffsets);
            (int PaletteIndex, float Weight)[] vertexInfluences = new (int PaletteIndex, float Weight)[allInfluences.Count];
            foreach ((int vertexIndex, int paletteIndex, float weight) in allInfluences)
                vertexInfluences[nextOffsets[vertexIndex]++] = (paletteIndex, weight);

            int maxInfluenceCount = 1;
            for (int vertexIndex = 0; vertexIndex < influenceCounts.Length; vertexIndex++)
                maxInfluenceCount = Math.Max(maxInfluenceCount, influenceCounts[vertexIndex]);

            ushort[] boneIndices = new ushort[mesh.VertexCount * maxInfluenceCount];
            float[] boneWeights = new float[mesh.VertexCount * maxInfluenceCount];

            for (int vertexIndex = 0; vertexIndex < influenceCounts.Length; vertexIndex++)
            {
                int influenceCount = influenceCounts[vertexIndex];
                if (influenceCount == 0)
                    continue;

                int start = offsets[vertexIndex];
                Array.Sort(vertexInfluences, start, influenceCount, Comparer<(int PaletteIndex, float Weight)>.Create(static (left, right) => right.Weight.CompareTo(left.Weight)));

                float totalWeight = 0f;
                for (int i = 0; i < influenceCount; i++)
                {
                    totalWeight += vertexInfluences[start + i].Weight;
                }

                if (totalWeight <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < influenceCount; i++)
                {
                    int targetIndex = (vertexIndex * maxInfluenceCount) + i;
                    boneIndices[targetIndex] = (ushort)vertexInfluences[start + i].PaletteIndex;
                    boneWeights[targetIndex] = vertexInfluences[start + i].Weight / totalWeight;
                }
            }

            mesh.Skin = new Skin(palette, new DataBuffer<ushort>(boneIndices, maxInfluenceCount, 1), new DataBuffer<float>(boneWeights, maxInfluenceCount, 1), inverseBindMatrices) { Name = FbxSceneMapper.GetNodeObjectName(objectsById[skinId]) };
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
        if (mesh.Skin is not { } meshSkin)
        {
            return;
        }

        long skinId = nextId++;
        FbxNode skin = new("Deformer");
        skin.Properties.Add(new FbxProperty('L', skinId));
        skin.Properties.Add(new FbxProperty('S', (meshSkin.Name ?? mesh.Name + "_Skin") + "\0\u0001Deformer"));
        skin.Properties.Add(new FbxProperty('S', "Skin"));
        skin.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 101) } });
        skin.Children.Add(new FbxNode("Link_DeformAcuracy") { Properties = { new FbxProperty('D', 50.0) } });
        objectsNode.Children.Add(skin);

        FbxSceneMapper.AddConnection(connectionsNode, "OO", skinId, geometryId);

        int skinnedVertexCount = Math.Min(mesh.VertexCount, Math.Min(meshSkin.BoneIndices.ElementCount, meshSkin.BoneWeights.ElementCount));
        int influenceCount = meshSkin.InfluenceCount;

        // Single pass over all vertices to collect per-bone influences.
        Dictionary<int, (List<int> Indices, List<double> Weights)> boneInfluences = [];

        for (int vertexIndex = 0; vertexIndex < skinnedVertexCount; vertexIndex++)
        {
            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                int paletteIndex = meshSkin.BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                float weight = meshSkin.BoneWeights.Get<float>(vertexIndex, influenceIndex, 0);

                if (weight <= 0f)
                {
                    continue;
                }

                if (!boneInfluences.TryGetValue(paletteIndex, out (List<int> Indices, List<double> Weights) entry))
                {
                    entry = ([], []);
                    boneInfluences[paletteIndex] = entry;
                }

                entry.Indices.Add(vertexIndex);
                entry.Weights.Add(weight);
            }
        }

        // Pre-compute the mesh world matrix in export (Y-up) space.
        Matrix4x4 meshWorld = FbxSceneMapper.GetExportBindWorldMatrix(mesh);

        for (int paletteIndex = 0; paletteIndex < meshSkin.Bones.Count; paletteIndex++)
        {
            SkeletonBone bone = meshSkin.Bones[paletteIndex];

            if (!boneIds.TryGetValue(bone, out long boneModelId))
            {
                continue;
            }

            if (!boneInfluences.TryGetValue(paletteIndex, out (List<int> Indices, List<double> Weights) influences) || influences.Indices.Count == 0)
            {
                continue;
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
            cluster.Children.Add(new FbxNode("Indexes") { Properties = { new FbxProperty('i', influences.Indices.ToArray()) } });
            cluster.Children.Add(new FbxNode("Weights") { Properties = { new FbxProperty('d', influences.Weights.ToArray()) } });

            // FBX files store Transform in bone-local space, not global space.
            // See: http://area.autodesk.com/forum/autodesk-fbx/fbx-sdk
            //   Transform (stored) = TransformLink⁻¹ × MeshWorld  (bone-local mesh bind)
            //   TransformLink       = BoneWorld                    (bone global bind)
            //   TransformAssociateModel = ArmatureWorld            (armature global bind)
            Matrix4x4 boneWorld = FbxSceneMapper.GetExportBindWorldMatrix(bone);

            Matrix4x4.Invert(boneWorld, out Matrix4x4 boneWorldInverse);
            Matrix4x4 transform = meshWorld * boneWorldInverse;

            cluster.Children.Add(new FbxNode("Transform") { Properties = { new FbxProperty('d', MatrixToArray(transform)) } });
            cluster.Children.Add(new FbxNode("TransformLink") { Properties = { new FbxProperty('d', MatrixToArray(boneWorld)) } });
            cluster.Children.Add(new FbxNode("TransformAssociateModel")
            {
                Properties = { new FbxProperty('d', MatrixToArray(GetArmatureExportBindWorldMatrix(bone))) },
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
    /// Uses the row-vector convention <c>Transform × TransformLink⁻¹</c> which satisfies
    /// the engine skinning formula <c>v × IBM × boneActive</c>. For displaced meshes
    /// (multiple bind poses), these IBMs are preserved as-is. For non-displaced meshes,
    /// the strip phase may rebuild IBMs from the scene graph.
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

        return transform * inverseLink;
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

        return new Matrix4x4((float)values[0],   (float)values[1],   (float)values[2],   (float)values[3], (float)values[4],   (float)values[5],   (float)values[6],   (float)values[7], (float)values[8],   (float)values[9],   (float)values[10],  (float)values[11], (float)values[12],  (float)values[13],  (float)values[14],  (float)values[15]);
    }

    /// <summary>
    /// Returns the export-time world bind matrix of the skeleton (armature) that owns the specified bone,
    /// including any coordinate-system basis rotations added during FBX export.
    /// </summary>
    /// <param name="bone">The bone whose armature export bind world matrix is needed.</param>
    /// <returns>The armature export world bind matrix, or <see cref="Matrix4x4.Identity"/> when not found.</returns>
    public static Matrix4x4 GetArmatureExportBindWorldMatrix(SkeletonBone bone)
    {
        SceneNode? current = bone.Parent;
        while (current is not null)
        {
            if (current is Skeleton skeleton)
            {
                return FbxSceneMapper.GetExportBindWorldMatrix(skeleton);
            }

            if (current is SkeletonBone rootBone && rootBone.Parent is not SkeletonBone)
            {
                return FbxSceneMapper.GetExportBindWorldMatrix(rootBone);
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

    /// <summary>
    /// Returns whether the given FBX node is a Deformer of the specified sub-type.
    /// </summary>
    /// <param name="node">The FBX node to inspect.</param>
    /// <param name="deformerType">The expected deformer sub-type (e.g. "Skin" or "Cluster").</param>
    /// <returns><c>true</c> when the node is a Deformer with a matching sub-type property.</returns>
    private static bool IsDeformerOfType(FbxNode node, string deformerType) =>
        string.Equals(node.Name, "Deformer", StringComparison.Ordinal)
        && node.Properties.Count > 2
        && string.Equals(node.Properties[2].AsString(), deformerType, StringComparison.OrdinalIgnoreCase);

}
