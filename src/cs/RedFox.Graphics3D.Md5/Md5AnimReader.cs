using System.Numerics;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Reads id Tech 4 MD5 animation (<c>.md5anim</c>) text files and populates a
/// <see cref="Scene"/> with the parsed skeleton and animation data.
/// <para>
/// An MD5 animation file defines a joint hierarchy with a base-frame pose, per-frame
/// animated component overrides selected by a per-joint flag bitmask, and optional
/// bounding-box data.  For each frame, the reader reconstructs joint transforms by
/// starting from the base-frame values and replacing flagged components with the
/// frame's component array values.  The resulting animation is stored as a
/// <see cref="SkeletonAnimation"/> with one <see cref="SkeletonAnimationTrack"/> per joint.
/// </para>
/// </summary>
public sealed class Md5AnimReader
{
    private readonly Stream _stream;
    private readonly string _name;
    private readonly SceneTranslatorOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="Md5AnimReader"/>.
    /// </summary>
    /// <param name="stream">The stream containing MD5 animation text data.</param>
    /// <param name="name">The scene or file name used when creating nodes.</param>
    /// <param name="options">Options that control translator behaviour.</param>
    public Md5AnimReader(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
        _name = name;
        _options = options;
    }

    /// <summary>
    /// Parses the MD5 animation stream and populates <paramref name="scene"/> with
    /// the resulting skeleton and animation data.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file header is invalid or the data is malformed.
    /// </exception>
    public void Read(Scene scene)
    {
        using var sr = new StreamReader(_stream, leaveOpen: true);
        string text = sr.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var tok = new TextTokenReader(text.AsSpan());

        int numFrames = 0;
        int numJoints = 0;
        int frameRate = 24;
        int numAnimatedComponents = 0;
        Md5AnimJoint[] hierarchy = [];
        (Vector3 Position, Quaternion Orientation)[] baseFrame = [];
        var frames = new List<float[]>();

        while (tok.TryReadToken(out var token))
        {
            if (token.SequenceEqual("MD5Version"))
            {
                if (!tok.TryReadInt(out int version) || version != Md5Format.Version)
                    throw new InvalidDataException("Unsupported MD5 version: expected 10.");
                continue;
            }

            if (token.SequenceEqual("commandline")) { tok.SkipRestOfLine(); continue; }
            if (token.SequenceEqual("numFrames")) { if (tok.TryReadInt(out int v)) numFrames = v; continue; }
            if (token.SequenceEqual("numJoints")) { if (tok.TryReadInt(out int v)) numJoints = v; continue; }
            if (token.SequenceEqual("frameRate")) { if (tok.TryReadInt(out int v)) frameRate = v; continue; }
            if (token.SequenceEqual("numAnimatedComponents")) { if (tok.TryReadInt(out int v)) numAnimatedComponents = v; continue; }

            if (token.SequenceEqual("hierarchy"))
            {
                if (!tok.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after hierarchy keyword.");
                hierarchy = ParseHierarchy(ref tok, numJoints);
                continue;
            }

            if (token.SequenceEqual("bounds"))
            {
                if (!tok.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after bounds keyword.");
                SkipBlock(ref tok);
                continue;
            }

            if (token.SequenceEqual("baseframe"))
            {
                if (!tok.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after baseframe keyword.");
                baseFrame = ParseBaseFrame(ref tok, numJoints);
                continue;
            }

            if (token.SequenceEqual("frame"))
            {
                tok.TryReadInt(out _); // frame index, not needed
                if (!tok.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after frame index.");
                frames.Add(ParseFrame(ref tok, numAnimatedComponents));
            }
        }

        if (hierarchy.Length == 0)
            return;

        // Build skeleton
        var bones = new SkeletonBone[hierarchy.Length];
        for (int i = 0; i < hierarchy.Length; i++)
            bones[i] = new SkeletonBone(hierarchy[i].Name);

        // Compute local transforms from world-space baseframe
        var worldPositions = new Vector3[hierarchy.Length];
        var worldOrientations = new Quaternion[hierarchy.Length];

        for (int i = 0; i < hierarchy.Length; i++)
        {
            if ((uint)i < (uint)baseFrame.Length)
            {
                worldPositions[i] = baseFrame[i].Position;
                worldOrientations[i] = baseFrame[i].Orientation;
            }
        }

        for (int i = 0; i < hierarchy.Length; i++)
        {
            int parentIdx = hierarchy[i].ParentIndex;
            if (parentIdx >= 0 && (uint)parentIdx < (uint)hierarchy.Length)
            {
                var invParentRot = Quaternion.Conjugate(worldOrientations[parentIdx]);
                bones[i].BindTransform.LocalPosition = Vector3.Transform(worldPositions[i] - worldPositions[parentIdx], invParentRot);
                bones[i].BindTransform.LocalRotation = Quaternion.Normalize(invParentRot * worldOrientations[i]);
            }
            else
            {
                bones[i].BindTransform.LocalPosition = worldPositions[i];
                bones[i].BindTransform.LocalRotation = worldOrientations[i];
            }
        }

            var skeleton = scene.RootNode.AddNode(new SkeletonBone($"{_name}_Skeleton"));
        for (int i = 0; i < hierarchy.Length; i++)
        {
            int parentIdx = hierarchy[i].ParentIndex;
            if (parentIdx < 0)
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else if ((uint)parentIdx < (uint)bones.Length)
                bones[i].MoveTo(bones[parentIdx], ReparentTransformMode.PreserveExisting);
        }

        // Build animation
        if (frames.Count > 0)
        {
            var anim = new SkeletonAnimation(_name, hierarchy.Length, TransformType.Absolute)
            {
                Framerate = frameRate,
                TransformType = TransformType.Absolute,
                TransformSpace = TransformSpace.Local,
            };

            var tracks = new SkeletonAnimationTrack[hierarchy.Length];
            for (int i = 0; i < hierarchy.Length; i++)
            {
                tracks[i] = new SkeletonAnimationTrack(hierarchy[i].Name)
                {
                    TransformType = TransformType.Absolute,
                    TransformSpace = TransformSpace.Local,
                };
                anim.Tracks.Add(tracks[i]);
            }

            for (int f = 0; f < frames.Count; f++)
            {
                float[] components = frames[f];
                float time = f;

                for (int j = 0; j < hierarchy.Length; j++)
                {
                    var frameWorld = ApplyComponentOverrides(j, hierarchy, baseFrame, components);

                    int parentIdx = hierarchy[j].ParentIndex;
                    Vector3 localPos;
                    Quaternion localRot;

                    if (parentIdx >= 0 && (uint)parentIdx < (uint)hierarchy.Length)
                    {
                        var parentWorld = GetFrameWorldTransform(parentIdx, hierarchy, baseFrame, components);
                        var invParentOri = Quaternion.Conjugate(parentWorld.Orientation);
                        localPos = Vector3.Transform(frameWorld.Position - parentWorld.Position, invParentOri);
                        localRot = Quaternion.Normalize(invParentOri * frameWorld.Orientation);
                    }
                    else
                    {
                        localPos = frameWorld.Position;
                        localRot = frameWorld.Orientation;
                    }

                    tracks[j].AddTranslationFrame(time, localPos);
                    tracks[j].AddRotationFrame(time, localRot);
                }
            }

            scene.RootNode.AddNode(anim);
        }
    }

    // ------------------------------------------------------------------
    // Frame world-space reconstruction
    // ------------------------------------------------------------------

    /// <summary>
    /// Applies component overrides from the frame data to the base-frame values for the
    /// specified joint, returning the resulting object-space transform.
    /// </summary>
    /// <param name="jointIndex">The joint index.</param>
    /// <param name="hierarchy">The hierarchy definition array.</param>
    /// <param name="baseFrame">The base-frame transform array.</param>
    /// <param name="components">The current frame's component array.</param>
    /// <returns>The object-space position and orientation for this joint in this frame.</returns>
    public static (Vector3 Position, Quaternion Orientation) ApplyComponentOverrides(int jointIndex, Md5AnimJoint[] hierarchy, (Vector3 Position, Quaternion Orientation)[] baseFrame, float[] components)
    {
        var bp = (uint)jointIndex < (uint)baseFrame.Length ? baseFrame[jointIndex].Position : Vector3.Zero;
        var bo = (uint)jointIndex < (uint)baseFrame.Length ? baseFrame[jointIndex].Orientation : Quaternion.Identity;

        float px = bp.X, py = bp.Y, pz = bp.Z;
        float qx = bo.X, qy = bo.Y, qz = bo.Z;

        int flags = hierarchy[jointIndex].Flags;
        int idx = hierarchy[jointIndex].StartIndex;

        if ((flags & 1) != 0) { px = SafeGetComponent(components, idx); idx++; }
        if ((flags & 2) != 0) { py = SafeGetComponent(components, idx); idx++; }
        if ((flags & 4) != 0) { pz = SafeGetComponent(components, idx); idx++; }
        if ((flags & 8) != 0) { qx = SafeGetComponent(components, idx); idx++; }
        if ((flags & 16) != 0) { qy = SafeGetComponent(components, idx); idx++; }
        if ((flags & 32) != 0) { qz = SafeGetComponent(components, idx); idx++; }

        return (new Vector3(px, py, pz), Md5Format.ComputeQuaternion(qx, qy, qz));
    }

    /// <summary>
    /// Reconstructs the world-space transform for the given joint in a frame by walking
    /// up the parent chain from root to the target joint.
    /// </summary>
    /// <param name="jointIndex">The joint index to reconstruct.</param>
    /// <param name="hierarchy">The hierarchy definition array.</param>
    /// <param name="baseFrame">The base-frame transform array.</param>
    /// <param name="components">The current frame's component array.</param>
    /// <returns>The world-space position and orientation of the joint for this frame.</returns>
    public static (Vector3 Position, Quaternion Orientation) GetFrameWorldTransform(int jointIndex, Md5AnimJoint[] hierarchy, (Vector3 Position, Quaternion Orientation)[] baseFrame, float[] components)
    {
        Span<int> chain = stackalloc int[64];
        int depth = 0;
        int current = jointIndex;
        while (current >= 0 && depth < 64)
        {
            chain[depth++] = current;
            current = hierarchy[current].ParentIndex;
        }

        var worldPos = Vector3.Zero;
        var worldOri = Quaternion.Identity;

        for (int d = depth - 1; d >= 0; d--)
        {
            int j = chain[d];
            var jt = ApplyComponentOverrides(j, hierarchy, baseFrame, components);

            if (d == depth - 1)
            {
                worldPos = jt.Position;
                worldOri = jt.Orientation;
            }
            else
            {
                worldPos += Vector3.Transform(jt.Position, worldOri);
                worldOri = Quaternion.Normalize(worldOri * jt.Orientation);
            }
        }

        return (worldPos, worldOri);
    }

    // ------------------------------------------------------------------
    // Section parsers
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses the <c>hierarchy { }</c> block. The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tok">The tokenizer.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of parsed <see cref="Md5AnimJoint"/> entries.</returns>
    public static Md5AnimJoint[] ParseHierarchy(ref TextTokenReader tok, int capacity)
    {
        var result = new Md5AnimJoint[capacity];
        int count = 0;

        while (!tok.IsEmpty)
        {
            if (tok.TryExpect('}'))
                break;

            if (!tok.TryReadQuotedString(out var nameSpan)) continue;
            string name = new(nameSpan);

            if (!tok.TryReadInt(out int parentIdx)) continue;
            if (!tok.TryReadInt(out int flags)) continue;
            if (!tok.TryReadInt(out int startIdx)) continue;

            tok.SkipRestOfLine();

            if ((uint)count < (uint)result.Length)
                result[count] = new Md5AnimJoint(name, parentIdx, flags, startIdx);
            count++;
        }

        return result;
    }

    /// <summary>
    /// Parses the <c>baseframe { }</c> block. The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tok">The tokenizer.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of per-joint base transforms (position + orientation).</returns>
    public static (Vector3 Position, Quaternion Orientation)[] ParseBaseFrame(ref TextTokenReader tok, int capacity)
    {
        var result = new (Vector3, Quaternion)[capacity];
        int count = 0;

        while (!tok.IsEmpty)
        {
            if (tok.TryExpect('}'))
                break;

            if (!tok.TryExpect('(')) continue;
            if (!tok.TryReadFloat(out float px)) continue;
            if (!tok.TryReadFloat(out float py)) continue;
            if (!tok.TryReadFloat(out float pz)) continue;
            if (!tok.TryExpect(')')) continue;
            if (!tok.TryExpect('(')) continue;
            if (!tok.TryReadFloat(out float qx)) continue;
            if (!tok.TryReadFloat(out float qy)) continue;
            if (!tok.TryReadFloat(out float qz)) continue;
            if (!tok.TryExpect(')')) continue;

            if ((uint)count < (uint)result.Length)
                result[count] = (new Vector3(px, py, pz), Md5Format.ComputeQuaternion(qx, qy, qz));
            count++;
        }

        return result;
    }

    /// <summary>
    /// Parses one <c>frame N { }</c> block and returns the component values as a flat array.
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tok">The tokenizer.</param>
    /// <param name="expectedComponents">The expected number of animated components.</param>
    /// <returns>The flat array of component values for this frame.</returns>
    public static float[] ParseFrame(ref TextTokenReader tok, int expectedComponents)
    {
        var components = new float[expectedComponents];
        int count = 0;

        while (!tok.IsEmpty)
        {
            if (tok.TryExpect('}'))
                break;

            if (tok.TryReadFloat(out float val))
            {
                if ((uint)count < (uint)components.Length)
                    components[count] = val;
                count++;
            }
        }

        return components;
    }

    /// <summary>
    /// Skips a brace-delimited block (e.g., the bounds block).
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tok">The tokenizer.</param>
    public static void SkipBlock(ref TextTokenReader tok)
    {
        int depth = 1;
        while (!tok.IsEmpty && depth > 0)
        {
            if (tok.TryExpect('}')) { depth--; continue; }
            if (tok.TryExpect('{')) { depth++; continue; }
            if (!tok.TryReadToken(out _)) break;
        }
    }

    /// <summary>
    /// Safely retrieves a component value from the frame data array.
    /// </summary>
    /// <param name="components">The frame's component array.</param>
    /// <param name="index">The index to read.</param>
    /// <returns>The component value, or <c>0</c> if the index is out of range.</returns>
    public static float SafeGetComponent(float[] components, int index) =>
        (uint)index < (uint)components.Length ? components[index] : 0f;
}
