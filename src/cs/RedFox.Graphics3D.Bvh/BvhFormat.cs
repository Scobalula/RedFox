namespace RedFox.Graphics3D.Bvh;

/// <summary>
/// Provides constants and channel helpers for BVH files.
/// </summary>
public static class BvhFormat
{
    /// <summary>
    /// Gets the standard file extension for BVH motion files.
    /// </summary>
    public const string Extension = ".bvh";

    /// <summary>
    /// Gets the default frame time used when a scene does not provide animation timing.
    /// </summary>
    public const float DefaultFrameTime = 1.0f / 30.0f;

    /// <summary>
    /// Gets the default root channel sequence written for BVH files.
    /// </summary>
    public static IReadOnlyList<BvhChannelType> DefaultRootChannelSequence { get; } =
        Array.AsReadOnly([BvhChannelType.Xposition, BvhChannelType.Yposition, BvhChannelType.Zposition, BvhChannelType.Zrotation, BvhChannelType.Xrotation, BvhChannelType.Yrotation]);

    /// <summary>
    /// Gets the default non-root joint channel sequence written for BVH files.
    /// </summary>
    public static IReadOnlyList<BvhChannelType> DefaultJointChannelSequence { get; } =
        Array.AsReadOnly([BvhChannelType.Zrotation, BvhChannelType.Xrotation, BvhChannelType.Yrotation]);

    /// <summary>
    /// Creates the default channel sequence for a BVH joint.
    /// </summary>
    /// <param name="includePositionChannels">Whether the returned sequence should include the standard XYZ position channels.</param>
    /// <returns>The default BVH channel sequence.</returns>
    public static BvhChannelType[] CreateDefaultChannelSequence(bool includePositionChannels)
    {
        return includePositionChannels
            ? [.. DefaultRootChannelSequence]
            : [.. DefaultJointChannelSequence];
    }

    /// <summary>
    /// Determines whether the specified channel is a translation channel.
    /// </summary>
    /// <param name="channel">The channel to classify.</param>
    /// <returns><see langword="true"/> when the channel animates position; otherwise, <see langword="false"/>.</returns>
    public static bool IsPositionChannel(BvhChannelType channel)
    {
        return channel is BvhChannelType.Xposition or BvhChannelType.Yposition or BvhChannelType.Zposition;
    }

    /// <summary>
    /// Determines whether the specified channel is a rotation channel.
    /// </summary>
    /// <param name="channel">The channel to classify.</param>
    /// <returns><see langword="true"/> when the channel animates rotation; otherwise, <see langword="false"/>.</returns>
    public static bool IsRotationChannel(BvhChannelType channel)
    {
        return channel is BvhChannelType.Xrotation or BvhChannelType.Yrotation or BvhChannelType.Zrotation;
    }

    /// <summary>
    /// Gets the axis index associated with the supplied position or rotation channel.
    /// </summary>
    /// <param name="channel">The channel whose axis should be returned.</param>
    /// <returns><c>0</c> for X, <c>1</c> for Y, and <c>2</c> for Z.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channel"/> is not a recognised BVH channel.</exception>
    public static int GetAxisIndex(BvhChannelType channel)
    {
        return channel switch
        {
            BvhChannelType.Xposition or BvhChannelType.Xrotation => 0,
            BvhChannelType.Yposition or BvhChannelType.Yrotation => 1,
            BvhChannelType.Zposition or BvhChannelType.Zrotation => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "The supplied BVH channel is not supported."),
        };
    }

    /// <summary>
    /// Gets the canonical BVH token text for the supplied channel.
    /// </summary>
    /// <param name="channel">The channel to convert to text.</param>
    /// <returns>The canonical BVH token for <paramref name="channel"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channel"/> is not a recognised BVH channel.</exception>
    public static string GetChannelName(BvhChannelType channel)
    {
        return channel switch
        {
            BvhChannelType.Xposition => "Xposition",
            BvhChannelType.Yposition => "Yposition",
            BvhChannelType.Zposition => "Zposition",
            BvhChannelType.Xrotation => "Xrotation",
            BvhChannelType.Yrotation => "Yrotation",
            BvhChannelType.Zrotation => "Zrotation",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "The supplied BVH channel is not supported."),
        };
    }

    /// <summary>
    /// Attempts to parse a BVH channel token.
    /// </summary>
    /// <param name="token">The token text to parse.</param>
    /// <param name="channel">The parsed channel when the token is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="token"/> is a supported BVH channel token; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseChannel(ReadOnlySpan<char> token, out BvhChannelType channel)
    {
        if (token.Equals("Xposition", StringComparison.OrdinalIgnoreCase))
        {
            channel = BvhChannelType.Xposition;
            return true;
        }

        if (token.Equals("Yposition", StringComparison.OrdinalIgnoreCase))
        {
            channel = BvhChannelType.Yposition;
            return true;
        }

        if (token.Equals("Zposition", StringComparison.OrdinalIgnoreCase))
        {
            channel = BvhChannelType.Zposition;
            return true;
        }

        if (token.Equals("Xrotation", StringComparison.OrdinalIgnoreCase))
        {
            channel = BvhChannelType.Xrotation;
            return true;
        }

        if (token.Equals("Yrotation", StringComparison.OrdinalIgnoreCase))
        {
            channel = BvhChannelType.Yrotation;
            return true;
        }

        if (token.Equals("Zrotation", StringComparison.OrdinalIgnoreCase))
        {
            channel = BvhChannelType.Zrotation;
            return true;
        }

        channel = default;
        return false;
    }
}
