namespace RedFox.GameExtraction;

/// <summary>
/// The base result of a successful <see cref="IAssetHandler.ReadAsync"/> call.
/// Use <see cref="AssetReadResult{T}"/> to carry strongly-typed decoded data.
/// </summary>
public abstract class AssetReadResult
{
    /// <summary>The entry that was read.</summary>
    public required IAssetEntry Entry { get; init; }

    /// <summary>
    /// Gets whether the read phase was intentionally skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets an optional human-readable skip reason.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Other entries that this asset references (e.g., textures referenced by a model,
    /// or dependent materials). <see langword="null"/> when no references are known.
    /// </summary>
    public IReadOnlyList<IAssetEntry>? References { get; init; }
}

/// <summary>
/// A strongly-typed asset read result carrying decoded asset data of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The decoded asset type (e.g., an image, model, or animation).</typeparam>
public sealed class AssetReadResult<T> : AssetReadResult
{
    /// <summary>The decoded asset data.</summary>
    public required T Data { get; init; }
}

/// <summary>
/// Read result used when an asset read is intentionally skipped.
/// </summary>
public sealed class SkippedAssetReadResult : AssetReadResult
{
}
