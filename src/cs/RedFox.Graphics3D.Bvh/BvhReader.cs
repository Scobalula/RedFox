using System.Globalization;
using System.Numerics;
using System.Text;

using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Bvh;

/// <summary>
/// Reads BVH text files and populates a <see cref="Scene"/> with a skeleton hierarchy and motion clip.
/// </summary>
public sealed class BvhReader
{
    /// <summary>
    /// Gets the stream containing BVH text data.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Gets the logical scene or source-file name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the translator options used by this reader.
    /// </summary>
    public SceneTranslatorOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BvhReader"/>.
    /// </summary>
    /// <param name="stream">The stream containing BVH text data.</param>
    /// <param name="name">The scene or file name used when creating nodes.</param>
    /// <param name="options">Options that control translator behaviour.</param>
    public BvhReader(Stream stream, string name, SceneTranslatorOptions options)
    {
        Stream = stream;
        Name = name;
        Options = options;
    }

    /// <summary>
    /// Parses the BVH stream and populates <paramref name="scene"/> with the resulting skeleton and animation data.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">Thrown when the underlying stream cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the BVH data is malformed or incomplete.</exception>
    public void Read(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (!Stream.CanRead)
        {
            throw new IOException("The supplied BVH stream is not readable.");
        }

        _ = Options;

        using StreamReader streamReader = new(Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        string text = streamReader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("The BVH stream is empty.");
        }

        TextTokenReader tokenReader = new(text.AsSpan());
        RequireToken(ref tokenReader, "HIERARCHY");
        RequireToken(ref tokenReader, "ROOT");

        string rootJointName = ReadRequiredToken(ref tokenReader, "root joint name");
        ValidateNodeName(rootJointName, "root joint");

        Skeleton skeleton = scene.RootNode.AddNode<Skeleton>($"{Name}_Skeleton");
        HashSet<string> jointNames = new(StringComparer.OrdinalIgnoreCase);
        List<SkeletonBone> joints = [];
        List<BvhChannelType[]> channelsByJoint = [];

        ReadJoint(ref tokenReader, rootJointName, skeleton, joints, channelsByJoint, jointNames);

        RequireToken(ref tokenReader, "MOTION");
        int frameCount = ReadFrameCount(ref tokenReader);
        float frameTime = ReadFrameTime(ref tokenReader);

        SkeletonAnimation animation = new($"{Name}_Animation", skeleton, joints.Count, TransformType.Absolute) { Framerate = 1.0f / frameTime, Skeleton = skeleton, TransformSpace = TransformSpace.Local, TransformType = TransformType.Absolute };
        List<SkeletonAnimationTrack> tracks = new(joints.Count);

        for (int i = 0; i < joints.Count; i++)
        {
            SkeletonAnimationTrack track = new(joints[i].Name) { TransformSpace = TransformSpace.Local, TransformType = TransformType.Absolute };
            animation.Tracks.Add(track);
            tracks.Add(track);
        }

        ReadMotionFrames(ref tokenReader, joints, channelsByJoint, tracks, frameCount);

        if (TryReadToken(ref tokenReader, out string? trailingToken))
        {
            throw new InvalidDataException($"Unexpected trailing BVH token '{trailingToken}' after the motion section.");
        }

        scene.RootNode.AddNode(animation);
    }

