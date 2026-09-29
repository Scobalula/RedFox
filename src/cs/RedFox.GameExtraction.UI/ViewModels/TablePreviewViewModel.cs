using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Presents a <see cref="PreviewTable"/> with a text filter across every cell.
/// </summary>
public sealed partial class TablePreviewViewModel : ObservableObject
{
    private readonly PreviewTable _table;

    /// <summary>
    /// Gets the column headers.
    /// </summary>
    public IReadOnlyList<string> Columns => _table.Columns;

    /// <summary>
    /// Gets or sets the text every visible row must contain.
    /// </summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Gets the rows that match the filter.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string[]> Rows { get; private set; }

    /// <summary>
    /// Gets the visible row count display.
    /// </summary>
    [ObservableProperty]
    public partial string CountDisplay { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TablePreviewViewModel"/> class.
    /// </summary>
    /// <param name="table">The table to present.</param>
    public TablePreviewViewModel(PreviewTable table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        Rows = table.Rows;
        CountDisplay = $"{table.Rows.Count:N0} rows";
    }

    partial void OnFilterChanged(string value)
    {
        string filter = value.Trim();
        Rows = filter.Length == 0 ? _table.Rows : [.. _table.Rows.Where(row => Array.Exists(row, cell => cell.Contains(filter, StringComparison.OrdinalIgnoreCase)))];
        CountDisplay = Rows.Count == _table.Rows.Count ? $"{Rows.Count:N0} rows" : $"{Rows.Count:N0} of {_table.Rows.Count:N0} rows";
    }
}
