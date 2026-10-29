using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.StudioMDL;

/// <summary>
/// Writes scene data as Valve SMD (.smd) text files.
/// </summary>
public sealed class SmdWriter
{
    private readonly Stream	_stream;

    /// <summary>
    /// Initializes an SMD writer for the supplied destination stream and translation settings.
    /// </summary>
    /// <param name="stream">The stream that receives SMD text.</param>
    /// <param name="name">The source name used in translation metadata.</param>
    /// <param name="options">The options that control SMD export.</param>
    public SmdWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream		= stream;
    }

    /// <summary>
    /// Serialises the scene to the output stream in SMD format.
    /// </summary>
    /// <param name="scene">
    /// The scene to serialise.
    /// </param>
    public void Write(Scene scene) => Write(scene, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(Scene scene, CancellationToken token) => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None), token);

    /// <summary>
    /// Serialises the selected scene view to the output stream in SMD format.
    /// </summary>
    /// <param name="selection">
    /// The filtered scene selection to serialise.
    /// </param>
    public void Write(SceneTranslationSelection selection) => Write(selection, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="selection">The selected scene data to write.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(SceneTranslationSelection selection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selection);

        var	meshes		= selection.GetDescendants<Mesh>();
        var	skelAnim	= selection.TryGetFirstOfType<SkeletonAnimation>();

        bool	hasMeshes	= meshes.Length > 0;
        bool	hasAnim		= skelAnim is not null;

        if (!hasMeshes && !hasAnim)
            throw new InvalidOperationException("Scene must contain at least one mesh or a skeleton animation to be written as SMD.");

        using var writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        writer.WriteLine("version 1");

        var allBones = selection.GetDescendants<SkeletonBone>();
        if (hasMeshes && allBones.Length == 0)
            allBones = [new SkeletonBone("root")];
        SceneNode[] exportedBoneNodes = Array.ConvertAll(allBones, static bone => (SceneNode)bone);

        if (hasMeshes)
        {
            WriteNodes(writer, allBones, exportedBoneNodes);
            WriteBindPoseSkeleton(writer, allBones, exportedBoneNodes);
            WriteTriangles(writer, meshes, allBones, selection, token);
        }
        else
        {
            SkeletonAnimation animation			= skelAnim ?? throw new InvalidDataException("Missing SMD animation.");
            SkeletonBone[] bonesForAnim			= allBones.Length > 0 ? allBones : SynthesiseBonesFromAnimation(animation);
            SceneNode[] exportedAnimBoneNodes	= Array.ConvertAll(bonesForAnim, static bone => (SceneNode)bone);
            WriteNodes(writer, bonesForAnim, exportedAnimBoneNodes);
            WriteAnimationSkeleton(writer, animation, bonesForAnim, exportedAnimBoneNodes, token);
        }

        writer.Flush();
    }

    private static void WriteNodes(StreamWriter writer, SkeletonBone[] bones, SceneNode[] exportedBoneNodes)
    {
        int[] parentIndices = SkeletonExportTransforms.BuildExportedParentIndices(bones, exportedBoneNodes);
        writer.WriteLine("nodes");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = parentIndices[i];
            writer.WriteLine($"  {i} \"{EscapeName(bones[i].Name)}\" {parentIdx}");
        }
        writer.WriteLine("end");
    }

    private static void WriteBindPoseSkeleton(StreamWriter writer, SkeletonBone[] bones, SceneNode[] exportedBoneNodes)
    {
        Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(bones, exportedBoneNodes);
        writer.WriteLine("skeleton");
        writer.WriteLine("  time 0");
        for (int i = 0; i < bones.Length; i++)
        {
            SkeletonExportTransforms.GetRelativeBindTransform(bones[i], parents[bones[i]], out Vector3 pos, out Quaternion rotQuat);
            var rot		= QuaternionToEulerXYZ(rotQuat);
            writer.WriteLine($"  {i}  {FormatFloat(pos.X)} {FormatFloat(pos.Y)} {FormatFloat(pos.Z)}  {FormatFloat(rot.X)} {FormatFloat(rot.Y)} {FormatFloat(rot.Z)}");
        }
        writer.WriteLine("end");
    }

    private static void WriteAnimationSkeleton(StreamWriter writer, SkeletonAnimation anim, SkeletonBone[] bones, SceneNode[] exportedBoneNodes, CancellationToken token)
    {
        var trackByName = new Dictionary<string, SkeletonAnimationTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in anim.Tracks)
            trackByName[track.Name] = track;

        var (_, maxFrameF) = anim.GetAnimationFrameRange();
        int frameCount = maxFrameF > float.MinValue ? (int)MathF.Ceiling(maxFrameF) + 1 : 1;
        Dictionary<SkeletonBone, SkeletonBone?> exportedParents = SkeletonExportTransforms.BuildExportedParentMap(bones, exportedBoneNodes);
        Dictionary<SkeletonBone, Matrix4x4> worldTransforms = [];

        writer.WriteLine("skeleton");
        for (int frame = 0; frame < frameCount; frame++)
        {
            token.ThrowIfCancellationRequested();
            writer.WriteLine($"  time {frame}");
            float t = frame;
            worldTransforms.Clear();

            for (int i = 0; i < bones.Length; i++)
            {
                SkeletonExportTransforms.GetRelativeAnimatedTransform(bones[i], exportedParents, trackByName, t, worldTransforms, out Vector3 pos, out Quaternion quat);
                var euler = QuaternionToEulerXYZ(quat);
                writer.WriteLine($"  {i}  {FormatFloat(pos.X)} {FormatFloat(pos.Y)} {FormatFloat(pos.Z)}  {FormatFloat(euler.X)} {FormatFloat(euler.Y)} {FormatFloat(euler.Z)}");
            }
        }
        writer.WriteLine("end");
    }

    private static void WriteTriangles(StreamWriter writer, Mesh[] meshes, SkeletonBone[] allBones, SceneTranslationSelection selection, CancellationToken token)
    {
        var boneIndexMap = BuildBoneIndexMap(allBones);

        writer.WriteLine("triangles");

        var sb = new StringBuilder(256);
        foreach (var mesh in meshes)
        {
            token.ThrowIfCancellationRequested();
            if (mesh.Positions is null || mesh.FaceIndices is null)
                continue;

            string materialName = ResolveMaterialName(mesh, selection);

            int		faceCount		= mesh.FaceIndices.ElementCount / 3;
            Skin?	skin			= mesh.Skin;
            int		influences		= skin?.BoneIndices.ValueCount ?? 0;
            int[]	globalBoneTable	= BuildGlobalBoneIndexTable(mesh, allBones, boneIndexMap);

            int[]? pooledBoneIndices	= influences > 0 ? ArrayPool<int>.Shared.Rent(influences) : null;
            float[]? pooledBoneWeights	= influences > 0 ? ArrayPool<float>.Shared.Rent(influences) : null;

            Span<int> boneIndices	= pooledBoneIndices;
            Span<float> boneWeights	= pooledBoneWeights;
            try
            {
                for (int f = 0; f < faceCount; f++)
                {
                    writer.WriteLine(materialName);

                    for (int v = 0; v < 3; v++)
                    {
                        int vertIdx = mesh.FaceIndices.Get<int>(f * 3 + v, 0, 0);

                        var pos	= mesh.GetVertexPosition(vertIdx, raw: true);
                        var nrm	= mesh.Normals is not null ? mesh.GetVertexNormal(vertIdx, raw: true) : Vector3.UnitY;
                        var uv	= mesh.UVLayers is not null ? mesh.UVLayers.GetVector2(vertIdx, 0) : Vector2.Zero;

                        int parentBone	= 0;
                        int linkCount	= 0;

                        if (influences > 0 && pooledBoneIndices is not null && pooledBoneWeights is not null && skin is not null)
                        {
                            for (int j = 0; j < influences; j++)
                            {
                                float weight = skin.BoneWeights.Get<float>(vertIdx, j, 0);
                                if (weight <= 0f) continue;
                                int localIdx	= skin.BoneIndices.Get<int>(vertIdx, j, 0);
                                if ((uint)localIdx >= (uint)globalBoneTable.Length)
                                {
                                    throw new InvalidDataException($"Cannot write SMD: mesh '{mesh.Name}' contains skin index {localIdx} outside the exported skin table.");
                                }

                                int globalIdx = globalBoneTable[localIdx];
                                pooledBoneIndices[linkCount] = globalIdx;
                                pooledBoneWeights[linkCount] = weight;
                                linkCount++;
                            }

                            if (linkCount > 0)
                                parentBone = pooledBoneIndices[0];
                        }

                        sb.Clear();
                        sb.Append("  ");
                        sb.Append(parentBone);
                        sb.Append("  ");
                        sb.Append(FormatFloat(pos.X)); sb.Append(' '); sb.Append(FormatFloat(pos.Y)); sb.Append(' '); sb.Append(FormatFloat(pos.Z));
                        sb.Append("  ");
                        sb.Append(FormatFloat(nrm.X)); sb.Append(' '); sb.Append(FormatFloat(nrm.Y)); sb.Append(' '); sb.Append(FormatFloat(nrm.Z));
                        sb.Append("  ");
                        sb.Append(FormatFloat(uv.X)); sb.Append(' '); sb.Append(FormatFloat(uv.Y));

                        if (linkCount > 0)
                        {
                            sb.Append("  ");
                            sb.Append(linkCount);
                            for (int i = 0; i < linkCount; i++)
                            {
                                sb.Append("  ");
                                sb.Append(boneIndices[i]);
                                sb.Append(' ');
                                sb.Append(FormatFloat(boneWeights[i]));
                            }
                        }

                        writer.WriteLine(sb.ToString());
                    }
                }
            }
            finally
            {
                if (pooledBoneIndices is not null)
                    ArrayPool<int>.Shared.Return(pooledBoneIndices, clearArray: false);
                if (pooledBoneWeights is not null)
                    ArrayPool<float>.Shared.Return(pooledBoneWeights, clearArray: false);
            }
        }

        writer.WriteLine("end");
    }

    private static Dictionary<SkeletonBone, int> BuildBoneIndexMap(SkeletonBone[] bones)
    {
        var map = new Dictionary<SkeletonBone, int>(bones.Length);
        for (int i = 0; i < bones.Length; i++)
            map[bones[i]] = i;
        return map;
    }

    private static int[] BuildGlobalBoneIndexTable(Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap)
    {
        _ = allBones;
        var skinnedBones = mesh.Skin?.Bones;
        if (skinnedBones is null || skinnedBones.Count == 0)
            return [];

        var table					= new int[skinnedBones.Count];
        List<string> missingBones	= [];
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
            throw new InvalidDataException($"Cannot write SMD: mesh '{mesh.Name}' references skinned bones that are not included in the export selection: {string.Join(", ", missingBones)}.");
        }

        return table;
    }

    private static SkeletonBone[] SynthesiseBonesFromAnimation(SkeletonAnimation anim)
    {
        return [.. anim.Tracks.Select(t => new SkeletonBone(t.Name))];
    }

    private static string ResolveMaterialName(Mesh mesh, SceneTranslationSelection selection)
    {
        if (mesh.Materials is { Count: > 0 } mats)
        {
            Material material = mats[0];
            if (!selection.Includes(material))
            {
                throw new InvalidDataException($"Cannot write SMD: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");
            }

            if (!string.IsNullOrWhiteSpace(material.Name))
                return material.Name;
        }

        return string.IsNullOrWhiteSpace(mesh.Name) ? "default" : mesh.Name;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatFloat(float value) => value.ToString("F6", CultureInfo.InvariantCulture);

    private static string EscapeName(string name) => name.Replace("\"", "'");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 QuaternionToEulerXYZ(Quaternion q)
    {
        float w = q.W, x = q.X, y = q.Y, z = q.Z;

        float	sinRy	= Math.Clamp(-2f * (x * z - w * y), -1f, 1f);
        float	ry		= MathF.Asin(sinRy);

        float rx, rz;
        if (MathF.Abs(sinRy) < 0.9999f)
        {
            float r21	= 2f * (y * z + w * x);
            float r22	= 1f - 2f * (x * x + y * y);
            rx = MathF.Atan2(r21, r22);

            float r10	= 2f * (x * y + w * z);
            float r00	= 1f - 2f * (y * y + z * z);
            rz = MathF.Atan2(r10, r00);
        }
        else
        {

            float r01 = 2f * (x * y - w * z);
            float r02 = 2f * (x * z + w * y);
            rx = MathF.Atan2(sinRy > 0f ? r01 : -r01, sinRy > 0f ? r02 : -r02);
            rz = 0f;
        }

        return new Vector3(rx, ry, rz);
    }
}
