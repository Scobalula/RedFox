using System.Collections.ObjectModel;
using System.IO.Enumeration;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides the filtered assets of a source container for the source browser.
/// </summary>
public partial class SourceBrowserViewModel : ObservableObject
{
    private readonly IReadOnlyList<Asset> _assets;
    private readonly RangeObservableCollection<Asset> _filteredAssets = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceBrowserViewModel"/> class.
    /// </summary>
    /// <param name="sourceName">The name of the source being browsed.</param>
    /// <param name="assets">The assets that can be mounted from the source.</param>
    public SourceBrowserViewModel(string sourceName, IReadOnlyList<Asset> assets)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        Title = $"Browse {sourceName}";
        ApplyFilter();
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the assets that match the filter.
    /// </summary>
    public ObservableCollection<Asset> Assets => _filteredAssets;

    /// <summary>
    /// Gets or sets the filter text. Every term must match a name, and terms with wildcards match it whole.
    /// </summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of selected assets.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad), nameof(LoadText), nameof(Summary))]
    public partial int SelectedCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether any assets are selected to load.
    /// </summary>
    public bool CanLoad => SelectedCount > 0;

    /// <summary>
    /// Gets the load button text.
    /// </summary>
    public string LoadText => SelectedCount > 1 ? $"Load {SelectedCount:N0}" : "Load";

    /// <summary>
    /// Gets a summary of the filtered and selected assets.
    /// </summary>
    public string Summary => $"{Assets.Count:N0} of {_assets.Count:N0} shown, {SelectedCount:N0} selected";

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string[] terms = FilterText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        _filteredAssets.ReplaceAll(_assets.Where(asset => terms.All(term => Matches(asset.Name, term))));
        OnPropertyChanged(nameof(Summary));
    }

    private static bool Matches(string name, string term)
    {
        if (term.Contains('*') || term.Contains('?'))
        {
            return FileSystemName.MatchesSimpleExpression(term, name, ignoreCase: true);
        }

        return name.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
