using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Writes scene data as id Tech 4 MD5 mesh (<c>.md5mesh</c>) text files.
/// <para>
/// The writer serialises a skeleton hierarchy and one or more skinned meshes into the
/// MD5 mesh format.  Vertex positions are decomposed back into joint-local weight offsets
/// using the bind-pose skeleton.  Each mesh produces a <c>mesh { }</c> block with a
/// shader name, vertex definitions, triangle indices, and a weight table.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="Md5MeshWriter"/>.
/// </remarks>
/// <param name="stream">The output stream to write MD5 mesh text data to.</param>
/// <param name="name">The logical scene name or destination file name.</param>
/// <param name="options">Options that control translator behaviour.</param>
public sealed class Md5MeshWriter(Stream stream, string name, SceneTranslatorOptions options)
{
    private readonly Stream _stream = stream;
    private readonly string _name = name;
    private readonly SceneTranslatorOptions _options = options;

    /// <summary>
    /// Serialises the scene to the output stream in MD5 mesh format.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene contains no meshes or no skeleton bones.
    /// </exception>
    public void Write(Scene scene)
    {
        var meshes = scene.RootNode.GetDescendants<Mesh>(SceneNodeFlags.NoExport);
        var allBones = scene.RootNode.GetDescendants<SkeletonBone>(SceneNodeFlags.NoExport);

        if (meshes.Length == 0)
            throw new InvalidOperationException("Scene must contain at least one mesh to write as MD5 mesh.");
        if (allBones.Length == 0)
            throw new InvalidOperationException("Scene must contain a skeleton to write as MD5 mesh.");

        using var writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        writer.WriteLine("MD5Version 10");
        writer.WriteLine($"commandline \"\"");
        writer.NewLine = "\n";

        writer.WriteLine();
        writer.WriteLine($"numJoints {allBones.Length}");
        writer.WriteLine($"numMeshes {meshes.Length}");

        // Compute world-space bind transforms for all bones
        var worldPositions = new Vector3[allBones.Length];
        var worldOrientations = new Quaternion[allBones.Length];
        var boneIndexMap = BuildBoneIndexMap(allBones);

        ComputeWorldBindTransforms(allBones, boneIndexMap, worldPositions, worldOrientations);
        WriteJoints(writer, allBones, boneIndexMap, worldPositions, worldOrientations);

        foreach (var mesh in meshes)
            WriteMesh(writer, mesh, allBones, boneIndexMap, worldPositions, worldOrientations);

        writer.Flush();
    }

