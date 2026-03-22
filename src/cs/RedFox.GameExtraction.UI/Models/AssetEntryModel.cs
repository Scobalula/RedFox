using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// View-friendly model wrapping an <see cref="IAssetEntry"/> for display in the asset list.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AssetEntryModel class using the specified asset entry.
/// </remarks>
/// <param name="entry">The asset entry to be wrapped by the model.</param>
public class AssetEntryModel(IAssetEntry entry)
{
    private readonly IAssetEntry _entry = entry;

    /// <summary>
    /// Gets the asset associated with the current entry.
    /// </summary>
    public IAssetEntry Entry => _entry;

    /// <summary>
    /// Gets the name associated with the current entry.
    /// </summary>
    public string Name => _entry.Name;

    /// <summary>
    /// Gets the full file path associated with the current entry.
    /// </summary>
    public string FullPath => _entry.FullPath;

    /// <summary>
    /// Gets the type name associated with the current entry.
    /// </summary>
    public string Type => _entry.Type;

    /// <summary>
    /// Gets the size of the entry, in bytes, if known.
    /// </summary>
    public long? Size => _entry.Size;

    /// <summary>
    /// Icon for display. Falls back to a generic file icon.
    /// </summary>
    public string Icon => _entry.Icon ?? "📄";

    /// <summary>
    /// Gets a human-readable string that represents the size of the entry, or a placeholder if the size is unavailable.
    /// </summary>
    public string SizeDisplay => _entry.Size.HasValue ? FormatSize(_entry.Size.Value) : "—";

    /// <summary>
    /// Gets the information text to display. Falls back to <see cref="SizeDisplay"/> if not set.
    /// </summary>
    public string Information => _entry.Information ?? SizeDisplay;

    /// <summary>
    /// Get a metadata value by column name.
    /// </summary>
    public string GetMetadata(string column)
    {
        if (_entry.Metadata is not null && _entry.Metadata.TryGetValue(column, out var value))
            return value?.ToString() ?? string.Empty;
        return string.Empty;
    }

    /// <summary>
    /// Returns all metadata as a display-friendly dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> MetadataDisplay
    {
        get
        {
            if (_entry.Metadata is null)
                return new Dictionary<string, string>();

            return _entry.Metadata.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString() ?? string.Empty);
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
