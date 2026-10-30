using System.Numerics;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.IdTech;

/// <summary>
/// Reads id Tech 4 MD5 animation (<c>.md5anim</c>) text files and populates a <see cref="Scene"/> with the parsed skeleton and animation data.
/// </summary>
public sealed class Md5AnimReader
{
    private readonly Stream _stream;
    private readonly string _name;

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
    }

    /// <summary>
    /// Parses the MD5 animation stream and populates <paramref name="scene"/> with the resulting skeleton and animation data.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file header is invalid or the data is malformed.
    /// </exception>
    public void Read(Scene scene) => Read(scene, CancellationToken.None);

    /// <summary>
    /// Applies component overrides from the frame data to the base-frame values for the specified joint, returning the resulting parent-local transform.
    /// </summary>
    /// <param name="jointIndex">The joint index.</param>
    /// <param name="hierarchy">The hierarchy definition array.</param>
    /// <param name="baseFrame">The base-frame transform array.</param>
    /// <param name="components">The current frame's component array.</param>
    /// <returns>The parent-local position and orientation for this joint in this frame.</returns>
    public static (Vector3 Position, Quaternion Orientation) ApplyComponentOverrides(int jointIndex, Md5AnimJoint[] hierarchy, (Vector3 Position, Quaternion Orientation)[] baseFrame, float[] components)
    {
        var basePosition	= (uint)jointIndex < (uint)baseFrame.Length ? baseFrame[jointIndex].Position : Vector3.Zero;
        var baseOrientation	= (uint)jointIndex < (uint)baseFrame.Length ? baseFrame[jointIndex].Orientation : Quaternion.Identity;

        float positionX		= basePosition.X;
        float positionY		= basePosition.Y;
        float positionZ		= basePosition.Z;
        float orientationX	= baseOrientation.X;
        float orientationY	= baseOrientation.Y;
        float orientationZ	= baseOrientation.Z;

        int flags			= hierarchy[jointIndex].Flags;
        int componentIndex	= hierarchy[jointIndex].StartIndex;

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
    /// Reconstructs the world-space transform for the given joint in a frame by walking up the parent chain from root to the target joint.
    /// </summary>
    /// <param name="jointIndex">The joint index to reconstruct.</param>
    /// <param name="hierarchy">The hierarchy definition array.</param>
    /// <param name="baseFrame">The base-frame transform array.</param>
    /// <param name="components">The current frame's component array.</param>
    /// <returns>The world-space position and orientation of the joint for this frame.</returns>
    public static (Vector3 Position, Quaternion Orientation) GetFrameWorldTransform(int jointIndex, Md5AnimJoint[] hierarchy, (Vector3 Position, Quaternion Orientation)[] baseFrame, float[] components)
    {
        Span<int> chain	= hierarchy.Length <= 256 ? stackalloc int[hierarchy.Length] : new int[hierarchy.Length];
        int depth		= 0;
        int current		= jointIndex;
        while (current >= 0)
        {
            if ((uint)current >= (uint)hierarchy.Length || depth == chain.Length)
                throw new InvalidDataException("Invalid or cyclic MD5 hierarchy.");
            chain[depth++] = current;
            current = hierarchy[current].ParentIndex;
        }

        var worldPos = Vector3.Zero;
        var worldOri = Quaternion.Identity;

        for (int d = depth - 1; d >= 0; d--)
        {
            int j	= chain[d];
            var jt	= ApplyComponentOverrides(j, hierarchy, baseFrame, components);

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

    /// <summary>
    /// Parses the <c>hierarchy { }</c> block.
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of parsed <see cref="Md5AnimJoint"/> entries.</returns>
    public static Md5AnimJoint[] ParseHierarchy(ref TextTokenReader tokenizer, int capacity)
    {
        var result	= new Md5AnimJoint[capacity];
        int count	= 0;

        while (true)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryReadQuotedString(out var nameSpan))
                throw new InvalidDataException("Malformed MD5 record.");
            string name = new(nameSpan);

            if (!tokenizer.TryReadInt(out int parentIndex))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadInt(out int flags))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadInt(out int firstComponentIndex))
                throw new InvalidDataException("Malformed MD5 record.");

            if ((uint)count >= (uint)result.Length)
                throw new InvalidDataException("Too many MD5 records.");
            result[count] = new Md5AnimJoint(name, parentIndex, flags, firstComponentIndex);
            count++;
        }

        if (count != capacity)
            throw new InvalidDataException("MD5 record count does not match its declaration.");
        return result;
    }

    /// <summary>
    /// Parses the <c>baseframe { }</c> block.
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of per-joint base transforms (position + orientation).</returns>
    public static (Vector3 Position, Quaternion Orientation)[] ParseBaseFrame(ref TextTokenReader tokenizer, int capacity)
    {
        var result	= new (Vector3, Quaternion)[capacity];
        int count	= 0;

        while (true)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryExpect('('))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float positionX))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float positionY))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float positionZ))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect(')'))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect('('))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float orientationX))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float orientationY))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float orientationZ))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect(')'))
                throw new InvalidDataException("Malformed MD5 record.");

            if ((uint)count >= (uint)result.Length)
                throw new InvalidDataException("Too many MD5 records.");
            result[count] = (new Vector3(positionX, positionY, positionZ), Md5Format.ComputeQuaternion(orientationX, orientationY, orientationZ));
            count++;
        }

        if (count != capacity)
            throw new InvalidDataException("MD5 record count does not match its declaration.");
        return result;
    }

    /// <summary>
    /// Parses one <c>frame N { }</c> block and returns the component values as a flat array.
    /// </summary>
    /// <param name="tokenizer">The tokenizer.</param>
    /// <param name="expectedComponents">The expected number of animated components.</param>
    /// <returns>The flat array of component values for this frame.</returns>
    public static float[] ParseFrame(ref TextTokenReader tokenizer, int expectedComponents)
    {
        if (expectedComponents < 0 || expectedComponents > tokenizer.Remaining.Length)
            throw new InvalidDataException("Invalid MD5 component count.");
        float[] components = new float[expectedComponents];
        for (int i = 0; i < components.Length; i++)
            if (!tokenizer.TryReadFloat(out components[i]) || !float.IsFinite(components[i]))
                throw new InvalidDataException("Expected an MD5 frame component.");
        if (!tokenizer.TryExpect('}'))
            throw new InvalidDataException("MD5 frame component count does not match its declaration.");
        return components;
    }

    /// <summary>
    /// Skips a brace-delimited block (e.g., the bounds block).
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
    /// Retrieves a required frame component.
    /// </summary>
    /// <param name="components">The frame's component array.</param>
    /// <param name="index">The index to read.</param>
    /// <returns>The component value.</returns>
    public static float SafeGetComponent(float[] components, int index) => (uint)index < (uint)components.Length ? components[index] : throw new InvalidDataException("Invalid MD5 component index.");

    /// <summary>
    /// Reads data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void Read(Scene scene, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var sr = new StreamReader(_stream, leaveOpen: true);
        string text = sr.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var tokenizer = new TextTokenReader(text.AsSpan());

        int frameCount				= -1;
        int jointCount				= 0;
        int frameRate				= 24;
        int animatedComponentCount	= 0;
        Md5AnimJoint[] hierarchy	= [];
        (Vector3 Position, Quaternion Orientation)[] baseFrame = [];
        var frames = new List<float[]>();

        while (tokenizer.TryReadToken(out var token))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                if (!tokenizer.TryReadInt(out frameCount) || frameCount < 0)
                    throw new InvalidDataException("Invalid MD5 frame count.");
                continue;
            }

            if (token.SequenceEqual("numJoints"))
            {
                if (!tokenizer.TryReadInt(out jointCount) || jointCount < 0 || jointCount > text.Length)
                    throw new InvalidDataException("Invalid MD5 declaration.");
                continue;
            }

            if (token.SequenceEqual("frameRate"))
            {
                if (!tokenizer.TryReadInt(out frameRate) || frameRate < 0 || frameRate > text.Length)
                    throw new InvalidDataException("Invalid MD5 declaration.");
                continue;
            }

            if (token.SequenceEqual("numAnimatedComponents"))
            {
                if (!tokenizer.TryReadInt(out animatedComponentCount) || animatedComponentCount < 0 || animatedComponentCount > text.Length)
                    throw new InvalidDataException("Invalid MD5 declaration.");
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
                if (!tokenizer.TryReadInt(out int frameIndex) || frameIndex != frames.Count)
                    throw new InvalidDataException("Invalid or duplicate MD5 frame index.");
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after frame index.");
                frames.Add(ParseFrame(ref tokenizer, animatedComponentCount));
            }
        }

        if (frameCount != frames.Count || hierarchy.Length != jointCount || baseFrame.Length != jointCount || frameRate <= 0)
            throw new InvalidDataException("MD5 animation declarations do not match their records.");
        SkeletonBone[] bones	= new SkeletonBone[hierarchy.Length];
        Skeleton skeleton		= scene.RootNode.AddNode(new Skeleton($"{_name}_Skeleton"));
        for (int i = 0; i < hierarchy.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Md5AnimJoint joint	= hierarchy[i];
            int count			= System.Numerics.BitOperations.PopCount((uint)joint.Flags);
            if (joint.ParentIndex < -1 || joint.ParentIndex >= i || joint.Flags < 0 || joint.Flags > 63 || joint.StartIndex < 0 || (long)joint.StartIndex + count > animatedComponentCount)
                throw new InvalidDataException("Invalid MD5 hierarchy or component window.");
            SkeletonBone bone = new(joint.Name);
            bone.BindTransform.LocalPosition	= baseFrame[i].Position;
            bone.BindTransform.LocalRotation	= baseFrame[i].Orientation;
            bones[i]							= bone;
            bone.MoveTo(joint.ParentIndex < 0 ? skeleton : bones[joint.ParentIndex], ReparentTransformMode.PreserveExisting);
        }

        if (frames.Count > 0)
        {
            var anim = new SkeletonAnimation(_name, hierarchy.Length, TransformType.Absolute)
            {
                Framerate		= frameRate,
                TransformType	= TransformType.Absolute,
                TransformSpace	= TransformSpace.Local,
            };

            var tracks = new SkeletonAnimationTrack[hierarchy.Length];
            for (int i = 0; i < hierarchy.Length; i++)
            {
                tracks[i] = new SkeletonAnimationTrack(hierarchy[i].Name)
                {
                    TransformType	= TransformType.Absolute,
                    TransformSpace	= TransformSpace.Local,
                };
                anim.Tracks.Add(tracks[i]);
            }

            for (int f = 0; f < frames.Count; f++)
            {
                float[] components	= frames[f];
                float time			= f;

                for (int j = 0; j < hierarchy.Length; j++)
                {
                    var frameLocal = ApplyComponentOverrides(j, hierarchy, baseFrame, components);
                    tracks[j].AddTranslationFrame(time, frameLocal.Position);
                    tracks[j].AddRotationFrame(time, frameLocal.Orientation);
                }
            }

            scene.RootNode.AddNode(anim);
        }
    }
}
