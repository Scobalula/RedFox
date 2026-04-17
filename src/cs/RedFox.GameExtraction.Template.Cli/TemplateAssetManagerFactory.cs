namespace RedFox.GameExtraction.Template.Cli;

/// <summary>
/// Creates preconfigured <see cref="AssetManager"/> instances for the ZIP template CLI.
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
