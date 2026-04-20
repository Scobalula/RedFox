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
        => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None));

    /// <summary>
    /// Serialises the selected scene view to the output stream in MD5 mesh format.
    /// </summary>
    /// <param name="selection">The filtered scene selection to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the selection contains no meshes or no skeleton bones.
    /// </exception>
    public void Write(SceneTranslationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var meshes = selection.GetDescendants<Mesh>();
        var allBones = selection.GetDescendants<SkeletonBone>();
        SceneNode[] exportedBoneNodes = Array.ConvertAll(allBones, static bone => (SceneNode)bone);

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
        WriteJoints(writer, allBones, exportedBoneNodes, worldPositions, worldOrientations);

        foreach (var mesh in meshes)
            WriteMesh(writer, mesh, allBones, boneIndexMap, worldPositions, worldOrientations, selection);

        writer.Flush();
    }

    /// <summary>
    /// Writes the <c>joints { }</c> section with object-space bind-pose transforms.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="bones">The ordered bone array.</param>
    /// <param name="exportedBoneNodes">The ordered exported bones as scene nodes.</param>
    /// <param name="worldPositions">Pre-computed world-space positions.</param>
    /// <param name="worldOrientations">Pre-computed world-space orientations.</param>
    public static void WriteJoints(StreamWriter writer, SkeletonBone[] bones, SceneNode[] exportedBoneNodes, Vector3[] worldPositions, Quaternion[] worldOrientations)
    {
        writer.WriteLine();
        writer.WriteLine("joints {");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = SceneNode.GetBestParentIndex(bones[i], exportedBoneNodes);

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
    /// <param name="selection">The filtered scene selection being exported.</param>
    public static void WriteMesh(StreamWriter writer, Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap, Vector3[] worldPositions, Quaternion[] worldOrientations, SceneTranslationSelection selection)
    {
        if (mesh.Positions is null || mesh.FaceIndices is null)
            return;

        string shader = ResolveShaderName(mesh, selection);

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
                    if ((uint)localIdx >= (uint)globalBoneTable.Length)
                    {
                        throw new InvalidDataException(
                            $"Cannot write MD5 mesh: mesh '{mesh.Name}' contains skin index {localIdx} outside the exported skin table.");
                    }

                    int globalIdx = globalBoneTable[localIdx];

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

        List<string> missingBones = [];
        var table = new int[skinnedBones.Count];
        for (int i = 0; i < skinnedBones.Count; i++)
        {
            if (!boneIndexMap.TryGetValue(skinnedBones[i], out int globalIdx))
            {
                missingBones.Add(skinnedBones[i].Name);
                continue;
            }

            table[i] = globalIdx;
        }

        if (missingBones.Count > 0)
        {
            throw new InvalidDataException(
                $"Cannot write MD5 mesh: mesh '{mesh.Name}' references skinned bones that are not included in the export selection: {string.Join(", ", missingBones)}.");
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
            _ = boneIndexMap;
            worldPositions[i] = bones[i].GetBindWorldPosition();
            worldOrientations[i] = Quaternion.Normalize(bones[i].GetBindWorldRotation());
        }
    }

    private static string ResolveShaderName(Mesh mesh, SceneTranslationSelection selection)
    {
        if (mesh.Materials is { Count: > 0 } mats)
        {
            Material material = mats[0];
            if (!selection.Includes(material))
            {
                throw new InvalidDataException(
                    $"Cannot write MD5 mesh: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");
            }

            if (!string.IsNullOrWhiteSpace(material.Name))
                return material.Name;
        }

        return string.IsNullOrWhiteSpace(mesh.Name) ? "default" : mesh.Name;
    }

    /// <summary>
    /// Formats a float value for MD5 file output.
    /// </summary>
    /// <param name="v">The value to format.</param>
    /// <returns>An invariant-culture numeric string.</returns>
    public static string F(float v) => Md5Format.F(v);
}
