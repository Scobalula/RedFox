using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.IdTech;

/// <summary>
/// Writes scene data as id Tech 4 MD5 mesh (<c>.md5mesh</c>) text files.
/// </summary>
public sealed class Md5MeshWriter
{
    private readonly Stream _stream;

    /// <summary>
    /// Creates an MD5 mesh writer.
    /// </summary>
    public Md5MeshWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
    }

    /// <summary>
    /// Serialises the scene to the output stream in MD5 mesh format.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene contains no meshes or no skeleton bones.
    /// </exception>
    public void Write(Scene scene) => Write(scene, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(Scene scene, CancellationToken token) => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None), token);

    /// <summary>
    /// Serialises the selected scene view to the output stream in MD5 mesh format.
    /// </summary>
    /// <param name="selection">The filtered scene selection to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the selection contains no meshes or no skeleton bones.
    /// </exception>
    public void Write(SceneTranslationSelection selection) => Write(selection, CancellationToken.None);

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
        int[] parentIndices = SkeletonExportTransforms.BuildExportedParentIndices(bones, exportedBoneNodes);
        writer.WriteLine();
        writer.WriteLine("joints {");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = parentIndices[i];

            var pos	= worldPositions[i];
            var q	= worldOrientations[i];
            if (q.W > 0f)
                q = new Quaternion(-q.X, -q.Y, -q.Z, -q.W);

            string parentComment = parentIdx >= 0 ? bones[parentIdx].Name : string.Empty;
            writer.Write($"\t\"{bones[i].Name}\"\t{parentIdx} ( {FormatFloat(pos.X)} {FormatFloat(pos.Y)} {FormatFloat(pos.Z)} ) ( {FormatFloat(q.X)} {FormatFloat(q.Y)} {FormatFloat(q.Z)} )");
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
    public static void WriteMesh(StreamWriter writer, Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap, Vector3[] worldPositions, Quaternion[] worldOrientations, SceneTranslationSelection selection) => WriteMesh(writer, mesh, allBones, boneIndexMap, worldPositions, worldOrientations, selection, CancellationToken.None);

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
        var skinnedBones = mesh.Skin?.Bones;
        if (skinnedBones is null || skinnedBones.Count == 0)
            return [];

        List<string> missingBones	= [];
        var table					= new int[skinnedBones.Count];
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
            throw new InvalidDataException($"Cannot write MD5 mesh: mesh '{mesh.Name}' references skinned bones that are not included in the export selection: {string.Join(", ", missingBones)}.");
        }

        return table;
    }

    /// <summary>
    /// Computes world-space bind transforms for all bones by traversing the hierarchy.
    /// </summary>
    /// <param name="bones">The ordered bone array.</param>
    /// <param name="worldPositions">Output array for world-space positions.</param>
    /// <param name="worldOrientations">Output array for world-space orientations.</param>
    public static void ComputeWorldBindTransforms(SkeletonBone[] bones, Vector3[] worldPositions, Quaternion[] worldOrientations)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            worldPositions[i]		= bones[i].GetBindWorldPosition();
            worldOrientations[i]	= Quaternion.Normalize(bones[i].GetBindWorldRotation());
        }
    }

    /// <summary>
    /// Formats a float value for MD5 file output.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>An invariant-culture numeric string.</returns>
    public static string FormatFloat(float value) => Md5Format.FormatFloat(value);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="selection">The selected scene data to write.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(SceneTranslationSelection selection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selection);

        var meshes						= selection.GetDescendants<Mesh>().Where(static mesh => mesh.Positions is not null && mesh.FaceIndices is not null).ToArray();
        var allBones					= selection.GetDescendants<SkeletonBone>();
        SceneNode[] exportedBoneNodes	= Array.ConvertAll(allBones, static bone => (SceneNode)bone);

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

        var worldPositions		= new Vector3[allBones.Length];
        var worldOrientations	= new Quaternion[allBones.Length];
        var boneIndexMap		= BuildBoneIndexMap(allBones);

        ComputeWorldBindTransforms(allBones, worldPositions, worldOrientations);
        WriteJoints(writer, allBones, exportedBoneNodes, worldPositions, worldOrientations);

        foreach (var mesh in meshes)
            WriteMesh(writer, mesh, allBones, boneIndexMap, worldPositions, worldOrientations, selection, token);

        writer.Flush();
    }

    private static void WriteMesh(StreamWriter writer, Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap, Vector3[] worldPositions, Quaternion[] worldOrientations, SceneTranslationSelection selection, CancellationToken token)
    {
        if (mesh.Positions is null || mesh.FaceIndices is null)
            return;

        string shader = ResolveShaderName(mesh, selection);

        int vertCount			= mesh.Positions.ElementCount;
        int triCount			= mesh.FaceIndices.ElementCount / 3;
        Skin? skin				= mesh.Skin;
        int influences			= skin?.BoneIndices.ValueCount ?? 0;
        int[] globalBoneTable	= BuildGlobalBoneIndexTable(mesh, allBones, boneIndexMap);

        var vertexWeights	= new List<(int JointIndex, float Bias, Vector3 Position)>();
        var vertInfos		= new (int WeightIndex, int WeightCount)[vertCount];

        for (int v = 0; v < vertCount; v++)
        {
            if ((v & 1023) == 0)
                token.ThrowIfCancellationRequested();
            int weightStart	= vertexWeights.Count;
            var vertPos		= new Vector3(mesh.Positions.Get<float>(v, 0, 0), mesh.Positions.Get<float>(v, 0, 1), mesh.Positions.Get<float>(v, 0, 2));

            int addedWeights = 0;
            if (skin is not null)
            {
                for (int j = 0; j < influences; j++)
                {
                    float weight = skin.BoneWeights.Get<float>(v, j, 0);
                    if (weight <= 0f) continue;
                    int localIdx = skin.BoneIndices.Get<int>(v, j, 0);
                    if ((uint)localIdx >= (uint)globalBoneTable.Length)
                    {
                        throw new InvalidDataException($"Cannot write MD5 mesh: mesh '{mesh.Name}' contains skin index {localIdx} outside the exported skin table.");
                    }

                    int globalIdx = globalBoneTable[localIdx];

                    var invRot		= Quaternion.Conjugate(worldOrientations[globalIdx]);
                    var localPos	= Vector3.Transform(vertPos - worldPositions[globalIdx], invRot);

                    vertexWeights.Add((globalIdx, weight, localPos));
                    addedWeights++;
                }
            }

            if (addedWeights == 0)
            {

                vertexWeights.Add((0, 1.0f, Vector3.Transform(vertPos - worldPositions[0], Quaternion.Conjugate(worldOrientations[0]))));
                addedWeights = 1;
            }

            vertInfos[v] = (weightStart, addedWeights);
        }

        writer.WriteLine();
        writer.WriteLine("mesh {");
        writer.WriteLine($"\tshader \"{shader}\"");
        writer.WriteLine();

        writer.WriteLine($"\tnumverts {vertCount}");
        for (int v = 0; v < vertCount; v++)
        {
            if ((v & 1023) == 0)
                token.ThrowIfCancellationRequested();
            float u		= mesh.UVLayers is not null ? mesh.UVLayers.Get<float>(v, 0, 0) : 0f;
            float uv	= mesh.UVLayers is not null ? mesh.UVLayers.Get<float>(v, 0, 1) : 0f;
            writer.WriteLine($"\tvert {v} ( {FormatFloat(u)} {FormatFloat(uv)} ) {vertInfos[v].WeightIndex} {vertInfos[v].WeightCount}");
        }

        writer.WriteLine();

        writer.WriteLine($"\tnumtris {triCount}");
        for (int t = 0; t < triCount; t++)
        {
            int i0 = mesh.FaceIndices.Get<int>(t * 3, 0, 0);
            int i1 = mesh.FaceIndices.Get<int>(t * 3 + 1, 0, 0);
            int i2 = mesh.FaceIndices.Get<int>(t * 3 + 2, 0, 0);
            writer.WriteLine($"\ttri {t} {i0} {i1} {i2}");
        }

        writer.WriteLine();

        writer.WriteLine($"\tnumweights {vertexWeights.Count}");
        for (int w = 0; w < vertexWeights.Count; w++)
        {
            var (jointIdx, bias, pos) = vertexWeights[w];
            writer.WriteLine($"\tweight {w} {jointIdx} {FormatFloat(bias)} ( {FormatFloat(pos.X)} {FormatFloat(pos.Y)} {FormatFloat(pos.Z)} )");
        }

        writer.WriteLine("}");
    }

    private static string ResolveShaderName(Mesh mesh, SceneTranslationSelection selection)
    {
        if (mesh.Materials is { Count: > 0 } mats)
        {
            Material material = mats[0];
            if (!selection.Includes(material))
            {
                throw new InvalidDataException($"Cannot write MD5 mesh: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");
            }

            if (!string.IsNullOrWhiteSpace(material.Name))
                return material.Name;
        }

        return string.IsNullOrWhiteSpace(mesh.Name) ? "default" : mesh.Name;
    }
}
