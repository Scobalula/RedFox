using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.Tests;

[TestClass]
public sealed class AssetReadPurposeTests
{
    [TestMethod]
    [DataRow(AssetReadPurpose.General)]
    [DataRow(AssetReadPurpose.Thumbnail)]
    [DataRow(AssetReadPurpose.Preview)]
    [DataRow(AssetReadPurpose.Export)]
    public async Task ReferencedReadsInheritPurposeAndTextureLoadsUseRequestedMip(AssetReadPurpose purpose)
    {
        ReadPurposeTestTextureHandler handler = new();
        AssetManager manager = await CreateManagerAsync(handler);
        AssetReadResult result = await manager.ReadAsync(manager.Assets[0], new GameExtractionConfiguration(), purpose, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { purpose, purpose, purpose }, handler.ReadPurposes);
        Assert.HasCount(1, result.References);
        Assert.HasCount(1, result.References[0].References);

        Texture texture = result.GetData<Texture>();
        Assert.AreSame(handler, texture.ImageLoader);
        Assert.IsEmpty(handler.LoadMips);

        foreach (MipType mipType in Enum.GetValues<MipType>())
        {
            var image = texture.ImageLoader!.Load(texture, manager.GetRequiredService<ImageTranslatorService>().Manager, mipType);
            Assert.AreEqual((byte)mipType, image!.PixelData[0]);
        }

        CollectionAssert.AreEqual(Enum.GetValues<MipType>(), handler.LoadMips);
    }

    [TestMethod]
    public async Task ExistingReadOverloadUsesGeneralPurpose()
    {
        ReadPurposeTestTextureHandler handler = new();
        AssetManager manager = await CreateManagerAsync(handler);
        await manager.ReadAsync(manager.Assets[0]);

        CollectionAssert.AreEqual(new[] { AssetReadPurpose.General, AssetReadPurpose.General, AssetReadPurpose.General }, handler.ReadPurposes);
    }

    [TestMethod]
    public async Task PurposeOverloadWithoutCancellationTokenIsNotUserData()
    {
        ReadPurposeTestTextureHandler handler = new();
        AssetManager manager = await CreateManagerAsync(handler);
        await manager.ReadAsync(manager.Assets[0], AssetReadPurpose.Thumbnail);

        CollectionAssert.AreEqual(new[] { AssetReadPurpose.Thumbnail, AssetReadPurpose.Thumbnail, AssetReadPurpose.Thumbnail }, handler.ReadPurposes);
    }

    [TestMethod]
    public async Task ExportReadsUseExportPurpose()
    {
        ReadPurposeTestTextureHandler handler = new();
        AssetManager manager = await CreateManagerAsync(handler);
        string directory = Path.Combine(Path.GetTempPath(), "RedFoxReadPurpose", Guid.NewGuid().ToString("N"));
        GameExtractionConfiguration configuration = new();
        configuration.SetOption("OutputDirectory", directory);
        configuration.SetOption("ImageFormats", new[] { ".dds" });

        try
        {
            await manager.ExportAsync(manager.Assets[0], configuration, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { AssetReadPurpose.Export, AssetReadPurpose.Export, AssetReadPurpose.Export }, handler.ReadPurposes);
            CollectionAssert.AreEqual(new[] { MipType.High }, handler.LoadMips);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "root.dds")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task ExportingThumbnailReadRequestsHighMip()
    {
        ReadPurposeTestTextureHandler handler = new();
        AssetManager manager = await CreateManagerAsync(handler);
        AssetReadResult result = await manager.ReadAsync(manager.Assets[0], AssetReadPurpose.Thumbnail, CancellationToken.None);
        Texture texture = result.GetData<Texture>();
        texture.ImageLoader!.Load(texture, manager.GetRequiredService<ImageTranslatorService>().Manager, MipType.Low);
        string directory = Path.Combine(Path.GetTempPath(), "RedFoxReadPurpose", Guid.NewGuid().ToString("N"));
        GameExtractionConfiguration configuration = new();
        configuration.SetOption("OutputDirectory", directory);
        configuration.SetOption("ImageFormats", new[] { ".dds" });

        try
        {
            await manager.ExportAsync(result, configuration, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { MipType.Low, MipType.High }, handler.LoadMips);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "root.dds")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static async Task<AssetManager> CreateManagerAsync(TextureHandler handler)
    {
        Asset leaf = new("leaf", "texture");
        Asset child = new("child", "texture", new[] { leaf });
        Asset root = new("root", "texture", new[] { child });
        AssetManager manager = new();
        manager.RegisterService(new ImageTranslatorService());
        manager.RegisterSourceReader(new ReadPurposeTestSourceReader([root, child, leaf]));
        manager.RegisterHandler(handler);
        await manager.MountFileAsync("read-purpose.test");
        return manager;
    }
}
