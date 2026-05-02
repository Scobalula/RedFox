using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Manages loaded asset rows, search filtering, and selection state.
/// </summary>
public partial class AssetListViewModel : ObservableObject
{
    private readonly List<AssetRowViewModel> _allAssets = [];

    /// <summary>
    /// Gets all loaded asset rows before search filtering.
    /// </summary>
    public IReadOnlyList<AssetRowViewModel> AllAssets => _allAssets;

    /// <summary>
    /// Gets currently displayed asset rows after filtering.
    /// </summary>
    public ObservableCollection<AssetRowViewModel> Assets { get; } = [];

    /// <summary>
    /// Gets currently selected asset rows.
    /// </summary>
    public ObservableCollection<AssetRowViewModel> SelectedAssets { get; } = [];

    /// <summary>
    /// Gets or sets the asset search text.
    /// </summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total loaded asset count.
    /// </summary>
    [ObservableProperty]
    public partial int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the displayed asset count after filtering.
    /// </summary>
    [ObservableProperty]
    public partial int FilteredCount { get; set; }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Adds assets from a mounted source.
    /// </summary>
    /// <param name="source">The source row to add.</param>
    public void AddSource(AssetSourceViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (Asset asset in source.Source.Assets)
        {
            _allAssets.Add(new AssetRowViewModel(asset, source));
        }

        TotalCount = _allAssets.Count;
        ApplyFilter();
    }

    /// <summary>
    /// Removes assets from a mounted source.
    /// </summary>
    /// <param name="source">The source row to remove.</param>
    public void RemoveSource(AssetSourceViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _allAssets.RemoveAll(row => ReferenceEquals(row.Source, source));
        for (int index = SelectedAssets.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(SelectedAssets[index].Source, source))
            {
                SelectedAssets.RemoveAt(index);
            }
        }

        TotalCount = _allAssets.Count;
        ApplyFilter();
    }

    /// <summary>
    /// Clears all loaded assets and selection state.
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

        string query = SearchText.Trim();
        IEnumerable<AssetRowViewModel> filtered = _allAssets;

        if (!string.IsNullOrEmpty(query))
        {
            List<SearchToken> tokens = ParseSearchTokens(query);
            filtered = _allAssets.Where(asset => MatchesAllTokens(asset, tokens));
        }

        foreach (AssetRowViewModel asset in filtered)
        {
            Assets.Add(asset);
        }

        FilteredCount = Assets.Count;
    }

    private static List<SearchToken> ParseSearchTokens(string query)
    {
        List<SearchToken> tokens = [];
        ReadOnlySpan<char> span = query.AsSpan();

        int index = 0;
        while (index < span.Length)
        {
            if (char.IsWhiteSpace(span[index]))
            {
                index++;
                continue;
            }

            int tokenStart = index;
            int colonIndex = -1;

            while (index < span.Length && !char.IsWhiteSpace(span[index]))
            {
                if (span[index] == ':' && colonIndex < 0)
                {
                    colonIndex = index;
                }

                index++;
            }

            if (colonIndex > tokenStart && colonIndex < index - 1)
            {
                string key = span[tokenStart..colonIndex].ToString();
                string value = span[(colonIndex + 1)..index].ToString();
                tokens.Add(new SearchToken(key, value));
            }
            else
            {
                tokens.Add(new SearchToken(null, span[tokenStart..index].ToString()));
            }
        }

        return tokens;
    }

    private static bool MatchesAllTokens(AssetRowViewModel asset, List<SearchToken> tokens)
    {
        foreach (SearchToken token in tokens)
        {
            if (token.Key is null)
            {
                if (!MatchesPlainText(asset, token.Value))
                {
                    return false;
                }
            }
            else if (!MatchesKeyedText(asset, token.Key, token.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesPlainText(AssetRowViewModel asset, string value)
    {
        return asset.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || asset.FullPath.Contains(value, StringComparison.OrdinalIgnoreCase)
            || asset.SourceName.Contains(value, StringComparison.OrdinalIgnoreCase)
            || asset.Type.Contains(value, StringComparison.OrdinalIgnoreCase)
            || asset.Information.Contains(value, StringComparison.OrdinalIgnoreCase)
            || asset.MetadataDisplay.Values.Any(metadata => metadata.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesKeyedText(AssetRowViewModel asset, string key, string value)
    {
        if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
        {
            return asset.Name.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            return asset.Type.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        if (key.Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            return asset.FullPath.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        if (key.Equals("source", StringComparison.OrdinalIgnoreCase))
        {
            return asset.SourceName.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        string metadata = asset.GetMetadata(key);
        return !string.IsNullOrEmpty(metadata) && metadata.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct SearchToken(string? Key, string Value);
}
