namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for failed manager operations.
/// </summary>
/// <param name="operation">The operation that failed.</param>
/// <param name="exception">The exception raised by the operation.</param>
/// <param name="source">The source associated with the failure when available.</param>
/// <param name="asset">The asset associated with the failure when available.</param>
/// <param name="relativeOutputDirectory">The relative output directory associated with the failure when available.</param>
public sealed class AssetOperationFailedEventArgs(AssetOperationKind operation, Exception exception, IAssetSource? source, Asset? asset, string? relativeOutputDirectory) : EventArgs
{
    /// <summary>
    /// Gets the operation that failed.
    /// </summary>
    public AssetOperationKind Operation { get; } = operation;

    /// <summary>
    /// Gets the exception raised by the operation.
    /// </summary>
    public Exception Exception { get; } = exception ?? throw new ArgumentNullException(nameof(exception));

    /// <summary>
    /// Gets the source associated with the failure when available.
    /// </summary>
    public IAssetSource? Source { get; } = source;

    /// <summary>
    /// Gets the asset associated with the failure when available.
    /// </summary>
    public Asset? Asset { get; } = asset;

    /// <summary>
    /// Gets the relative output directory associated with the failure when available.
    /// </summary>
    public string? RelativeOutputDirectory { get; } = relativeOutputDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetOperationFailedEventArgs"/> class without source details.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="exception">The exception raised by the operation.</param>
    public AssetOperationFailedEventArgs(AssetOperationKind operation, Exception exception)
        : this(operation, exception, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetOperationFailedEventArgs"/> class.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="exception">The exception raised by the operation.</param>
    /// <param name="source">The source associated with the failure when available.</param>
    /// <param name="asset">The asset associated with the failure when available.</param>
    /// <param name="relativeOutputDirectory">The relative output directory associated with the failure when available.</param>
}
