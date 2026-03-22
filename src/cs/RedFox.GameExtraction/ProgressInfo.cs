namespace RedFox.GameExtraction;

/// <summary>
/// Reports progress for loading or exporting operations.
/// </summary>
public class ProgressInfo
{
    public int Current { get; init; }

    public int Total { get; init; }

    public string Status { get; init; } = string.Empty;

    public double? Percentage => Total > 0 ? (double)Current / Total : null;
}