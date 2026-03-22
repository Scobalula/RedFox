namespace RedFox.GameExtraction;

/// <summary>
/// Exports assets from a source.
/// </summary>
/// <typeparam name="TAsset">The asset type.</typeparam>
public interface IAssetHandler<in TAsset>
{
    /// <summary>
    /// Exports a single asset using its owning source.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="outputDirectory">The base output directory.</param>
    /// <param name="source">The source contributing the asset.</param>
    void Export(TAsset asset, string outputDirectory, IAssetSource source);
}