using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.IO;

namespace RedFox.GameExtraction;

public sealed class AssetTextureLoader : ITextureLoader
{
    private readonly TextureHandler _handler;

    public AssetReadPurpose Purpose { get; }

    public AssetTextureLoader(TextureHandler handler, AssetReadPurpose purpose)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Purpose = purpose;
    }

    public Image Load(Texture texture, ImageTranslatorManager translatorManager) => Load(texture, translatorManager, Purpose);

    public Image Load(Texture texture, ImageTranslatorManager translatorManager, AssetReadPurpose purpose) => _handler.Load(texture, translatorManager, purpose);
}
