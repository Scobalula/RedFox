using RedFox.GameExtraction;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Creates preconfigured <see cref="AssetManager"/> instances for ZIP-backed template applications.
/// </summary>
public static class TemplateAssetManagerFactory
{
    /// <summary>
    /// Creates an <see cref="AssetManager"/> configured with ZIP mounting, model, animation, image, audio, and raw export support,
    /// and a shared virtual file system service.
    /// </summary>
    /// <returns>A configured asset manager.</returns>
    public static AssetManager Create()
    {
        AssetManager manager = new();
        manager.RegisterService(new AssetFileSystemService(manager));
        manager.RegisterSourceReader(new ZipAssetSourceReader());
        manager.RegisterHandler(new ModelHandler());
        manager.RegisterHandler(new AnimationHandler());
        manager.RegisterHandler(new ImageHandler());
        manager.RegisterHandler(new AudioHandler());
        manager.RegisterHandler(new RawAssetHandler());

        manager.RegisterService<SceneTranslatorService>();
        manager.RegisterService<ImageTranslatorService>();

        return manager;
    }
}