namespace RedFox.GameExtraction;

/// <summary>
/// Provides the formats the registered <see cref="ImageTranslatorService"/> can export images to, using each translator's primary extension.
/// </summary>
public sealed class ImageFormatOptionsSource : IGameExtractionOptionsSource
{
    /// <inheritdoc/>
    public IReadOnlyList<string> GetOptions(AssetManager assetManager)
    {
        var translators = assetManager.GetRequiredService<ImageTranslatorService>().Manager.Translators;

        return [.. translators.Where(t => t.CanWrite).SelectMany(t => t.Extensions.Take(1)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }
}
