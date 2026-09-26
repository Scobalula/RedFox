using System.Numerics;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.IdTech;

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

        var tokenizer = new TextTokenReader(text.AsSpan());

        int jointCount = 0;
        int frameRate = 24;
        int animatedComponentCount = 0;
        Md5AnimJoint[] hierarchy = [];
        (Vector3 Position, Quaternion Orientation)[] baseFrame = [];
        var frames = new List<float[]>();

        while (tokenizer.TryReadToken(out var token))
        {
            if (token.SequenceEqual("MD5Version"))
            {
                if (!tokenizer.TryReadInt(out int version) || version != Md5Format.Version)
                    throw new InvalidDataException("Unsupported MD5 version: expected 10.");
                continue;
            }

            if (token.SequenceEqual("commandline"))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }

            if (token.SequenceEqual("numFrames"))
            {
                tokenizer.TryReadInt(out _);
                continue;
            }

            if (token.SequenceEqual("numJoints"))
            {
                if (tokenizer.TryReadInt(out int parsedJointCount))
                    jointCount = parsedJointCount;
                continue;
            }

            if (token.SequenceEqual("frameRate"))
            {
                if (tokenizer.TryReadInt(out int parsedFrameRate))
                    frameRate = parsedFrameRate;
                continue;
            }

            if (token.SequenceEqual("numAnimatedComponents"))
            {
                if (tokenizer.TryReadInt(out int parsedComponentCount))
                    animatedComponentCount = parsedComponentCount;
                continue;
            }

            if (token.SequenceEqual("hierarchy"))
            {
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after hierarchy keyword.");
                hierarchy = ParseHierarchy(ref tokenizer, jointCount);
                continue;
            }

            if (token.SequenceEqual("bounds"))
            {
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after bounds keyword.");
                SkipBlock(ref tokenizer);
                continue;
            }

            if (token.SequenceEqual("baseframe"))
            {
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after baseframe keyword.");
                baseFrame = ParseBaseFrame(ref tokenizer, jointCount);
                continue;
            }

            if (token.SequenceEqual("frame"))
            {
                tokenizer.TryReadInt(out _);
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after frame index.");
                frames.Add(ParseFrame(ref tokenizer, animatedComponentCount));
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
            int parentIndex = hierarchy[i].ParentIndex;
            if (parentIndex >= 0 && (uint)parentIndex < (uint)hierarchy.Length)
            {
                var inverseParentRotation = Quaternion.Conjugate(worldOrientations[parentIndex]);
                bones[i].BindTransform.LocalPosition = Vector3.Transform(worldPositions[i] - worldPositions[parentIndex], inverseParentRotation);
                bones[i].BindTransform.LocalRotation = Quaternion.Normalize(inverseParentRotation * worldOrientations[i]);
            }
            else
            {
                bones[i].BindTransform.LocalPosition = worldPositions[i];
                bones[i].BindTransform.LocalRotation = worldOrientations[i];
            }
        }

            var skeleton = scene.RootNode.AddNode(new Skeleton($"{_name}_Skeleton"));
        for (int i = 0; i < hierarchy.Length; i++)
        {
            int parentIndex = hierarchy[i].ParentIndex;
            if (parentIndex < 0)
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else if ((uint)parentIndex < (uint)bones.Length)
                bones[i].MoveTo(bones[parentIndex], ReparentTransformMode.PreserveExisting);
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

                    int parentIndex = hierarchy[j].ParentIndex;
                    Vector3 localPos;
                    Quaternion localRot;

                    if (parentIndex >= 0 && (uint)parentIndex < (uint)hierarchy.Length)
                    {
                        var parentWorld = GetFrameWorldTransform(parentIndex, hierarchy, baseFrame, components);
                        var inverseParentOrientation = Quaternion.Conjugate(parentWorld.Orientation);
                        localPos = Vector3.Transform(frameWorld.Position - parentWorld.Position, inverseParentOrientation);
                        localRot = Quaternion.Normalize(inverseParentOrientation * frameWorld.Orientation);
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
        var basePosition = (uint)jointIndex < (uint)baseFrame.Length ? baseFrame[jointIndex].Position : Vector3.Zero;
        var baseOrientation = (uint)jointIndex < (uint)baseFrame.Length ? baseFrame[jointIndex].Orientation : Quaternion.Identity;

        float positionX = basePosition.X;
        float positionY = basePosition.Y;
        float positionZ = basePosition.Z;
        float orientationX = baseOrientation.X;
        float orientationY = baseOrientation.Y;
        float orientationZ = baseOrientation.Z;

        int flags = hierarchy[jointIndex].Flags;
        int componentIndex = hierarchy[jointIndex].StartIndex;

        if ((flags & 1) != 0)
        {
            positionX = SafeGetComponent(components, componentIndex);
            componentIndex++;
        }
        if ((flags & 2) != 0)
        {
            positionY = SafeGetComponent(components, componentIndex);
            componentIndex++;
        }
        if ((flags & 4) != 0)
        {
            positionZ = SafeGetComponent(components, componentIndex);
            componentIndex++;
        }
        if ((flags & 8) != 0)
        {
            orientationX = SafeGetComponent(components, componentIndex);
            componentIndex++;
        }
        if ((flags & 16) != 0)
        {
            orientationY = SafeGetComponent(components, componentIndex);
            componentIndex++;
        }
        if ((flags & 32) != 0)
            orientationZ = SafeGetComponent(components, componentIndex);

        return (new Vector3(positionX, positionY, positionZ), Md5Format.ComputeQuaternion(orientationX, orientationY, orientationZ));
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
    /// <param name="tokenizer">The tokenizer.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of parsed <see cref="Md5AnimJoint"/> entries.</returns>
    public static Md5AnimJoint[] ParseHierarchy(ref TextTokenReader tokenizer, int capacity)
    {
        var result = new Md5AnimJoint[capacity];
        int count = 0;

        while (!tokenizer.IsEmpty)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryReadQuotedString(out var nameSpan))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            string name = new(nameSpan);

            if (!tokenizer.TryReadInt(out int parentIndex))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadInt(out int flags))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadInt(out int firstComponentIndex))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }

            tokenizer.SkipRestOfLine();

            if ((uint)count < (uint)result.Length)
                result[count] = new Md5AnimJoint(name, parentIndex, flags, firstComponentIndex);
            count++;
        }

        return result;
    }

    /// <summary>
    /// Parses the <c>baseframe { }</c> block. The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of per-joint base transforms (position + orientation).</returns>
    public static (Vector3 Position, Quaternion Orientation)[] ParseBaseFrame(ref TextTokenReader tokenizer, int capacity)
    {
        var result = new (Vector3, Quaternion)[capacity];
        int count = 0;

        while (!tokenizer.IsEmpty)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryExpect('('))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float positionX))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float positionY))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float positionZ))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect(')'))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect('('))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float orientationX))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float orientationY))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float orientationZ))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect(')'))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }

            if ((uint)count < (uint)result.Length)
                result[count] = (new Vector3(positionX, positionY, positionZ), Md5Format.ComputeQuaternion(orientationX, orientationY, orientationZ));
            count++;
        }

        return result;
    }

    /// <summary>
    /// Parses one <c>frame N { }</c> block and returns the component values as a flat array.
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    /// <param name="expectedComponents">The expected number of animated components.</param>
    /// <returns>The flat array of component values for this frame.</returns>
    public static float[] ParseFrame(ref TextTokenReader tokenizer, int expectedComponents)
    {
        var components = new float[expectedComponents];
        int count = 0;

        while (!tokenizer.IsEmpty)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (tokenizer.TryReadFloat(out float componentValue))
            {
                if ((uint)count < (uint)components.Length)
                    components[count] = componentValue;
                count++;
            }
        }

        return components;
    }

    /// <summary>
    /// Skips a brace-delimited block (e.g., the bounds block).
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    public static void SkipBlock(ref TextTokenReader tokenizer)
    {
        int depth = 1;
        while (!tokenizer.IsEmpty && depth > 0)
        {
            if (tokenizer.TryExpect('}'))
            {
                depth--;
                continue;
            }
            if (tokenizer.TryExpect('{'))
            {
                depth++;
                continue;
            }
            if (!tokenizer.TryReadToken(out _))
                break;
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
