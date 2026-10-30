namespace RedFox.GameExtraction;

/// <summary>
/// Represents an animation asset whose frame, bone, and shape details can be sorted and searched.
/// </summary>
public class AnimationAsset : Asset
{
    /// <summary>
    /// Gets the number of bones animated.
    /// </summary>
    public int BoneCount { get; init; }

    /// <summary>
    /// Gets the number of frames in the animation.
    /// </summary>
    public int FrameCount { get; init; }

    /// <summary>
    /// Gets the number of blend shapes animated.
    /// </summary>
    public int ShapeCount { get; init; }

    /// <summary>
    /// Gets the playback rate in frames per second.
    /// </summary>
    public float Framerate { get; init; }

    /// <summary>
    /// Gets the names of the bones animated.
    /// </summary>
    public IReadOnlyList<string> BoneNames { get; init; } = [];

    /// <inheritdoc/>
    public override string? Information => $"Frames: {FrameCount}, Framerate: {Framerate}, Bones: {BoneCount}, Shapes: {ShapeCount}";

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationAsset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    public AnimationAsset(string name, string type, object? dataSource) : this(name, type, dataSource, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationAsset"/> class with source metadata and loader-defined user data.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    /// <param name="userData">Optional loader-defined data for consumers such as asset comparers.</param>
    public AnimationAsset(string name, string type, object? dataSource, IReadOnlyDictionary<string, object?>? metadata, object? userData) : base(name, type, dataSource, null, metadata, userData, AssetCategory.Animation)
    {
    }

    /// <inheritdoc/>
    public override bool TryGetSearchValue(string key, out long value)
    {
        switch (key)
        {
            case "bonecount":
                value = BoneCount;
                return true;
            case "framecount":
                value = FrameCount;
                return true;
            case "shapecount":
                value = ShapeCount;
                return true;
            case "framerate":
                value = (long)Framerate;
                return true;
            default:
                return base.TryGetSearchValue(key, out value);
        }
    }

    /// <inheritdoc/>
    public override bool TryGetSearchNames(string key, out IReadOnlyList<string> names)
    {
        if (key == "bonename")
        {
            names = BoneNames;
            return true;
        }

        return base.TryGetSearchNames(key, out names);
    }

    /// <inheritdoc/>
    public override int CompareInformation(Asset other)
    {
        if (other is not AnimationAsset animation)
        {
            return base.CompareInformation(other);
        }

        int frameComparison = FrameCount.CompareTo(animation.FrameCount);
        return frameComparison != 0 ? frameComparison : BoneCount.CompareTo(animation.BoneCount);
    }
}
