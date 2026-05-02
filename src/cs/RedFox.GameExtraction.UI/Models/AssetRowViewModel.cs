using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Presents a core <see cref="Asset"/> as a row in the asset list.
/// </summary>
public sealed class AssetRowViewModel
{
    private static readonly string[] SizeMetadataKeys = ["Size", "Length", "UncompressedSize", "CompressedSize"];

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetRowViewModel"/> class.
    /// </summary>
    /// <param name="asset">The asset represented by the row.</param>
    /// <param name="source">The source row that owns the asset.</param>
    public AssetRowViewModel(Asset asset, AssetSourceViewModel source)
    {
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Source = source ?? throw new ArgumentNullException(nameof(source));
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
    public string Name
    {
        get
        {
            string fileName = Path.GetFileName(Asset.Name);
            return string.IsNullOrWhiteSpace(fileName) ? Asset.Name : fileName;
        }
    }

    /// <summary>
    /// Gets the full virtual path for the asset.
    /// </summary>
    public string FullPath => Asset.Name;

    /// <summary>
    /// Gets the logical asset type.
    /// </summary>
    public string Type => Asset.Type;

    /// <summary>
    /// Gets the asset size in bytes when known.
    /// </summary>
    public long? Size => ResolveSize();

    /// <summary>
    /// Gets the display text for the asset size.
    /// </summary>
    public string SizeDisplay => Size is long size ? FormatSize(size) : "-";

    /// <summary>
    /// Gets secondary information for the asset.
    /// </summary>
    public string Information => string.IsNullOrWhiteSpace(Asset.Information) ? SizeDisplay : Asset.Information;

    /// <summary>
    /// Gets display-friendly metadata values.
    /// </summary>
    public IReadOnlyDictionary<string, string> MetadataDisplay => Asset.Metadata.ToDictionary(
        pair => pair.Key,
        pair => pair.Value?.ToString() ?? string.Empty,
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a metadata value for display or search.
    /// </summary>
    /// <param name="column">The metadata column name.</param>
    /// <returns>The metadata value text, or an empty string when no value is present.</returns>
    public string GetMetadata(string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);

        return Asset.Metadata.TryGetValue(column, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    private long? ResolveSize()
    {
        foreach (string key in SizeMetadataKeys)
        {
            if (Asset.Metadata.TryGetValue(key, out object? value) && TryConvertToInt64(value, out long size))
            {
                return size;
            }
        }

        return null;
    }

    private static bool TryConvertToInt64(object? value, out long result)
    {
        result = 0;
        switch (value)
        {
            case long longValue:
                result = longValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                result = (long)ulongValue;
                return true;
            case string text when long.TryParse(text, out long parsed):
                result = parsed;
                return true;
            default:
                return false;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0 ? $"{size:N0} {suffixes[order]}" : $"{size:N2} {suffixes[order]}";
    }
}
