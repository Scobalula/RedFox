namespace RedFox.GameExtraction;

/// <summary>
/// Provides the available values for a setting from the asset manager.
/// </summary>
public interface IGameExtractionOptionsSource
{
    /// <summary>
    /// Gets the available values for the setting.
    /// </summary>
    /// <param name="assetManager">The asset manager that owns the services the values are resolved from.</param>
    /// <returns>The available values.</returns>
    IReadOnlyList<string> GetOptions(AssetManager assetManager);
}
