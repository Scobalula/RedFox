using Avalonia.Controls;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Provides the read result and current view to an asset previewer.
/// </summary>
public sealed class AssetPreviewContext
{
    /// <summary>
    /// Gets the result returned by the asset loader, including its payload and referenced reads.
    /// </summary>
    public AssetReadResult ReadResult { get; }

    /// <summary>
    /// Gets the payload returned by the asset loader.
    /// </summary>
    public object? Data => ReadResult.Data;

    /// <summary>
    /// Gets the asset manager that produced the read result, for previewers that need its services.
    /// </summary>
    public AssetManager AssetManager { get; }

    /// <summary>
    /// Gets the currently displayed preview, if any.
    /// </summary>
    public Control? CurrentPreview { get; }

    internal AssetPreviewContext(AssetReadResult readResult, AssetManager assetManager, Control? currentPreview)
    {
        ReadResult = readResult ?? throw new ArgumentNullException(nameof(readResult));
        AssetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        CurrentPreview = currentPreview;
    }
}
