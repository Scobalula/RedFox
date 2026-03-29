namespace RedFox.GameExtraction;

/// <summary>
/// An <see cref="IAssetEntry"/> paired with the <see cref="IAssetHandler"/> responsible
/// for reading and exporting it, as resolved by the <see cref="AssetManager"/>.
/// </summary>
public sealed class Asset
{
    /// <summary>The underlying entry containing asset metadata.</summary>
    public required IAssetEntry Entry { get; init; }

    /// <summary>
    /// The handler that can read and export this asset.
    /// <see langword="null"/> when no registered handler supports the entry's type.
    /// </summary>
    public IAssetHandler? Handler { get; init; }

    /// <summary>
    /// Whether a handler is available for this asset.
    /// </summary>
    public bool IsSupported => Handler is not null;
}
