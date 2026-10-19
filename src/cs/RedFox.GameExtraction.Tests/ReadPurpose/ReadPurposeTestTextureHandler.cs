using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.GameExtraction.Tests;

internal sealed class ReadPurposeTestTextureHandler : TextureHandler
{
    public List<AssetReadPurpose> ReadPurposes { get; } = [];
    public List<AssetReadPurpose> LoadPurposes { get; } = [];

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

    public override Image Load(Texture texture, ImageTranslatorManager translatorManager) => throw new InvalidOperationException("Expected a read purpose.");

    public override Image Load(Texture texture, ImageTranslatorManager translatorManager, AssetReadPurpose purpose)
    {
        LoadPurposes.Add(purpose);
        return new Image(1, 1, 1, 1, 1, ImageFormat.R8Unorm, false, [(byte)purpose]);
    }
}
