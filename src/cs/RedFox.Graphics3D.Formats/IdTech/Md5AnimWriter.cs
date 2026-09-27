using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.IdTech;

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
        => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None));

    /// <summary>
    /// Serialises the selected scene view to the output stream in MD5 animation format.
    /// </summary>
    /// <param name="selection">The filtered scene selection to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the selection contains no skeleton bones.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when no animation matches the selection.
    /// </exception>
    public void Write(SceneTranslationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var skelAnim = selection.TryGetFirstOfType<SkeletonAnimation>()
            ?? throw new InvalidDataException(
                $"Cannot write MD5 anim: no SkeletonAnimation matched the export selection '{selection.Filter}'.");

        var allBones = selection.GetDescendants<SkeletonBone>();
        if (allBones.Length == 0)
            throw new InvalidOperationException("Scene must contain a skeleton to write as MD5 anim.");

        SceneNode[] exportedBoneNodes = Array.ConvertAll(allBones, static bone => (SceneNode)bone);

        var localPositions = new Vector3[allBones.Length];
        var localOrientations = new Quaternion[allBones.Length];
        for (int i = 0; i < allBones.Length; i++)
        {
            localPositions[i] = allBones[i].GetBindLocalPosition();
            localOrientations[i] = Quaternion.Normalize(allBones[i].GetBindLocalRotation());
        }

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
        WriteHierarchy(writer, allBones, exportedBoneNodes, perJointFlags, perJointStartIndex);

        // Write bounds (dummy bounds for now)
        writer.WriteLine();
        WriteBounds(writer, numFrames);

        writer.WriteLine();
        WriteBaseFrame(writer, localPositions, localOrientations);

        // Write frames
        StringBuilder sb = new(totalComponents * 16);
        for (int f = 0; f < numFrames; f++)
        {
            float time = f;
            writer.WriteLine();
            writer.Write($"frame {f} {{");
            writer.WriteLine();

            sb.Clear();
            for (int j = 0; j < allBones.Length; j++)
            {
                int flags = perJointFlags[j];
                if (flags == 0)
                {
                    continue;
                }

                Vector3 localPosition = allBones[j].GetBindLocalPosition();
                Quaternion localOrientation = Quaternion.Normalize(allBones[j].GetBindLocalRotation());
                if (trackByName.TryGetValue(allBones[j].Name, out SkeletonAnimationTrack? track))
                {
                    if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                    {
                        localPosition = translationCurve.SampleVector3(time);
                    }

                    if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                    {
                        localOrientation = rotationCurve.SampleQuaternion(time);
                    }
                }

                if (localOrientation.W > 0f)
                {
                    localOrientation = new Quaternion(-localOrientation.X, -localOrientation.Y, -localOrientation.Z, -localOrientation.W);
                }

                if ((flags & 1) != 0)
                {
                    sb.Append('\t');
                    sb.Append(F(localPosition.X));
                }
                if ((flags & 2) != 0)
                {
                    sb.Append('\t');
                    sb.Append(F(localPosition.Y));
                }
                if ((flags & 4) != 0)
                {
                    sb.Append('\t');
                    sb.Append(F(localPosition.Z));
                }
                if ((flags & 8) != 0)
                {
                    sb.Append('\t');
                    sb.Append(F(localOrientation.X));
                }
                if ((flags & 16) != 0)
                {
                    sb.Append('\t');
                    sb.Append(F(localOrientation.Y));
                }
                if ((flags & 32) != 0)
                {
                    sb.Append('\t');
                    sb.Append(F(localOrientation.Z));
                }
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
    /// <param name="exportedBoneNodes">The ordered exported bones as scene nodes.</param>
    /// <param name="flags">Per-joint animation flags.</param>
    /// <param name="startIndices">Per-joint start indices into the component array.</param>
    public static void WriteHierarchy(StreamWriter writer, SkeletonBone[] bones, SceneNode[] exportedBoneNodes, int[] flags, int[] startIndices)
    {
        writer.WriteLine("hierarchy {");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = SceneNode.GetBestParentIndex(bones[i], exportedBoneNodes);

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
    /// Writes the <c>baseframe { }</c> section with parent-local bind-pose transforms.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="localPositions">The parent-local joint positions.</param>
    /// <param name="localOrientations">The parent-local joint orientations.</param>
    public static void WriteBaseFrame(StreamWriter writer, Vector3[] localPositions, Quaternion[] localOrientations)
    {
        writer.WriteLine("baseframe {");
        for (int i = 0; i < localPositions.Length; i++)
        {
            var pos = localPositions[i];
            var q = localOrientations[i];
            if (q.W > 0f)
            {
                q = new Quaternion(-q.X, -q.Y, -q.Z, -q.W);
            }
            writer.WriteLine($"\t( {F(pos.X)} {F(pos.Y)} {F(pos.Z)} ) ( {F(q.X)} {F(q.Y)} {F(q.Z)} )");
        }
        writer.WriteLine("}");
    }

    // ------------------------------------------------------------------
    // World-space reconstruction for animation
    // ------------------------------------------------------------------

    /// <summary>
    /// Computes the world-space transform of a bone at a given animation time
    /// by walking up the full parent chain in the scene graph.
    /// </summary>
    /// <param name="bone">The bone to evaluate.</param>
    /// <param name="trackByName">Animation track lookup by bone name.</param>
    /// <param name="time">The animation time.</param>
    /// <param name="worldPosition">Receives the sampled world-space position.</param>
    /// <param name="worldOrientation">Receives the sampled world-space orientation.</param>
    public static void ComputeAnimWorldTransform(SkeletonBone bone, IReadOnlyDictionary<string, SkeletonAnimationTrack> trackByName, float time, out Vector3 worldPosition, out Quaternion worldOrientation)
    {
        ArgumentNullException.ThrowIfNull(bone);
        ArgumentNullException.ThrowIfNull(trackByName);

        Vector3 localPosition = bone.GetBindLocalPosition();
        Quaternion localRotation = Quaternion.Normalize(bone.GetBindLocalRotation());

        if (trackByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track))
        {
            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                localPosition = translationCurve.SampleVector3(time);
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                localRotation = rotationCurve.SampleQuaternion(time);
        }

        if (bone.Parent is SkeletonBone parentBone)
        {
            ComputeAnimWorldTransform(parentBone, trackByName, time, out Vector3 parentWorldPosition, out Quaternion parentWorldOrientation);
            worldOrientation = Quaternion.Normalize(parentWorldOrientation * localRotation);
            worldPosition = parentWorldPosition + Vector3.Transform(localPosition, parentWorldOrientation);
            return;
        }

        worldPosition = localPosition;
        worldOrientation = localRotation;
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
