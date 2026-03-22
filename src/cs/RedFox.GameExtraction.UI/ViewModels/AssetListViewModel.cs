using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class AssetListViewModel : ObservableObject
{
    private readonly IReadOnlyList<string> _metadataColumns;
    private List<AssetEntryModel> _allAssets = [];

    public AssetListViewModel(IReadOnlyList<string> metadataColumns)
    {
        _metadataColumns = metadataColumns;
    }

    /// <summary>Metadata column names for dynamic columns.</summary>
    public IReadOnlyList<string> MetadataColumns => _metadataColumns;

    /// <summary>Currently displayed (filtered) assets.</summary>
    public ObservableCollection<AssetEntryModel> Assets { get; } = [];

    /// <summary>Currently selected assets.</summary>
    public ObservableCollection<AssetEntryModel> SelectedAssets { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial int FilteredCount { get; set; }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Replace all assets in the list.
    /// </summary>
    public void LoadAssets(IReadOnlyList<IAssetEntry> entries)
    {
        _allAssets = entries.Select(e => new AssetEntryModel(e)).ToList();
        TotalCount = _allAssets.Count;
        SelectedAssets.Clear();
        ApplyFilter();
    }

    /// <summary>
    /// Add assets from a new source (additive — does not clear existing).
    /// </summary>
    public void AddAssets(IReadOnlyList<IAssetEntry> entries)
    {
        var models = entries.Select(e => new AssetEntryModel(e)).ToList();
        _allAssets.AddRange(models);
        TotalCount = _allAssets.Count;
        ApplyFilter();
    }

    /// <summary>
    /// Remove assets belonging to a specific source.
    /// </summary>
    public void RemoveAssets(IReadOnlyList<IAssetEntry> entries)
    {
        var toRemove = new HashSet<IAssetEntry>(entries);
        _allAssets.RemoveAll(m => toRemove.Contains(m.Entry));
        SelectedAssets.Clear();
        TotalCount = _allAssets.Count;
        ApplyFilter();
    }

    /// <summary>
    /// Clear all loaded assets.
    /// </summary>
    public void Clear()
    {
        _allAssets.Clear();
        Assets.Clear();
        SelectedAssets.Clear();
        TotalCount = 0;
        FilteredCount = 0;
    }

    private void ApplyFilter()
    {
        Assets.Clear();

        var query = SearchText.Trim();
        IEnumerable<AssetEntryModel> filtered = _allAssets;

        if (!string.IsNullOrEmpty(query))
        {
            var tokens = ParseSearchTokens(query);
            filtered = _allAssets.Where(a => MatchesAllTokens(a, tokens));
        }

        foreach (var asset in filtered)
            Assets.Add(asset);

        FilteredCount = Assets.Count;
    }

    /// <summary>
    /// Parses the search query into a list of tokens. Supports:
    /// <list type="bullet">
    /// <item><c>key:value</c> — match a specific field (name, type, path, or metadata column)</item>
    /// <item><c>plain text</c> — matches against name, full path, and type</item>
    /// </list>
    /// Multiple tokens are AND-combined.
    /// </summary>
    private static List<SearchToken> ParseSearchTokens(string query)
    {
        var tokens = new List<SearchToken>();
        var span = query.AsSpan();

        int i = 0;
        while (i < span.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(span[i]))
            {
                i++;
                continue;
            }

            // Check for key:value pattern — key is contiguous non-whitespace before ':'
            int tokenStart = i;
            int colonIndex = -1;

            while (i < span.Length && !char.IsWhiteSpace(span[i]))
            {
                if (span[i] == ':' && colonIndex < 0)
                    colonIndex = i;
                i++;
            }

            if (colonIndex > tokenStart && colonIndex < i - 1)
            {
                var key = span[tokenStart..colonIndex].ToString();
                var value = span[(colonIndex + 1)..i].ToString();
                tokens.Add(new SearchToken(key, value));
            }
            else
            {
                tokens.Add(new SearchToken(null, span[tokenStart..i].ToString()));
            }
        }

        return tokens;
    }

    /// <summary>
    /// Returns true if the asset matches all search tokens (AND logic).
    /// </summary>
    private bool MatchesAllTokens(AssetEntryModel asset, List<SearchToken> tokens)
    {
        foreach (var token in tokens)
        {
            if (token.Key is null)
            {
                // Plain text: match against name, full path, or type
                if (!asset.Name.Contains(token.Value, StringComparison.OrdinalIgnoreCase) &&
                    !asset.FullPath.Contains(token.Value, StringComparison.OrdinalIgnoreCase) &&
                    !asset.Type.Contains(token.Value, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                var key = token.Key;
                var value = token.Value;

                if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    if (!asset.Name.Contains(value, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    if (!asset.Type.Contains(value, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (key.Equals("path", StringComparison.OrdinalIgnoreCase))
                {
                    if (!asset.FullPath.Contains(value, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    // Try metadata column match
                    var metadata = asset.GetMetadata(key);
                    if (string.IsNullOrEmpty(metadata) ||
                        !metadata.Contains(value, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
        }

        return true;
    }

    private readonly record struct SearchToken(string? Key, string Value);
}
