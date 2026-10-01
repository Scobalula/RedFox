using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Presents a core <see cref="Asset"/> as a row in the asset list.
/// </summary>
public sealed class AssetRowViewModel
{
    private readonly string _information;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetRowViewModel"/> class.
    /// </summary>
    /// <param name="asset">The asset represented by the row.</param>
    /// <param name="source">The source row that owns the asset.</param>
    public AssetRowViewModel(Asset asset, AssetSourceViewModel source)
    {
        Asset = asset;
        Source = source;
        Name = asset.Name;
        _information = asset.Information?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Gets the core asset represented by the row.
    /// </summary>
    public Asset Asset { get; }

    /// <summary>
    /// Gets the source row that owns the asset.
    /// </summary>
    public AssetSourceViewModel Source { get; }

    /// <summary>
    /// Gets the source display name.
    /// </summary>
    public string SourceName => Source.DisplayName;

    /// <summary>
    /// Gets the asset file or display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the logical asset type.
    /// </summary>
    public string Type => Asset.Type;

    /// <summary>
    /// Gets the broad content category used for category-specific presentation.
    /// </summary>
    public AssetCategory Category => Asset.Category;

    /// <summary>
    /// Gets a value indicating whether a secondary information line should be shown.
    /// </summary>
    public bool HasInformation => _information.Length > 0;

    /// <summary>
    /// Gets secondary information for the asset.
    /// </summary>
    public string Information => _information;

    /// <summary>
    /// Gets display-friendly metadata values.
    /// </summary>
    public IReadOnlyDictionary<string, string> MetadataDisplay => Asset.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a metadata value for display or search.
    /// </summary>
    /// <param name="column">The metadata column name.</param>
    /// <returns>The metadata value text, or an empty string when no value is present.</returns>
    public string GetMetadata(string column)
    {
        return Asset.Metadata.TryGetValue(column, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

}
