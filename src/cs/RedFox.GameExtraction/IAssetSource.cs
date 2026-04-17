using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction;

/// <summary>
/// Represents a mounted asset source that exposes discovered assets.
/// </summary>
public interface IAssetSource : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the display name for the mounted source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the assets exposed by the source.
    /// </summary>
    IReadOnlyList<Asset> Assets { get; }

    /// <summary>
    /// Attempts to resolve an asset by virtual path.
    /// </summary>
    /// <param name="path">The virtual path of the asset to resolve.</param>
    /// <param name="asset">The resolved asset when one is found.</param>
    /// <returns><see langword="true"/> when the asset exists; otherwise, <see langword="false"/>.</returns>
    bool TryGetAsset(string path, [NotNullWhen(true)] out Asset? asset);
}
