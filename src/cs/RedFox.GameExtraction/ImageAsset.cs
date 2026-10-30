namespace RedFox.GameExtraction;

/// <summary>
/// Represents an image asset whose dimensions can be sorted and searched.
/// </summary>
public class ImageAsset : Asset
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <inheritdoc/>
    public override string? Information => $"{Width}x{Height}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageAsset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    public ImageAsset(string name, string type, object? dataSource) : this(name, type, dataSource, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageAsset"/> class with source metadata and loader-defined user data.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    /// <param name="userData">Optional loader-defined data for consumers such as asset comparers.</param>
    public ImageAsset(string name, string type, object? dataSource, IReadOnlyDictionary<string, object?>? metadata, object? userData) : base(name, type, dataSource, null, metadata, userData, AssetCategory.Image)
    {
    }

    /// <inheritdoc/>
    public override bool TryGetSearchValue(string key, out long value)
    {
        switch (key)
        {
            case "width":
                value = Width;
                return true;
            case "height":
                value = Height;
                return true;
            default:
                return base.TryGetSearchValue(key, out value);
        }
    }

    /// <inheritdoc/>
    public override int CompareInformation(Asset other)
    {
        if (other is not ImageAsset image)
        {
            return base.CompareInformation(other);
        }

        int widthComparison = Width.CompareTo(image.Width);
        return widthComparison != 0 ? widthComparison : Height.CompareTo(image.Height);
    }
}
