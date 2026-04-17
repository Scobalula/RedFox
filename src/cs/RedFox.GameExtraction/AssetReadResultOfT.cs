namespace RedFox.GameExtraction;

/// <summary>
/// Represents the typed result of reading an asset.
/// </summary>
/// <typeparam name="T">The payload type produced by the read operation.</typeparam>
public sealed class AssetReadResult<T> : AssetReadResult
{
    /// <summary>
    /// Gets the decoded payload returned by the handler.
    /// </summary>
    public required T Data { get; init; }
}
