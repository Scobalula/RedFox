using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Handles previewing asset content.
/// </summary>
public interface IPreviewHandler
{
    bool CanPreview(IAssetEntry asset);

    Task<PreviewData> GetPreviewAsync(IAssetEntry asset, CancellationToken cancellationToken);
}

[Flags]
public enum PreviewType
{
    None = 0,
    Hex = 1,
    Text = 2,
    Model3D = 4,
    Image = 8
}