    /// <summary>
    /// Reads one BVH joint block and appends the created joint to the supplied scene hierarchy and lookup tables.
    /// </summary>
    /// <param name="tokenReader">The token reader positioned at the opening brace of the joint block.</param>
    /// <param name="jointName">The joint name.</param>
    /// <param name="parent">The parent scene node that will receive the joint.</param>
    /// <param name="joints">The list that receives joints in motion-channel order.</param>
    /// <param name="channelsByJoint">The list that receives the channel array for each joint.</param>
    /// <param name="jointNames">The set used to reject duplicate joint names.</param>
    /// <returns>The created skeleton bone.</returns>
    public static SkeletonBone ReadJoint(ref TextTokenReader tokenReader, string jointName, SceneNode parent, List<SkeletonBone> joints, List<BvhChannelType[]> channelsByJoint, HashSet<string> jointNames)
    {
        if (!jointNames.Add(jointName))
        {
            throw new InvalidDataException($"The BVH hierarchy contains duplicate joint names. The joint '{jointName}' is defined more than once.");
        }

        SkeletonBone joint = parent.AddNode(new SkeletonBone(jointName));
        joints.Add(joint);

        RequireToken(ref tokenReader, "{");
        RequireToken(ref tokenReader, "OFFSET");
        joint.BindTransform.LocalPosition = ReadRequiredVector3(ref tokenReader, $"offset for joint '{joint.Name}'");
        joint.BindTransform.LocalRotation = Quaternion.Identity;

        RequireToken(ref tokenReader, "CHANNELS");
        int channelCount = ReadRequiredInt(ref tokenReader, $"channel count for joint '{joint.Name}'");
        if (channelCount <= 0)
        {
            throw new InvalidDataException($"Joint '{joint.Name}' declares an invalid BVH channel count of {channelCount}.");
        }

        BvhChannelType[] channels = new BvhChannelType[channelCount];
        bool hasXPosition = false;
        bool hasYPosition = false;
        bool hasZPosition = false;
        bool hasXRotation = false;
        bool hasYRotation = false;
        bool hasZRotation = false;

        for (int i = 0; i < channelCount; i++)
        {
            if (!tokenReader.TryReadToken(out ReadOnlySpan<char> channelToken))
            {
                throw new InvalidDataException($"Joint '{joint.Name}' is missing one or more channel tokens.");
            }

            if (!BvhFormat.TryParseChannel(channelToken, out BvhChannelType channel))
            {
                throw new InvalidDataException($"Joint '{joint.Name}' declares an unsupported BVH channel '{channelToken.ToString()}'.");
            }

            ValidateUniqueChannel(channel, joint.Name, ref hasXPosition, ref hasYPosition, ref hasZPosition, ref hasXRotation, ref hasYRotation, ref hasZRotation);
            channels[i] = channel;
        }

        channelsByJoint.Add(channels);

        while (true)
        {
            string? nextToken = PeekToken(ref tokenReader);
            if (nextToken is null)
            {
                throw new InvalidDataException($"Joint '{joint.Name}' is missing its closing brace.");
            }

            if (nextToken.Equals("JOINT", StringComparison.OrdinalIgnoreCase))
            {
                RequireToken(ref tokenReader, "JOINT");
                string childName = ReadRequiredToken(ref tokenReader, $"child joint name under '{joint.Name}'");
                ValidateNodeName(childName, $"child joint under '{joint.Name}'");
                ReadJoint(ref tokenReader, childName, joint, joints, channelsByJoint, jointNames);
                continue;
            }

            if (nextToken.Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                SkipEndSite(ref tokenReader, joint.Name);
                continue;
            }

            if (nextToken.Equals("}", StringComparison.Ordinal))
            {
                RequireToken(ref tokenReader, "}");
                return joint;
            }

            throw new InvalidDataException($"Joint '{joint.Name}' contains an unexpected BVH token '{nextToken}'.");
        }
    }

    /// <summary>
    /// Consumes one BVH <c>End Site</c> block without creating any additional scene nodes.
    /// </summary>
    /// <param name="tokenReader">The token reader positioned at the <c>End</c> token.</param>
    /// <param name="parentName">The parent joint name used for error reporting.</param>
    public static void SkipEndSite(ref TextTokenReader tokenReader, string parentName)
    {
        RequireToken(ref tokenReader, "End");
        RequireToken(ref tokenReader, "Site");
        RequireToken(ref tokenReader, "{");
        RequireToken(ref tokenReader, "OFFSET");
        _ = ReadRequiredVector3(ref tokenReader, $"end-site offset for joint '{parentName}'");
        RequireToken(ref tokenReader, "}");
    }

