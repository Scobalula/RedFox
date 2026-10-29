using System.Numerics;
using System.Text;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.IdTech;

/// <summary>
/// Writes scene data as id Tech 4 MD5 animation (<c>.md5anim</c>) text files.
/// </summary>
public sealed class Md5AnimWriter
{
    private readonly Stream _stream;

    /// <summary>
    /// Initializes a new instance of <see cref="Md5AnimWriter"/>.
    /// </summary>
    /// <param name="stream">The output stream to write MD5 animation text data to.</param>
    /// <param name="name">The logical scene name or destination file name.</param>
    /// <param name="options">Options that control translator behaviour.</param>
    public Md5AnimWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
    }

    /// <summary>
    /// Serialises the scene to the output stream in MD5 animation format.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene contains no skeleton animation.
    /// </exception>
    public void Write(Scene scene) => Write(scene, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(Scene scene, CancellationToken token) => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None), token);

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
    public void Write(SceneTranslationSelection selection) => Write(selection, CancellationToken.None);

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
        int[] parentIndices = SkeletonExportTransforms.BuildExportedParentIndices(bones, exportedBoneNodes);
        writer.WriteLine("hierarchy {");
        for (int i = 0; i < bones.Length; i++)
        {
            int parentIdx = parentIndices[i];

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
            var pos	= localPositions[i];
            var q	= localOrientations[i];
            if (q.W > 0f)
            {
                q = new Quaternion(-q.X, -q.Y, -q.Z, -q.W);
            }
            writer.WriteLine($"\t( {FormatFloat(pos.X)} {FormatFloat(pos.Y)} {FormatFloat(pos.Z)} ) ( {FormatFloat(q.X)} {FormatFloat(q.Y)} {FormatFloat(q.Z)} )");
        }
        writer.WriteLine("}");
    }

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

        var skelAnim = selection.TryGetFirstOfType<SkeletonAnimation>() ?? throw new InvalidDataException($"Cannot write MD5 anim: no SkeletonAnimation matched the export selection '{selection.Filter}'.");

        var allBones = selection.GetDescendants<SkeletonBone>();
        if (allBones.Length == 0)
            throw new InvalidOperationException("Scene must contain a skeleton to write as MD5 anim.");

        SceneNode[] exportedBoneNodes = Array.ConvertAll(allBones, static bone => (SceneNode)bone);

        Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(allBones, exportedBoneNodes);
        Dictionary<SkeletonBone, Matrix4x4> worldTransforms = [];

        var localPositions		= new Vector3[allBones.Length];
        var localOrientations	= new Quaternion[allBones.Length];
        for (int i = 0; i < allBones.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            SkeletonExportTransforms.GetRelativeBindTransform(allBones[i], parents[allBones[i]], out localPositions[i], out localOrientations[i]);
        }

        var trackByName = new Dictionary<string, SkeletonAnimationTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in skelAnim.Tracks)
            trackByName[track.Name] = track;

        var (_, maxFrameF) = skelAnim.GetAnimationFrameRange();
        int numFrames = maxFrameF > float.MinValue ? (int)MathF.Ceiling(maxFrameF) + 1 : 1;

        var perJointFlags		= new int[allBones.Length];
        var perJointStartIndex	= new int[allBones.Length];

        int totalComponents = 0;
        for (int i = 0; i < allBones.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            int flags = 0;
            if (trackByName.ContainsKey(allBones[i].Name) || !ReferenceEquals(parents[allBones[i]], allBones[i].Parent))
                flags = 63;

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

        writer.WriteLine();
        WriteHierarchy(writer, allBones, exportedBoneNodes, perJointFlags, perJointStartIndex);

        writer.WriteLine();
        WriteBounds(writer, numFrames);

        writer.WriteLine();
        WriteBaseFrame(writer, localPositions, localOrientations);

        StringBuilder sb = new(totalComponents * 16);
        for (int f = 0; f < numFrames; f++)
        {
            token.ThrowIfCancellationRequested();
            float time = f;
            worldTransforms.Clear();
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

                SkeletonExportTransforms.GetRelativeAnimatedTransform(allBones[j], parents, trackByName, time, worldTransforms, out Vector3 localPosition, out Quaternion localOrientation);

                if (localOrientation.W > 0f)
                {
                    localOrientation = new Quaternion(-localOrientation.X, -localOrientation.Y, -localOrientation.Z, -localOrientation.W);
                }

                if ((flags & 1) != 0)
                {
                    sb.Append('\t');
                    sb.Append(FormatFloat(localPosition.X));
                }
                if ((flags & 2) != 0)
                {
                    sb.Append('\t');
                    sb.Append(FormatFloat(localPosition.Y));
                }
                if ((flags & 4) != 0)
                {
                    sb.Append('\t');
                    sb.Append(FormatFloat(localPosition.Z));
                }
                if ((flags & 8) != 0)
                {
                    sb.Append('\t');
                    sb.Append(FormatFloat(localOrientation.X));
                }
                if ((flags & 16) != 0)
                {
                    sb.Append('\t');
                    sb.Append(FormatFloat(localOrientation.Y));
                }
                if ((flags & 32) != 0)
                {
                    sb.Append('\t');
                    sb.Append(FormatFloat(localOrientation.Z));
                }
            }

            writer.WriteLine(sb.ToString());
            writer.WriteLine("}");
        }

        writer.Flush();
    }
}
