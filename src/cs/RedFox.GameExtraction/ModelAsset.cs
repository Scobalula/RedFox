namespace RedFox.GameExtraction;

/// <summary>
/// Represents a model asset whose bone and LOD details can be sorted and searched.
/// </summary>
public class ModelAsset : Asset
{
    /// <summary>
    /// Gets the number of bones in the model.
    /// </summary>
    public int BoneCount { get; init; }

    /// <summary>
    /// Gets the number of levels of detail in the model.
    /// </summary>
    public int LodCount { get; init; }

    /// <summary>
    /// Gets the names of the bones in the model.
    /// </summary>
    public IReadOnlyList<string> BoneNames { get; init; } = [];

    /// <inheritdoc/>
    public override string? Information => $"Bones: {BoneCount}, LODs: {LodCount}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelAsset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    public ModelAsset(string name, string type, object? dataSource) : this(name, type, dataSource, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelAsset"/> class with source metadata and loader-defined user data.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    /// <param name="userData">Optional loader-defined data for consumers such as asset comparers.</param>
    public ModelAsset(string name, string type, object? dataSource, IReadOnlyDictionary<string, object?>? metadata, object? userData) : base(name, type, dataSource, null, metadata, userData, AssetCategory.Model)
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
            case "lodcount":
                value = LodCount;
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
        if (other is not ModelAsset model)
        {
            return base.CompareInformation(other);
        }

        int boneComparison = BoneCount.CompareTo(model.BoneCount);
        return boneComparison != 0 ? boneComparison : LodCount.CompareTo(model.LodCount);
    }
}
