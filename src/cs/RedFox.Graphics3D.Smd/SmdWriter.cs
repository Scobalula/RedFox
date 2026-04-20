using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Smd;

/// <summary>
/// Writes scene data as Valve SMD (.smd) text files.
/// <para>
/// When the scene contains <see cref="Mesh"/> geometry the writer produces a
/// <em>reference</em> SMD with a <c>nodes</c> section (skeleton), a single-frame
/// <c>skeleton</c> section containing the bind pose, and a <c>triangles</c> section.
/// </para>
/// <para>
/// When the scene contains no meshes but has a <see cref="SkeletonAnimation"/>, the
/// writer produces an <em>animation</em> SMD with a <c>nodes</c> section and a
/// multi-frame <c>skeleton</c> section encoding the keyframe data.
/// </para>
/// </summary>
public sealed class SmdWriter
{
    private readonly Stream                _stream;
    private readonly string                _name;
    private readonly SceneTranslatorOptions _options;

    public SmdWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream  = stream;
        _name    = name;
        _options = options;
    }

    /// <summary>
    /// Serialises the scene to the output stream in SMD format.
    /// </summary>
    /// <param name="scene">
    /// The scene to serialise.
    /// </param>
    public void Write(Scene scene)
        => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None));

    /// <summary>
    /// Serialises the selected scene view to the output stream in SMD format.
    /// </summary>
    /// <param name="selection">
    /// The filtered scene selection to serialise.
    /// </param>
    public void Write(SceneTranslationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var meshes   = selection.GetDescendants<Mesh>();
        var skelAnim = selection.TryGetFirstOfType<SkeletonAnimation>();

        bool hasMeshes = meshes.Length > 0;
        bool hasAnim   = skelAnim is not null;

        if (!hasMeshes && !hasAnim)
            throw new InvalidOperationException("Scene must contain at least one mesh or a skeleton animation to be written as SMD.");

        using var writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        writer.WriteLine("version 1");

        var allBones = selection.GetDescendants<SkeletonBone>();
        SceneNode[] exportedBoneNodes = Array.ConvertAll(allBones, static bone => (SceneNode)bone);

        if (hasMeshes)
        {
            WriteNodes(writer, allBones, exportedBoneNodes);
            WriteBindPoseSkeleton(writer, allBones, exportedBoneNodes);
            WriteTriangles(writer, meshes, allBones, selection);
        }
        else
        {
            // Animation-only path: fall back to bones from the scene if they exist,
            // otherwise synthesise placeholder bones from the track names.
            SkeletonBone[] bonesForAnim = allBones.Length > 0
                ? allBones
                : SynthesiseBonesFromAnimation(skelAnim!);
            SceneNode[] exportedAnimBoneNodes = Array.ConvertAll(bonesForAnim, static bone => (SceneNode)bone);
            WriteNodes(writer, bonesForAnim, exportedAnimBoneNodes);
            WriteAnimationSkeleton(writer, skelAnim!, bonesForAnim, exportedAnimBoneNodes);
        }

        writer.Flush();
    }

    // ------------------------------------------------------------------
    // nodes section
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes the SMD nodes section for the provided skeleton bones.
    /// </summary>
    /// <param name="writer">
    /// The output writer.
    /// </param>
    /// <param name="bones">
    /// The ordered bone array.</param>
    public static void WriteNodes(StreamWriter writer, SkeletonBone[] bones, SceneNode[] exportedBoneNodes)
    {
        writer.WriteLine("nodes");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = SceneNode.GetBestParentIndex(bones[i], exportedBoneNodes);
            writer.WriteLine($"  {i} \"{EscapeName(bones[i].Name)}\" {parentIdx}");
        }
        writer.WriteLine("end");
    }

    // ------------------------------------------------------------------
    // skeleton section — bind pose
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes a single-frame bind pose skeleton section.
    /// </summary>
    /// <param name="writer">
    /// The output writer.
    /// </param>
    /// <param name="bones">
    /// The ordered bone array.
    /// </param>
    public static void WriteBindPoseSkeleton(StreamWriter writer, SkeletonBone[] bones, SceneNode[] exportedBoneNodes)
    {
        writer.WriteLine("skeleton");
        writer.WriteLine("  time 0");
        for (int i = 0; i < bones.Length; i++)
        {
            GetRelativeBindTransform(bones[i], exportedBoneNodes, out Vector3 pos, out Quaternion rotQuat);
            var rot   = QuaternionToEulerXYZ(rotQuat);
            writer.WriteLine($"  {i}  {F(pos.X)} {F(pos.Y)} {F(pos.Z)}  {F(rot.X)} {F(rot.Y)} {F(rot.Z)}");
        }
        writer.WriteLine("end");
    }

    // ------------------------------------------------------------------
    // skeleton section — animation frames
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes a multi-frame animation skeleton section.
    /// </summary>
    /// <param name="writer">
    /// The output writer.
    /// </param>
    /// <param name="anim">
    /// The source skeleton animation.
    /// </param>
    /// <param name="bones">
    /// The ordered bone array.
    /// </param>
    public static void WriteAnimationSkeleton(StreamWriter writer, SkeletonAnimation anim, SkeletonBone[] bones, SceneNode[] exportedBoneNodes)
    {
        var trackByName = new Dictionary<string, SkeletonAnimationTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in anim.Tracks)
            trackByName[track.Name] = track;

        var (_, maxFrameF) = anim.GetAnimationFrameRange();
        int frameCount = maxFrameF > float.MinValue ? (int)MathF.Ceiling(maxFrameF) + 1 : 1;

        writer.WriteLine("skeleton");
        for (int frame = 0; frame < frameCount; frame++)
        {
            writer.WriteLine($"  time {frame}");
            float t = frame;

            for (int i = 0; i < bones.Length; i++)
            {
                GetRelativeAnimatedTransform(bones[i], exportedBoneNodes, trackByName, t, out Vector3 pos, out Quaternion quat);
                var euler = QuaternionToEulerXYZ(quat);
                writer.WriteLine($"  {i}  {F(pos.X)} {F(pos.Y)} {F(pos.Z)}  {F(euler.X)} {F(euler.Y)} {F(euler.Z)}");
            }
        }
        writer.WriteLine("end");
    }

    // ------------------------------------------------------------------
    // triangles section
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes the SMD triangles section for all writable meshes.
    /// </summary>
    /// <param name="writer">
    /// The output writer.
    /// </param>
    /// <param name="meshes">
    /// The source meshes.
    /// </param>
    /// <param name="allBones">
    /// The full skeleton bone array.
    /// </param>
    public static void WriteTriangles(StreamWriter writer, Mesh[] meshes, SkeletonBone[] allBones, SceneTranslationSelection selection)
    {
        var boneIndexMap = BuildBoneIndexMap(allBones);

        writer.WriteLine("triangles");

        var sb = new StringBuilder(256);
        foreach (var mesh in meshes)
        {
            if (mesh.Positions is null || mesh.FaceIndices is null)
                continue;

            string materialName = ResolveMaterialName(mesh, selection);

            int faceCount  = mesh.FaceIndices.ElementCount / 3;
            int influences = mesh.BoneIndices?.ValueCount ?? 0;
            int[] globalBoneTable = BuildGlobalBoneIndexTable(mesh, allBones, boneIndexMap);

            int[]? pooledBoneIndices = influences > 0 ? ArrayPool<int>.Shared.Rent(influences) : null;
            float[]? pooledBoneWeights = influences > 0 ? ArrayPool<float>.Shared.Rent(influences) : null;

            try
            {
                for (int f = 0; f < faceCount; f++)
                {
                    writer.WriteLine(materialName);

                    for (int v = 0; v < 3; v++)
                    {
                        int vertIdx = mesh.FaceIndices.Get<int>(f * 3 + v, 0, 0);

                    var pos = mesh.GetVertexPosition(vertIdx, raw: true);
                    var nrm = mesh.Normals is not null
                        ? mesh.GetVertexNormal(vertIdx, raw: true)
                        : Vector3.UnitY;
                    var uv = mesh.UVLayers is not null
                        ? mesh.UVLayers.GetVector2(vertIdx, 0)
                        : Vector2.Zero;

                        // ---- Gather bone-weight pairs ----
                        int parentBone = 0;
                        int linkCount = 0;

                        if (influences > 0 && pooledBoneIndices is not null && pooledBoneWeights is not null && mesh.BoneWeights is not null && mesh.BoneIndices is not null)
                        {
                            for (int j = 0; j < influences; j++)
                            {
                                float weight = mesh.BoneWeights.Get<float>(vertIdx, j, 0);
                                if (weight <= 0f) continue;
                                int localIdx  = mesh.BoneIndices.Get<int>(vertIdx, j, 0);
                                if ((uint)localIdx >= (uint)globalBoneTable.Length)
                                {
                                    throw new InvalidDataException(
                                        $"Cannot write SMD: mesh '{mesh.Name}' contains skin index {localIdx} outside the exported skin table.");
                                }

                                int globalIdx = globalBoneTable[localIdx];
                                pooledBoneIndices[linkCount] = globalIdx;
                                pooledBoneWeights[linkCount] = weight;
                                linkCount++;
                            }

                            if (linkCount > 0)
                                parentBone = pooledBoneIndices[0];
                        }

                    // ---- Emit vertex line ----
                    sb.Clear();
                    sb.Append("  ");
                    sb.Append(parentBone);
                    sb.Append("  ");
                    sb.Append(F(pos.X)); sb.Append(' '); sb.Append(F(pos.Y)); sb.Append(' '); sb.Append(F(pos.Z));
                    sb.Append("  ");
                    sb.Append(F(nrm.X)); sb.Append(' '); sb.Append(F(nrm.Y)); sb.Append(' '); sb.Append(F(nrm.Z));
                    sb.Append("  ");
                    sb.Append(F(uv.X)); sb.Append(' '); sb.Append(F(uv.Y));

                        if (linkCount > 0)
                        {
                            sb.Append("  ");
                            sb.Append(linkCount);
                            for (int i = 0; i < linkCount; i++)
                            {
                                sb.Append("  ");
                                sb.Append(pooledBoneIndices![i]);
                                sb.Append(' ');
                                sb.Append(F(pooledBoneWeights![i]));
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

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a lookup from bone reference to global bone index.
    /// </summary>
    /// <param name="bones">
    /// The ordered bone array.
    /// </param>
    /// <returns>
    /// A dictionary mapping each bone to its index.
    /// </returns>
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
    /// <param name="mesh">
    /// The source mesh.
    /// </param>
    /// <param name="allBones">
    /// The full skeleton bone array.
    /// </param>
    /// <param name="boneIndexMap">
    /// The global bone index lookup.
    /// </param>
    /// <returns>
    /// A local index to global index mapping array.
    /// </returns>
    public static int[] BuildGlobalBoneIndexTable(Mesh mesh, SkeletonBone[] allBones, Dictionary<SkeletonBone, int> boneIndexMap)
    {
        _ = allBones;
        var skinnedBones = mesh.SkinnedBones;
        if (skinnedBones is null || skinnedBones.Count == 0)
            return [];

        var table = new int[skinnedBones.Count];
        List<string> missingBones = [];
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
                $"Cannot write SMD: mesh '{mesh.Name}' references skinned bones that are not included in the export selection: {string.Join(", ", missingBones)}.");
        }

        return table;
    }

    /// <summary>
    /// Creates synthetic bones from animation track names when no scene skeleton exists.
    /// </summary>
    /// <param name="anim">
    /// The source animation.
    /// </param>
    /// <returns>
    /// An array of synthetic bones.
    /// </returns>
    public static SkeletonBone[] SynthesiseBonesFromAnimation(SkeletonAnimation anim)
    {
        return [.. anim.Tracks.Select(t => new SkeletonBone(t.Name))];
    }

    private static void GetRelativeBindTransform(SkeletonBone bone, SceneNode[] exportedBoneNodes, out Vector3 position, out Quaternion rotation)
    {
        Vector3 worldPosition = bone.GetBindWorldPosition();
        Quaternion worldRotation = Quaternion.Normalize(bone.GetBindWorldRotation());

        if (SceneNode.GetBestParent(bone, exportedBoneNodes) is SkeletonBone exportedParent)
        {
            Vector3 parentWorldPosition = exportedParent.GetBindWorldPosition();
            Quaternion parentWorldRotation = Quaternion.Normalize(exportedParent.GetBindWorldRotation());
            position = Vector3.Transform(worldPosition - parentWorldPosition, Quaternion.Conjugate(parentWorldRotation));
            rotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * worldRotation);
            return;
        }

        position = worldPosition;
        rotation = worldRotation;
    }

    private static void GetRelativeAnimatedTransform(
        SkeletonBone bone,
        SceneNode[] exportedBoneNodes,
        IReadOnlyDictionary<string, SkeletonAnimationTrack> trackByName,
        float time,
        out Vector3 position,
        out Quaternion rotation)
    {
        ComputeAnimatedWorldTransform(bone, trackByName, time, out Vector3 worldPosition, out Quaternion worldRotation);

        if (SceneNode.GetBestParent(bone, exportedBoneNodes) is SkeletonBone exportedParent)
        {
            ComputeAnimatedWorldTransform(exportedParent, trackByName, time, out Vector3 parentWorldPosition, out Quaternion parentWorldRotation);
            position = Vector3.Transform(worldPosition - parentWorldPosition, Quaternion.Conjugate(parentWorldRotation));
            rotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * worldRotation);
            return;
        }

        position = worldPosition;
        rotation = worldRotation;
    }

    private static void ComputeAnimatedWorldTransform(
        SkeletonBone bone,
        IReadOnlyDictionary<string, SkeletonAnimationTrack> trackByName,
        float time,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        Vector3 localPosition = bone.BindTransform.LocalPosition ?? Vector3.Zero;
        Quaternion localRotation = Quaternion.Normalize(bone.BindTransform.LocalRotation ?? Quaternion.Identity);

        if (trackByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track))
        {
            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                localPosition = translationCurve.SampleVector3(time);
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                localRotation = rotationCurve.SampleQuaternion(time);
        }

        if (bone.Parent is SkeletonBone parentBone)
        {
            ComputeAnimatedWorldTransform(parentBone, trackByName, time, out Vector3 parentWorldPosition, out Quaternion parentWorldRotation);
            worldRotation = Quaternion.Normalize(parentWorldRotation * localRotation);
            worldPosition = parentWorldPosition + Vector3.Transform(localPosition, parentWorldRotation);
            return;
        }

        worldPosition = localPosition;
        worldRotation = localRotation;
    }

    private static string ResolveMaterialName(Mesh mesh, SceneTranslationSelection selection)
    {
        if (mesh.Materials is { Count: > 0 } mats)
        {
            Material material = mats[0];
            if (!selection.Includes(material))
            {
                throw new InvalidDataException(
                    $"Cannot write SMD: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");
            }

            if (!string.IsNullOrWhiteSpace(material.Name))
                return material.Name;
        }

        return string.IsNullOrWhiteSpace(mesh.Name) ? "default" : mesh.Name;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /// <summary>
    /// Formats a float using fixed six-decimal invariant culture output.
    /// </summary>
    /// <param name="v">
    /// The value to format.
    /// </param>
    /// <returns>
    /// A fixed precision numeric string.
    /// </returns>
    public static string F(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes an SMD node name by replacing embedded quotes.
    /// </summary>
    /// <param name="name">
    /// The source name.
    /// </param>
    /// <returns>
    /// The escaped name.
    /// </returns>
    public static string EscapeName(string name) => name.Replace("\"", "'");

    // ------------------------------------------------------------------
    // Rotation conversion — Quaternion → Euler XYZ (radians)
    // ------------------------------------------------------------------

    /// <summary>
    /// Decomposes a quaternion into SMD Euler angles (XYZ intrinsic order, radians).
    /// Extraction uses the standard ZYX-extrinsic (= XYZ-intrinsic) rotation matrix
    /// decomposition; gimbal-lock situations are handled by zeroing the Z component.
    /// </summary>
    /// <summary>
    /// Decomposes a quaternion into SMD Euler angles in XYZ intrinsic order.
    /// </summary>
    /// <param name="q">
    /// The source quaternion.
    /// </param>
    /// <returns>
    /// A vector containing X, Y, and Z Euler rotations in radians.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 QuaternionToEulerXYZ(Quaternion q)
    {
        float w = q.W, x = q.X, y = q.Y, z = q.Z;

        // For R = Rz * Ry * Rx, the (2,0) entry is –sin(ry).
        // Quaternion rotation matrix (column-vector convention):
        //   R[2,0] = 2*(x*z – w*y)
        float sinRy = Math.Clamp(-2f * (x * z - w * y), -1f, 1f); // = 2*(w*y – x*z)
        float ry    = MathF.Asin(sinRy);

        float rx, rz;
        if (MathF.Abs(sinRy) < 0.9999f)
        {
            // No gimbal lock
            float r21 = 2f * (y * z + w * x);          // R[2,1] = cy*sx
            float r22 = 1f - 2f * (x * x + y * y);     // R[2,2] = cy*cx
            rx = MathF.Atan2(r21, r22);

            float r10 = 2f * (x * y + w * z);           // R[1,0] = cy*sz
            float r00 = 1f - 2f * (y * y + z * z);      // R[0,0] = cy*cz
            rz = MathF.Atan2(r10, r00);
        }
        else
        {
            // Gimbal lock (ry ≈ ±π/2): only rx ± rz is recoverable; set rz = 0.
            float r01 = 2f * (x * y - w * z);
            float r02 = 2f * (x * z + w * y);
            rx = MathF.Atan2(sinRy > 0f ? r01 : -r01, sinRy > 0f ? r02 : -r02);
            rz = 0f;
        }

        return new Vector3(rx, ry, rz);
    }
}
