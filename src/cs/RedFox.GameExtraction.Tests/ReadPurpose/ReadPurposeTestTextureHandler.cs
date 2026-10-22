using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.GameExtraction.Tests;

internal sealed class ReadPurposeTestTextureHandler : TextureHandler
{
    public List<AssetReadPurpose> ReadPurposes { get; } = [];
    public List<MipType> LoadMips { get; } = [];

    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration) => true;

    public override async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        ReadPurposes.Add(context.Purpose);

        if (asset.DataSource is Asset[] children)
        {
            foreach (Asset child in children)
            {
                await context.ReadAsync(child, cancellationToken);
            }
        }

        return await base.ReadAsync(asset, context, cancellationToken);
    }

    public override Image Load(Texture texture, ImageTranslatorManager translatorManager, MipType mipType)
    {
        LoadMips.Add(mipType);
        return new Image(1, 1, 1, 1, 1, ImageFormat.R8Unorm, false, [(byte)mipType]);
    }
}
