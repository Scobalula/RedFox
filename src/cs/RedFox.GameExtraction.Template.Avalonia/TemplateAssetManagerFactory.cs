using RedFox.GameExtraction;

namespace RedFox.GameExtraction.Template.Avalonia;

/// <summary>
/// Creates preconfigured <see cref="AssetManager"/> instances for the ZIP Avalonia template.
/// </summary>
public static class TemplateAssetManagerFactory
{
    /// <summary>
    /// Creates an <see cref="AssetManager"/> configured with ZIP mounting, raw export support,
    /// and a shared virtual file system service.
    /// </summary>
    /// <returns>A configured asset manager.</returns>
    public static AssetManager Create()
    {
        AssetManager manager = new();
        manager.RegisterService(new AssetFileSystemService());
        manager.RegisterSourceReader(new ZipAssetSourceReader());
        manager.RegisterHandler(new RawAssetHandler());
        return manager;
    }
}
