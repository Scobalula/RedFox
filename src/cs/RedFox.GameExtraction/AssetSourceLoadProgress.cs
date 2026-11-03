namespace RedFox.GameExtraction;

/// <summary>
/// Describes how far the source currently being opened has loaded.
/// </summary>
/// <param name="Current">The amount loaded so far.</param>
/// <param name="Total">The total amount to load.</param>
public readonly record struct AssetSourceLoadProgress(long Current, long Total)
{
    /// <summary>
    /// Reports the load progress of the current source when the progress reporter accepts it.
    /// </summary>
    /// <param name="progress">The progress reporter passed to the source reader.</param>
    /// <param name="current">The amount loaded so far.</param>
    /// <param name="total">The total amount to load.</param>
    public static void Report(IProgress<string>? progress, long current, long total)
    {
        if (progress is IProgress<AssetSourceLoadProgress> loadProgress)
        {
            loadProgress.Report(new AssetSourceLoadProgress(current, total));
        }
    }
}
