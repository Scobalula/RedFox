using Avalonia.Controls;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.ViewModels;
using RedFox.Graphics3D;

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
    /// Gets the shared settings and operation options used to read the asset.
    /// </summary>
    public GameExtractionConfiguration Configuration { get; }

    /// <summary>
    /// Gets the currently displayed preview, if any.
    /// </summary>
    public Control? CurrentPreview { get; }

    internal ScenePreviewSettings PreviewSettings { get; }

    /// <summary>
    /// Gets scene bounds prepared off the UI thread for the built-in scene previewer.
    /// </summary>
    public IReadOnlyDictionary<Scene, ScenePreviewBounds> PreparedSceneBounds { get; }

    /// <summary>
    /// Gets scene nodes, animation clips, statistics, and bounds prepared off the UI thread.
    /// </summary>
    public IReadOnlyDictionary<Scene, ScenePreviewData> PreparedSceneData { get; }

    internal AssetPreviewContext(AssetReadResult readResult, AssetManager assetManager, GameExtractionConfiguration configuration, Control? currentPreview, IReadOnlyDictionary<Scene, ScenePreviewData> preparedSceneData, ScenePreviewSettings previewSettings)
    {
        ReadResult = readResult ?? throw new ArgumentNullException(nameof(readResult));
        AssetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        CurrentPreview = currentPreview;
        PreviewSettings = previewSettings ?? throw new ArgumentNullException(nameof(previewSettings));
        PreparedSceneData = preparedSceneData ?? throw new ArgumentNullException(nameof(preparedSceneData));
        Dictionary<Scene, ScenePreviewBounds> preparedBounds = new(ReferenceEqualityComparer.Instance);
        foreach ((Scene scene, ScenePreviewData data) in preparedSceneData)
        {
            preparedBounds[scene] = data.Bounds;
        }

        PreparedSceneBounds = preparedBounds;
    }
}
