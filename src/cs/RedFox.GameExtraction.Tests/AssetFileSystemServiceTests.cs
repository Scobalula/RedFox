using System.Diagnostics.CodeAnalysis;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Tests;

[TestClass]
public sealed class AssetFileSystemServiceTests
{
    private sealed class MemoryVirtualFile(byte[] bytes) : VirtualFile(bytes.Length)
    {
        public byte[] Bytes { get; } = bytes;

        public override Stream Open() => new MemoryStream(Bytes);
    }

    private sealed class TestAssetSource(string name, Asset[] assets) : IAssetSource
    {
        public string Name { get; } = name;

        public IReadOnlyList<Asset> Assets { get; } = assets;

        public bool TryGetAsset(string path, [NotNullWhen(true)] out Asset? asset)
        {
            asset = Assets.FirstOrDefault(a => a.Name.Equals(path, StringComparison.OrdinalIgnoreCase));
            return asset is not null;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestSourceReader(Func<AssetSourceRequest, AssetManager, IAssetSource> open, Func<AssetSourceRequest, bool>? canOpen = null) : IAssetSourceReader
    {
        public bool CanOpen(AssetSourceRequest request) => canOpen?.Invoke(request) ?? true;

        public Task<IAssetSource> OpenAsync(AssetSourceRequest request, AssetManager assetManager, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            return Task.FromResult(open(request, assetManager));
        }
    }

    private static AssetManager CreateManager()
    {
        AssetManager manager = new();
        manager.RegisterService(new AssetFileSystemService(manager));
        return manager;
    }

    [TestMethod]
    public async Task ReaderAddsFiles_CustomDirectoryStructure_IsReachable()
    {
        AssetManager manager = CreateManager();
        Asset file = new("ignored", "bin", new MemoryVirtualFile([1, 2, 3]));

        manager.RegisterSourceReader(new TestSourceReader((_, assetManager) =>
        {
            AssetFileSystemService service = assetManager.GetRequiredService<AssetFileSystemService>();
            service.AddFile("custom/path/alpha.bin", (VirtualFile)file.DataSource!);

            return new TestAssetSource("test", [file]);
        }));

        IAssetSource source = await manager.MountAsync(AssetSourceRequest.ForFile("test.bin"));

        AssetFileSystemService service = manager.GetRequiredService<AssetFileSystemService>();

        Assert.IsTrue(service.FileSystem.TryGetFile("custom/path/alpha.bin", out VirtualFile? found));
        Assert.AreEqual("alpha.bin", found!.Name);
    }

    [TestMethod]
    public async Task UnloadSource_RemovesFilesAndPrunesEmptyDirectories()
    {
        AssetManager manager = CreateManager();
        Asset file = new("ignored", "bin", new MemoryVirtualFile([1, 2, 3]));

        manager.RegisterSourceReader(new TestSourceReader((_, assetManager) =>
        {
            AssetFileSystemService service = assetManager.GetRequiredService<AssetFileSystemService>();
            service.AddFile("custom/path/alpha.bin", (VirtualFile)file.DataSource!);

            return new TestAssetSource("test", [file]);
        }));

        IAssetSource source = await manager.MountAsync(AssetSourceRequest.ForFile("test.bin"));
        await manager.UnloadAsync(source);

        AssetFileSystemService service = manager.GetRequiredService<AssetFileSystemService>();

        Assert.IsFalse(service.FileSystem.TryGetFile("custom/path/alpha.bin", out _));
        Assert.IsEmpty(service.FileSystem.Root.Directories);
        Assert.IsEmpty(service.FileSystem.Root.Files);
    }

    [TestMethod]
    public async Task ReaderDoesNotUseFileSystem_UnloadLeavesFileSystemEmpty()
    {
        AssetManager manager = CreateManager();
        Asset file = new("ignored", "bin", new MemoryVirtualFile([1, 2, 3]));

        manager.RegisterSourceReader(new TestSourceReader((_, _) => new TestAssetSource("test", [file])));

        IAssetSource source = await manager.MountAsync(AssetSourceRequest.ForFile("test.bin"));
        await manager.UnloadAsync(source);

        AssetFileSystemService service = manager.GetRequiredService<AssetFileSystemService>();

        Assert.IsEmpty(service.FileSystem.Root.Directories);
        Assert.IsEmpty(service.FileSystem.Root.Files);
    }

    [TestMethod]
    public async Task TwoSources_SharedDirectory_UnloadOneLeavesOtherFiles()
    {
        AssetManager manager = CreateManager();
        Asset firstFile = new("first", "bin", new MemoryVirtualFile([1]));
        Asset secondFile = new("second", "bin", new MemoryVirtualFile([2]));

        manager.RegisterSourceReader(new TestSourceReader((request, assetManager) =>
        {
            AssetFileSystemService service = assetManager.GetRequiredService<AssetFileSystemService>();
            service.AddFile("shared/first.bin", (VirtualFile)firstFile.DataSource!);

            return new TestAssetSource("first", [firstFile]);
        }, request => request.Location?.EndsWith("first.bin") == true));

        manager.RegisterSourceReader(new TestSourceReader((request, assetManager) =>
        {
            AssetFileSystemService service = assetManager.GetRequiredService<AssetFileSystemService>();
            service.AddFile("shared/second.bin", (VirtualFile)secondFile.DataSource!);

            return new TestAssetSource("second", [secondFile]);
        }, request => request.Location?.EndsWith("second.bin") == true));

        IAssetSource firstSource = await manager.MountAsync(AssetSourceRequest.ForFile("first.bin"));
        IAssetSource secondSource = await manager.MountAsync(AssetSourceRequest.ForFile("second.bin"));

        await manager.UnloadAsync(firstSource);

        AssetFileSystemService service = manager.GetRequiredService<AssetFileSystemService>();

        Assert.IsFalse(service.FileSystem.TryGetFile("shared/first.bin", out _));
        Assert.IsTrue(service.FileSystem.TryGetFile("shared/second.bin", out VirtualFile? found));
        Assert.AreEqual("second.bin", found!.Name);
    }
}
