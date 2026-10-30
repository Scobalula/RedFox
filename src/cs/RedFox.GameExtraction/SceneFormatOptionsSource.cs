using RedFox.Graphics3D.IO;

namespace RedFox.GameExtraction;

/// <summary>
/// Provides the non-internal formats the registered <see cref="SceneTranslatorService"/> can export the specified content to.
/// </summary>
public sealed class SceneFormatOptionsSource : IGameExtractionOptionsSource
{
    private readonly SceneTranslatorContent _content;

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneFormatOptionsSource"/> class.
    /// </summary>
    /// <param name="content">The content the formats must support.</param>
    public SceneFormatOptionsSource(SceneTranslatorContent content)
    {
        _content = content;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetOptions(AssetManager assetManager)
    {
        var translators = assetManager.GetRequiredService<SceneTranslatorService>().Manager.Translators;

        return [.. translators.Where(t => t.CanWrite && !t.IsInternal &&t.Content.HasFlag(_content)).SelectMany(t => t.Extensions).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }
}