    /// <summary>
    /// Writes the <c>joints { }</c> section with object-space bind-pose transforms.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="bones">The ordered bone array.</param>
    /// <param name="boneIndexMap">Maps each bone to its index.</param>
    /// <param name="worldPositions">Pre-computed world-space positions.</param>
    /// <param name="worldOrientations">Pre-computed world-space orientations.</param>
    public static void WriteJoints(StreamWriter writer, SkeletonBone[] bones, Dictionary<SkeletonBone, int> boneIndexMap, Vector3[] worldPositions, Quaternion[] worldOrientations)
    {
        writer.WriteLine();
        writer.WriteLine("joints {");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = bones[i].Parent is SkeletonBone parentBone && boneIndexMap.TryGetValue(parentBone, out int pi) ? pi : -1;

            var pos = worldPositions[i];
            var q = worldOrientations[i];

            string parentComment = parentIdx >= 0 ? bones[parentIdx].Name : string.Empty;
            writer.Write($"\t\"{bones[i].Name}\"\t{parentIdx} ( {F(pos.X)} {F(pos.Y)} {F(pos.Z)} ) ( {F(q.X)} {F(q.Y)} {F(q.Z)} )");
            if (!string.IsNullOrEmpty(parentComment))
                writer.Write($"\t\t// {parentComment}");
            writer.WriteLine();
        }
        writer.WriteLine("}");
    }

    /// <summary>
    /// Writes one <c>mesh { }</c> section for the given mesh.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="allBones">The full skeleton bone array.</param>
    /// <param name="boneIndexMap">The global bone index lookup.</param>
    /// <param name="worldPositions">Pre-computed world-space joint positions.</param>
    /// <param name="worldOrientations">Pre-computed world-space joint orientations.</param>
    public static void WriteMesh(StreamWriter writer, Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap, Vector3[] worldPositions, Quaternion[] worldOrientations)
    {
        if (mesh.Positions is null || mesh.FaceIndices is null)
            return;

        string shader = mesh.Materials is { Count: > 0 } mats && !string.IsNullOrWhiteSpace(mats[0].Name)
            ? mats[0].Name
            : (string.IsNullOrWhiteSpace(mesh.Name) ? "default" : mesh.Name);

        int vertCount = mesh.Positions.ElementCount;
        int triCount = mesh.FaceIndices.ElementCount / 3;
        int influences = mesh.BoneIndices?.ValueCount ?? 0;
        int[] globalBoneTable = BuildGlobalBoneIndexTable(mesh, allBones, boneIndexMap);

        // Build weights list — for each vertex, decompose position back to joint-local space
        var vertexWeights = new List<(int JointIndex, float Bias, Vector3 Position)>();
        var vertInfos = new (int WeightIndex, int WeightCount)[vertCount];

        for (int v = 0; v < vertCount; v++)
        {
            int weightStart = vertexWeights.Count;
            var vertPos = new Vector3(
                mesh.Positions.Get<float>(v, 0, 0),
                mesh.Positions.Get<float>(v, 0, 1),
                mesh.Positions.Get<float>(v, 0, 2));

            int addedWeights = 0;
            if (influences > 0 && mesh.BoneWeights is not null && mesh.BoneIndices is not null)
            {
                for (int j = 0; j < influences; j++)
                {
                    float weight = mesh.BoneWeights.Get<float>(v, j, 0);
                    if (weight <= 0f) continue;
                    int localIdx = mesh.BoneIndices.Get<int>(v, j, 0);
                    int globalIdx = (uint)localIdx < (uint)globalBoneTable.Length ? globalBoneTable[localIdx] : 0;

                    // Decompose: weight.Position = Conjugate(joint.Orientation) * (vertPos - joint.Position)
                    // But since bias weights the contribution, we need to find the local pos that produces the right result
                    var invRot = Quaternion.Conjugate(worldOrientations[globalIdx]);
                    var localPos = Vector3.Transform(vertPos - worldPositions[globalIdx], invRot);

                    vertexWeights.Add((globalIdx, weight, localPos));
                    addedWeights++;
                }
            }

            if (addedWeights == 0)
            {
                // Assign full weight to first bone
                vertexWeights.Add((0, 1.0f, Vector3.Transform(vertPos - worldPositions[0], Quaternion.Conjugate(worldOrientations[0]))));
                addedWeights = 1;
            }

            vertInfos[v] = (weightStart, addedWeights);
        }

        writer.WriteLine();
        writer.WriteLine("mesh {");
        writer.WriteLine($"\tshader \"{shader}\"");
        writer.WriteLine();

        // Vertices
        writer.WriteLine($"\tnumverts {vertCount}");
        for (int v = 0; v < vertCount; v++)
        {
            float u = mesh.UVLayers is not null ? mesh.UVLayers.Get<float>(v, 0, 0) : 0f;
            float uv = mesh.UVLayers is not null ? mesh.UVLayers.Get<float>(v, 0, 1) : 0f;
            writer.WriteLine($"\tvert {v} ( {F(u)} {F(uv)} ) {vertInfos[v].WeightIndex} {vertInfos[v].WeightCount}");
        }

        writer.WriteLine();

        // Triangles
        writer.WriteLine($"\tnumtris {triCount}");
        for (int t = 0; t < triCount; t++)
        {
            int i0 = mesh.FaceIndices.Get<int>(t * 3, 0, 0);
            int i1 = mesh.FaceIndices.Get<int>(t * 3 + 1, 0, 0);
            int i2 = mesh.FaceIndices.Get<int>(t * 3 + 2, 0, 0);
            writer.WriteLine($"\ttri {t} {i0} {i1} {i2}");
        }

        writer.WriteLine();

        // Weights
        writer.WriteLine($"\tnumweights {vertexWeights.Count}");
        for (int w = 0; w < vertexWeights.Count; w++)
        {
            var (jointIdx, bias, pos) = vertexWeights[w];
            writer.WriteLine($"\tweight {w} {jointIdx} {F(bias)} ( {F(pos.X)} {F(pos.Y)} {F(pos.Z)} )");
        }

        writer.WriteLine("}");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a lookup from bone reference to global bone index.
    /// </summary>
    /// <param name="bones">The ordered bone array.</param>
    /// <returns>A dictionary mapping each bone to its index.</returns>
    public static Dictionary<SkeletonBone, int> BuildBoneIndexMap(SkeletonBone[] bones)
    {
        var map = new Dictionary<SkeletonBone, int>(bones.Length);
        for (int i = 0; i < bones.Length; i++)
            map[bones[i]] = i;
        return map;
    }

    /// <summary>
    /// Builds a local-to-global bone index table for one skinned mesh.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="allBones">The full skeleton bone array.</param>
    /// <param name="boneIndexMap">The global bone index lookup.</param>
    /// <returns>A local index to global index mapping array.</returns>
    public static int[] BuildGlobalBoneIndexTable(Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap)
    {
        var skinnedBones = mesh.SkinnedBones;
        if (skinnedBones is null || skinnedBones.Count == 0)
            return [];

        var table = new int[skinnedBones.Count];
        for (int i = 0; i < skinnedBones.Count; i++)
        {
            if (!boneIndexMap.TryGetValue(skinnedBones[i], out int globalIdx))
                globalIdx = 0;
            table[i] = globalIdx;
        }
        return table;
    }

    /// <summary>
    /// Computes world-space bind transforms for all bones by traversing the hierarchy.
    /// </summary>
    /// <param name="bones">The ordered bone array.</param>
    /// <param name="boneIndexMap">Maps each bone to its index.</param>
    /// <param name="worldPositions">Output array for world-space positions.</param>
    /// <param name="worldOrientations">Output array for world-space orientations.</param>
    public static void ComputeWorldBindTransforms(SkeletonBone[] bones, Dictionary<SkeletonBone, int> boneIndexMap, Vector3[] worldPositions, Quaternion[] worldOrientations)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            var localPos = bones[i].BindTransform.LocalPosition ?? Vector3.Zero;
            var localRot = Quaternion.Normalize(bones[i].BindTransform.LocalRotation ?? Quaternion.Identity);

            if (bones[i].Parent is SkeletonBone parentBone && boneIndexMap.TryGetValue(parentBone, out int pi))
            {
                worldOrientations[i] = Quaternion.Normalize(worldOrientations[pi] * localRot);
                worldPositions[i] = worldPositions[pi] + Vector3.Transform(localPos, worldOrientations[pi]);
            }
            else
            {
                worldPositions[i] = localPos;
                worldOrientations[i] = localRot;
            }
        }
    }

    /// <summary>
    /// Formats a float value for MD5 file output.
    /// </summary>
    /// <param name="v">The value to format.</param>
    /// <returns>An invariant-culture numeric string.</returns>
    public static string F(float v) => Md5Format.F(v);
}
