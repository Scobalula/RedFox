using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Creates a view for a decoded asset payload when the previewer supports it.
/// </summary>
public interface IAssetPreviewer
{
    /// <summary>Gets the settings this previewer exposes in the application settings window.</summary>
    IReadOnlyList<GameExtractionSetting> SettingDefinitions => [];

    /// <summary>
    /// Attempts to create a preview for the supplied read result.
    /// </summary>
    /// <param name="context">The asset read result and current preview context.</param>
    /// <param name="preview">The created view when the payload is supported.</param>
    /// <returns><see langword="true"/> when this previewer created a view.</returns>
    bool TryCreatePreview(AssetPreviewContext context, [NotNullWhen(true)] out Control? preview);
}
