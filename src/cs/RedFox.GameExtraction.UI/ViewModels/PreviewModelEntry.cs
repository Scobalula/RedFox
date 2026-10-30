using RedFox.GameExtraction.UI.Models;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Represents a model asset included in the preview group.
/// </summary>
public sealed class PreviewModelEntry
{
    /// <summary>
    /// Gets the source asset represented by this entry.
    /// </summary>
    public AssetRowViewModel Asset { get; }

    /// <summary>
    /// Gets the asset name displayed in the preview group.
    /// </summary>
    public string Name => Asset.Name;
    internal Scene? Scene { get; }

    internal PreviewModelEntry(AssetRowViewModel asset, Scene? scene)
    {
        Asset	= asset;
        Scene	= scene;
    }
}
