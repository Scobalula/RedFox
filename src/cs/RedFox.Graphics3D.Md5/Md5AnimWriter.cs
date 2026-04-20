using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Writes scene data as id Tech 4 MD5 animation (<c>.md5anim</c>) text files.
/// <para>
/// The writer serialises a skeleton animation into the MD5 animation format.  For each
/// joint, a flag bitmask is computed from the set of animated components that differ from
/// the base frame.  The base frame is taken from the skeleton's bind pose, and frame data
/// is written as a flat array of component override values.
/// </para>
/// </summary>
public sealed class Md5AnimWriter
{
    private readonly Stream _stream;
    private readonly string _name;
    private readonly SceneTranslatorOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="Md5AnimWriter"/>.
    /// </summary>
    /// <param name="stream">The output stream to write MD5 animation text data to.</param>
    /// <param name="name">The logical scene name or destination file name.</param>
    /// <param name="options">Options that control translator behaviour.</param>
    public Md5AnimWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
        _name = name;
        _options = options;
    }

    /// <summary>
    /// Serialises the scene to the output stream in MD5 animation format.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene contains no skeleton animation.
    /// </exception>
    public void Write(Scene scene)
    {
        var skelAnim = scene.TryGetFirstOfType<SkeletonAnimation>(SceneNodeFlags.NoExport)
            ?? throw new InvalidOperationException("Scene must contain a skeleton animation to write as MD5 anim.");

        var allBones = scene.RootNode.GetDescendants<SkeletonBone>(SceneNodeFlags.NoExport);
        if (allBones.Length == 0)
            throw new InvalidOperationException("Scene must contain a skeleton to write as MD5 anim.");

        var boneIndexMap = Md5MeshWriter.BuildBoneIndexMap(allBones);

        // Compute world-space bind transforms
        var worldPositions = new Vector3[allBones.Length];
        var worldOrientations = new Quaternion[allBones.Length];
        Md5MeshWriter.ComputeWorldBindTransforms(allBones, boneIndexMap, worldPositions, worldOrientations);

        // Build track lookup
        var trackByName = new Dictionary<string, SkeletonAnimationTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in skelAnim.Tracks)
            trackByName[track.Name] = track;

        // Determine frame range
        var (_, maxFrameF) = skelAnim.GetAnimationFrameRange();
        int numFrames = maxFrameF > float.MinValue ? (int)MathF.Ceiling(maxFrameF) + 1 : 1;

        // Pre-compute: for each bone, determine the flags and the base-frame values
        // Then compute per-frame component data
        var perJointFlags = new int[allBones.Length];
        var perJointStartIndex = new int[allBones.Length];

        // Base frame = world-space bind pose (already computed)
        // For each frame, compute world-space transforms from local animation data

        // First pass: determine which components are animated (all components for simplicity,
        // since the animation stores local-space data we need to convert to world-space)
        // We mark all components as animated for joints that have tracks.
        int totalComponents = 0;
        for (int i = 0; i < allBones.Length; i++)
        {
            int flags = 0;
            if (trackByName.ContainsKey(allBones[i].Name))
                flags = 63; // All 6 components: Tx Ty Tz Qx Qy Qz

            perJointFlags[i] = flags;
            perJointStartIndex[i] = totalComponents;
            totalComponents += CountBits(flags);
        }

        using var writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.NewLine = "\n";

        writer.WriteLine("MD5Version 10");
        writer.WriteLine($"commandline \"\"");
        writer.WriteLine();
        writer.WriteLine($"numFrames {numFrames}");
        writer.WriteLine($"numJoints {allBones.Length}");
        writer.WriteLine($"frameRate {(int)skelAnim.Framerate}");
        writer.WriteLine($"numAnimatedComponents {totalComponents}");

        // Write hierarchy
        writer.WriteLine();
        WriteHierarchy(writer, allBones, boneIndexMap, perJointFlags, perJointStartIndex);

        // Write bounds (dummy bounds for now)
        writer.WriteLine();
        WriteBounds(writer, numFrames);

        // Write baseframe (world-space bind pose)
        writer.WriteLine();
        WriteBaseFrame(writer, worldPositions, worldOrientations);

        // Write frames
        for (int f = 0; f < numFrames; f++)
        {
            float time = f;
            writer.WriteLine();
            writer.Write($"frame {f} {{");
            writer.WriteLine();

            var sb = new StringBuilder(totalComponents * 16);
            for (int j = 0; j < allBones.Length; j++)
            {
                int flags = perJointFlags[j];
                if (flags == 0) continue;

                // Get local-space animation data at this time
                var localPos = allBones[j].BindTransform.LocalPosition ?? Vector3.Zero;
                var localRot = Quaternion.Normalize(allBones[j].BindTransform.LocalRotation ?? Quaternion.Identity);

                if (trackByName.TryGetValue(allBones[j].Name, out var track))
                {
                    if (track.TranslationCurve is { KeyFrameCount: > 0 } tCurve)
                        localPos = tCurve.SampleVector3(time);
                    if (track.RotationCurve is { KeyFrameCount: > 0 } rCurve)
                        localRot = rCurve.SampleQuaternion(time);
                }

                // Convert local to world
                Vector3 worldPos;
                Quaternion worldOri;
                if (allBones[j].Parent is SkeletonBone parentBone && boneIndexMap.TryGetValue(parentBone, out int pi))
                {
                    var parentWorldPos = ComputeAnimWorldPosition(pi, allBones, boneIndexMap, trackByName, time);
                    var parentWorldOri = ComputeAnimWorldOrientation(pi, allBones, boneIndexMap, trackByName, time);
                    worldOri = Quaternion.Normalize(parentWorldOri * localRot);
                    worldPos = parentWorldPos + Vector3.Transform(localPos, parentWorldOri);
                }
                else
                {
                    worldPos = localPos;
                    worldOri = localRot;
                }

                if ((flags & 1) != 0) { sb.Append('\t'); sb.Append(F(worldPos.X)); }
                if ((flags & 2) != 0) { sb.Append('\t'); sb.Append(F(worldPos.Y)); }
                if ((flags & 4) != 0) { sb.Append('\t'); sb.Append(F(worldPos.Z)); }
                if ((flags & 8) != 0) { sb.Append('\t'); sb.Append(F(worldOri.X)); }
                if ((flags & 16) != 0) { sb.Append('\t'); sb.Append(F(worldOri.Y)); }
                if ((flags & 32) != 0) { sb.Append('\t'); sb.Append(F(worldOri.Z)); }
            }

            writer.WriteLine(sb.ToString());
            writer.WriteLine("}");
        }

        writer.Flush();
    }

    // ------------------------------------------------------------------
    // Hierarchy
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes the <c>hierarchy { }</c> section.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="bones">The ordered bone array.</param>
    /// <param name="boneIndexMap">Maps each bone to its index.</param>
    /// <param name="flags">Per-joint animation flags.</param>
    /// <param name="startIndices">Per-joint start indices into the component array.</param>
    public static void WriteHierarchy(StreamWriter writer, SkeletonBone[] bones, Dictionary<SkeletonBone, int> boneIndexMap, int[] flags, int[] startIndices)
    {
        writer.WriteLine("hierarchy {");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = bones[i].Parent is SkeletonBone parentBone
                         && boneIndexMap.TryGetValue(parentBone, out int pi) ? pi : -1;

            string parentComment = parentIdx >= 0 ? bones[parentIdx].Name : string.Empty;
            writer.Write($"\t\"{bones[i].Name}\"\t{parentIdx} {flags[i]} {startIndices[i]}");
            if (!string.IsNullOrEmpty(parentComment))
                writer.Write($"\t\t// {parentComment}");
            writer.WriteLine();
        }
        writer.WriteLine("}");
    }

    /// <summary>
    /// Writes the <c>bounds { }</c> section with zero-volume bounds.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="numFrames">The number of frames.</param>
    public static void WriteBounds(StreamWriter writer, int numFrames)
    {
        writer.WriteLine("bounds {");
        for (int f = 0; f < numFrames; f++)
            writer.WriteLine("\t( 0 0 0 ) ( 0 0 0 )");
        writer.WriteLine("}");
    }

    /// <summary>
    /// Writes the <c>baseframe { }</c> section with world-space bind-pose transforms.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="worldPositions">The world-space joint positions.</param>
    /// <param name="worldOrientations">The world-space joint orientations.</param>
    public static void WriteBaseFrame(StreamWriter writer, Vector3[] worldPositions, Quaternion[] worldOrientations)
    {
        writer.WriteLine("baseframe {");
        for (int i = 0; i < worldPositions.Length; i++)
        {
            var pos = worldPositions[i];
            var q = worldOrientations[i];
            writer.WriteLine($"\t( {F(pos.X)} {F(pos.Y)} {F(pos.Z)} ) ( {F(q.X)} {F(q.Y)} {F(q.Z)} )");
        }
        writer.WriteLine("}");
    }

    // ------------------------------------------------------------------
    // World-space reconstruction for animation
    // ------------------------------------------------------------------

    /// <summary>
    /// Computes the world-space position of a bone at a given animation time
    /// by walking up the parent chain.
    /// </summary>
    /// <param name="boneIndex">The bone index.</param>
    /// <param name="bones">The bone array.</param>
    /// <param name="boneIndexMap">The bone-to-index lookup.</param>
    /// <param name="trackByName">Animation track lookup by bone name.</param>
    /// <param name="time">The animation time.</param>
    /// <returns>The world-space position.</returns>
    public static Vector3 ComputeAnimWorldPosition(int boneIndex, SkeletonBone[] bones, Dictionary<SkeletonBone, int> boneIndexMap, Dictionary<string, SkeletonAnimationTrack> trackByName, float time)
    {
        // Build chain from root
        Span<int> chain = stackalloc int[64];
        int depth = 0;
        int current = boneIndex;
        while (current >= 0 && depth < 64)
        {
            chain[depth++] = current;
            current = bones[current].Parent is SkeletonBone p && boneIndexMap.TryGetValue(p, out int pi) ? pi : -1;
        }

        var worldPos = Vector3.Zero;
        var worldOri = Quaternion.Identity;

        for (int d = depth - 1; d >= 0; d--)
        {
            int j = chain[d];
            var localPos = bones[j].BindTransform.LocalPosition ?? Vector3.Zero;
            var localRot = Quaternion.Normalize(bones[j].BindTransform.LocalRotation ?? Quaternion.Identity);

            if (trackByName.TryGetValue(bones[j].Name, out var track))
            {
                if (track.TranslationCurve is { KeyFrameCount: > 0 } tCurve)
                    localPos = tCurve.SampleVector3(time);
                if (track.RotationCurve is { KeyFrameCount: > 0 } rCurve)
                    localRot = rCurve.SampleQuaternion(time);
            }

            if (d == depth - 1)
            {
                worldPos = localPos;
                worldOri = localRot;
            }
            else
            {
                worldPos += Vector3.Transform(localPos, worldOri);
                worldOri = Quaternion.Normalize(worldOri * localRot);
            }
        }

        return worldPos;
    }

    /// <summary>
    /// Computes the world-space orientation of a bone at a given animation time
    /// by walking up the parent chain.
    /// </summary>
    /// <param name="boneIndex">The bone index.</param>
    /// <param name="bones">The bone array.</param>
    /// <param name="boneIndexMap">The bone-to-index lookup.</param>
    /// <param name="trackByName">Animation track lookup by bone name.</param>
    /// <param name="time">The animation time.</param>
    /// <returns>The world-space orientation.</returns>
    public static Quaternion ComputeAnimWorldOrientation(int boneIndex, SkeletonBone[] bones, Dictionary<SkeletonBone, int> boneIndexMap, Dictionary<string, SkeletonAnimationTrack> trackByName, float time)
    {
        Span<int> chain = stackalloc int[64];
        int depth = 0;
        int current = boneIndex;
        while (current >= 0 && depth < 64)
        {
            chain[depth++] = current;
            current = bones[current].Parent is SkeletonBone p && boneIndexMap.TryGetValue(p, out int pi) ? pi : -1;
        }

        var worldOri = Quaternion.Identity;
        for (int d = depth - 1; d >= 0; d--)
        {
            int j = chain[d];
            var localRot = Quaternion.Normalize(bones[j].BindTransform.LocalRotation ?? Quaternion.Identity);

            if (trackByName.TryGetValue(bones[j].Name, out var track))
            {
                if (track.RotationCurve is { KeyFrameCount: > 0 } rCurve)
                    localRot = rCurve.SampleQuaternion(time);
            }

            worldOri = d == depth - 1 ? localRot : Quaternion.Normalize(worldOri * localRot);
        }

        return worldOri;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Counts the number of set bits in the flag integer.
    /// </summary>
    /// <param name="flags">The flag value.</param>
    /// <returns>The number of set bits.</returns>
    public static int CountBits(int flags)
    {
        return System.Numerics.BitOperations.PopCount((uint)flags);
    }

    /// <summary>
    /// Formats a float value for MD5 file output.
    /// </summary>
    /// <param name="v">The value to format.</param>
    /// <returns>An invariant-culture numeric string.</returns>
    public static string F(float v) => Md5Format.F(v);
}
