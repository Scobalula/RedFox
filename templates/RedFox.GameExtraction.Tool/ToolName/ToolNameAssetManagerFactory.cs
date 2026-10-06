using RedFox.GameExtraction;
using ToolName.Handlers;
using ToolName.Sources;

namespace ToolName;

/// <summary>
/// Creates preconfigured <see cref="AssetManager"/> instances for ToolName applications.
/// </summary>
public static class ToolNameAssetManagerFactory
{
    /// <summary>
    /// Creates an <see cref="AssetManager"/> configured with ZIP mounting, the enabled asset handlers, and a shared virtual file system service.
    /// </summary>
    /// <returns>A configured asset manager.</returns>
    public static AssetManager Create()
    {
        AssetManager manager = new();
        manager.RegisterService(new AssetFileSystemService(manager));
        manager.RegisterSourceReader(new ZipAssetSourceReader());

#if MODEL_SUPPORT
        manager.RegisterHandler(new ModelHandler());
        manager.RegisterHandler(new AnimationHandler());
        manager.RegisterService<SceneTranslatorService>();
#endif
#if IMAGE_SUPPORT
        manager.RegisterHandler(new ImageHandler());
        manager.RegisterService<ImageTranslatorService>();
#endif
#if AUDIO_SUPPORT
        manager.RegisterHandler(new AudioHandler());
        manager.RegisterService<AudioTranslatorService>();
#endif

        manager.RegisterHandler(new RawAssetHandler());

        return manager;
    }
}
