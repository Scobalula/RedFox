using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.GameExtraction.Tests;

internal sealed class LegacyReadPurposeTestTextureHandler : TextureHandler
{
    public int LoadCount { get; private set; }

    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration) => true;

    public override Image Load(Texture texture, ImageTranslatorManager translatorManager)
    {
        LoadCount++;
        return new Image(1, 1, 1, 1, 1, ImageFormat.R8Unorm, false, [42]);
    }
}