    /// <summary>
    /// Reads BVH motion values and appends local translation and rotation keyframes to the supplied tracks.
    /// </summary>
    /// <param name="tokenReader">The token reader positioned at the first frame value.</param>
    /// <param name="joints">The joints in motion-channel order.</param>
    /// <param name="channelsByJoint">The channel array for each joint.</param>
    /// <param name="tracks">The destination animation tracks.</param>
    /// <param name="frameCount">The total number of motion frames.</param>
    public static void ReadMotionFrames(ref TextTokenReader tokenReader, IReadOnlyList<SkeletonBone> joints, IReadOnlyList<BvhChannelType[]> channelsByJoint, IReadOnlyList<SkeletonAnimationTrack> tracks, int frameCount)
    {
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            float frameTime = frameIndex;

            for (int jointIndex = 0; jointIndex < joints.Count; jointIndex++)
            {
                SkeletonBone joint = joints[jointIndex];
                BvhChannelType[] channels = channelsByJoint[jointIndex];
                SkeletonAnimationTrack track = tracks[jointIndex];
                Vector3 offset = joint.BindTransform.LocalPosition ?? Vector3.Zero;
                Vector3 localPosition = offset;
                Vector3 rotationDegrees = Vector3.Zero;
                bool hasPositionChannels = false;
                bool hasRotationChannels = false;

                for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
                {
                    BvhChannelType channel = channels[channelIndex];
                    float value = ReadRequiredFloat(ref tokenReader, $"motion value for joint '{joint.Name}' in frame {frameIndex.ToString(CultureInfo.InvariantCulture)}");

                    switch (channel)
                    {
                        case BvhChannelType.Xposition:
                            localPosition.X = offset.X + value;
                            hasPositionChannels = true;
                            break;

                        case BvhChannelType.Yposition:
                            localPosition.Y = offset.Y + value;
                            hasPositionChannels = true;
                            break;

                        case BvhChannelType.Zposition:
                            localPosition.Z = offset.Z + value;
                            hasPositionChannels = true;
                            break;

                        case BvhChannelType.Xrotation:
                            rotationDegrees.X = value;
                            hasRotationChannels = true;
                            break;

                        case BvhChannelType.Yrotation:
                            rotationDegrees.Y = value;
                            hasRotationChannels = true;
                            break;

                        case BvhChannelType.Zrotation:
                            rotationDegrees.Z = value;
                            hasRotationChannels = true;
                            break;
                    }
                }

                if (hasPositionChannels)
                {
                    track.AddTranslationFrame(frameTime, localPosition);
                }

                if (hasRotationChannels)
                {
                    track.AddRotationFrame(frameTime, BvhRotation.ComposeDegrees(rotationDegrees, channels));
                }
            }
        }
    }

    /// <summary>
    /// Reads the BVH frame-count declaration.
    /// </summary>
    /// <param name="tokenReader">The token reader positioned at the frame-count declaration.</param>
    /// <returns>The declared frame count.</returns>
    public static int ReadFrameCount(ref TextTokenReader tokenReader)
    {
        RequireToken(ref tokenReader, "Frames:");
        int frameCount = ReadRequiredInt(ref tokenReader, "frame count");
        if (frameCount <= 0)
        {
            throw new InvalidDataException($"The BVH file declares an invalid frame count of {frameCount}.");
        }

        return frameCount;
    }

    /// <summary>
    /// Reads the BVH frame-time declaration.
    /// </summary>
    /// <param name="tokenReader">The token reader positioned at the frame-time declaration.</param>
    /// <returns>The declared frame time in seconds.</returns>
    public static float ReadFrameTime(ref TextTokenReader tokenReader)
    {
        string label = ReadRequiredToken(ref tokenReader, "frame-time label");
        if (label.Equals("Frame", StringComparison.OrdinalIgnoreCase))
        {
            RequireToken(ref tokenReader, "Time:");
        }
        else if (!label.Equals("FrameTime:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Expected 'Frame Time:' in the BVH motion header but found '{label}'.");
        }

        float frameTime = ReadRequiredFloat(ref tokenReader, "frame time");
        if (!float.IsFinite(frameTime) || frameTime <= 0.0f)
        {
            throw new InvalidDataException($"The BVH file declares an invalid frame time of {frameTime.ToString(CultureInfo.InvariantCulture)}.");
        }

        return frameTime;
    }

    /// <summary>
    /// Reads a required vector from the token stream.
    /// </summary>
    /// <param name="tokenReader">The token reader to consume.</param>
    /// <param name="description">A human-readable description used in exception messages.</param>
    /// <returns>The parsed vector.</returns>
    public static Vector3 ReadRequiredVector3(ref TextTokenReader tokenReader, string description)
    {
        float x = ReadRequiredFloat(ref tokenReader, $"{description} X component");
        float y = ReadRequiredFloat(ref tokenReader, $"{description} Y component");
        float z = ReadRequiredFloat(ref tokenReader, $"{description} Z component");
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Reads a required integer from the token stream.
    /// </summary>
    /// <param name="tokenReader">The token reader to consume.</param>
    /// <param name="description">A human-readable description used in exception messages.</param>
    /// <returns>The parsed integer.</returns>
    public static int ReadRequiredInt(ref TextTokenReader tokenReader, string description)
    {
        if (!tokenReader.TryReadInt(out int value))
        {
            throw new InvalidDataException($"Expected an integer token for {description}.");
        }

        return value;
    }

    /// <summary>
    /// Reads a required floating-point value from the token stream.
    /// </summary>
    /// <param name="tokenReader">The token reader to consume.</param>
    /// <param name="description">A human-readable description used in exception messages.</param>
    /// <returns>The parsed floating-point value.</returns>
    public static float ReadRequiredFloat(ref TextTokenReader tokenReader, string description)
    {
        if (!tokenReader.TryReadFloat(out float value))
        {
            throw new InvalidDataException($"Expected a floating-point token for {description}.");
        }

        if (!float.IsFinite(value))
        {
            throw new InvalidDataException($"Expected a finite floating-point token for {description}.");
        }

        return value;
    }

    /// <summary>
    /// Reads a required token from the token stream.
    /// </summary>
    /// <param name="tokenReader">The token reader to consume.</param>
    /// <param name="description">A human-readable description used in exception messages.</param>
    /// <returns>The parsed token as a string.</returns>
    public static string ReadRequiredToken(ref TextTokenReader tokenReader, string description)
    {
        if (!tokenReader.TryReadToken(out ReadOnlySpan<char> token))
        {
            throw new InvalidDataException($"Expected a token for {description}.");
        }

        return token.ToString();
    }

    /// <summary>
    /// Requires the next token to match the supplied expected token.
    /// </summary>
    /// <param name="tokenReader">The token reader to consume.</param>
    /// <param name="expectedToken">The expected BVH token.</param>
    public static void RequireToken(ref TextTokenReader tokenReader, string expectedToken)
    {
        string actualToken = ReadRequiredToken(ref tokenReader, $"'{expectedToken}'");
        if (!actualToken.Equals(expectedToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Expected the BVH token '{expectedToken}' but found '{actualToken}'.");
        }
    }

    /// <summary>
    /// Peeks the next token without consuming it.
    /// </summary>
    /// <param name="tokenReader">The token reader to inspect.</param>
    /// <returns>The next token, or <see langword="null"/> when the input is exhausted.</returns>
    public static string? PeekToken(ref TextTokenReader tokenReader)
    {
        TextTokenReader lookAhead = tokenReader;
        if (!lookAhead.TryReadToken(out ReadOnlySpan<char> token))
        {
            return null;
        }

        return token.ToString();
    }

    /// <summary>
    /// Attempts to read the next token as a string.
    /// </summary>
    /// <param name="tokenReader">The token reader to consume.</param>
    /// <param name="token">The token text when one is available.</param>
    /// <returns><see langword="true"/> when a token is available; otherwise, <see langword="false"/>.</returns>
    public static bool TryReadToken(ref TextTokenReader tokenReader, out string? token)
    {
        if (!tokenReader.TryReadToken(out ReadOnlySpan<char> tokenSpan))
        {
            token = null;
            return false;
        }

        token = tokenSpan.ToString();
        return true;
    }

    /// <summary>
    /// Validates that a BVH node name is non-empty and token-safe.
    /// </summary>
    /// <param name="nodeName">The node name to validate.</param>
    /// <param name="description">A human-readable description used in exception messages.</param>
    public static void ValidateNodeName(string nodeName, string description)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            throw new InvalidDataException($"The BVH {description} is missing a valid name.");
        }

        for (int i = 0; i < nodeName.Length; i++)
        {
            char character = nodeName[i];
            if (char.IsWhiteSpace(character) || character is '{' or '}')
            {
                throw new InvalidDataException($"The BVH {description} name '{nodeName}' contains unsupported whitespace or brace characters.");
            }
        }
    }

    /// <summary>
    /// Validates that a BVH channel is not declared more than once for the same joint.
    /// </summary>
    /// <param name="channel">The channel to validate.</param>
    /// <param name="jointName">The joint name used in exception messages.</param>
    /// <param name="hasXPosition">Tracks whether X position was already declared.</param>
    /// <param name="hasYPosition">Tracks whether Y position was already declared.</param>
    /// <param name="hasZPosition">Tracks whether Z position was already declared.</param>
    /// <param name="hasXRotation">Tracks whether X rotation was already declared.</param>
    /// <param name="hasYRotation">Tracks whether Y rotation was already declared.</param>
    /// <param name="hasZRotation">Tracks whether Z rotation was already declared.</param>
    public static void ValidateUniqueChannel(BvhChannelType channel, string jointName, ref bool hasXPosition, ref bool hasYPosition, ref bool hasZPosition, ref bool hasXRotation, ref bool hasYRotation, ref bool hasZRotation)
    {
        switch (channel)
        {
            case BvhChannelType.Xposition when hasXPosition:
            case BvhChannelType.Yposition when hasYPosition:
            case BvhChannelType.Zposition when hasZPosition:
            case BvhChannelType.Xrotation when hasXRotation:
            case BvhChannelType.Yrotation when hasYRotation:
            case BvhChannelType.Zrotation when hasZRotation:
                throw new InvalidDataException($"Joint '{jointName}' declares the BVH channel '{BvhFormat.GetChannelName(channel)}' more than once.");
        }

        switch (channel)
        {
            case BvhChannelType.Xposition:
                hasXPosition = true;
                break;

            case BvhChannelType.Yposition:
                hasYPosition = true;
                break;

            case BvhChannelType.Zposition:
                hasZPosition = true;
                break;

            case BvhChannelType.Xrotation:
                hasXRotation = true;
                break;

            case BvhChannelType.Yrotation:
                hasYRotation = true;
                break;

            case BvhChannelType.Zrotation:
                hasZRotation = true;
                break;
        }
    }
}
