using RedFox.GameExtraction;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RedFox.Tests.GameExtraction;

public sealed class AssetManagerTests
{
    [Fact]
    public async Task MountAsync_SelectsReadersForFileDirectoryAndProcessRequests()
    {
        AssetManager manager = new();
        TestAssetSourceReader fileReader = new(
            request => request.Kind == AssetSourceKind.File,
            async (request, _, _, _) =>
            {
                Asset asset = new("file\\asset.bin", "file");
                return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
            });
        TestAssetSourceReader directoryReader = new(
            request => request.Kind == AssetSourceKind.Directory,
            async (request, _, _, _) =>
            {
                Asset asset = new("directory\\asset.bin", "directory");
                return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
            });
        TestAssetSourceReader processReader = new(
            request => request.Kind == AssetSourceKind.Process,
            async (request, _, _, _) =>
            {
                Asset asset = new("process\\asset.bin", "process");
                return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
            });

        manager.RegisterSourceReader(fileReader);
        manager.RegisterSourceReader(directoryReader);
        manager.RegisterSourceReader(processReader);

        await manager.MountFileAsync("game.ff");
        await manager.MountDirectoryAsync("assets");
        await manager.MountProcessAsync(42);

        Assert.Equal(1, fileReader.OpenCount);
        Assert.Equal(1, directoryReader.OpenCount);
        Assert.Equal(1, processReader.OpenCount);
        Assert.Equal(3, manager.Sources.Count);
    }

    [Fact]
    public async Task MountAsync_UsesHeaderBufferToSelectSpecificVersionedReader()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pak");

        try
        {
            await File.WriteAllBytesAsync(tempPath, [0x50, 0x41, 0x4B, 0x00, 0x04, 0x00, 0x00, 0x00]);

            AssetManager manager = new();
            TestAssetSourceReader version3Reader = new(
                request =>
                {
                    if (request.Kind != AssetSourceKind.File || request.Header.Length < 8)
                    {
                        return false;
                    }

                    return MemoryMarshal.Read<uint>(request.HeaderSpan[4..]) == 3;
                },
                async (request, _, _, _) =>
                {
                    Asset asset = new("v3\\asset.bin", "pak-v3");
                    return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
                });
            TestAssetSourceReader version4Reader = new(
                request =>
                {
                    if (request.Kind != AssetSourceKind.File || request.Header.Length < 8)
                    {
                        return false;
                    }

                    return MemoryMarshal.Read<uint>(request.HeaderSpan[4..]) == 4;
                },
                async (request, _, _, _) =>
                {
                    Asset asset = new("v4\\asset.bin", "pak-v4");
                    return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
                });
            TestAssetSourceReader genericReader = new(
                request =>
                    request.Kind == AssetSourceKind.File &&
                    string.Equals(Path.GetExtension(request.Location), ".pak", StringComparison.OrdinalIgnoreCase),
                async (request, _, _, _) =>
                {
                    Asset asset = new("generic\\asset.bin", "generic");
                    return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
                });

            manager.RegisterSourceReader(version4Reader);
            manager.RegisterSourceReader(version3Reader);
            manager.RegisterSourceReader(genericReader);

            IAssetSource source = await manager.MountFileAsync(tempPath);

            Assert.Equal(1, version4Reader.OpenCount);
            Assert.Equal(0, version3Reader.OpenCount);
            Assert.Equal(0, genericReader.OpenCount);
            Assert.Equal("pak-v4", source.Assets.Single().Type);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void Services_ResolveRegisteredInstancesAndFactories()
    {
        AssetManager manager = new();
        TestService instance = new("instance");
        int factoryCalls = 0;

        manager.RegisterService(instance);
        manager.RegisterServiceFactory(() =>
        {
            factoryCalls++;
            return new AnotherTestService("factory");
        });

        Assert.True(manager.TryGetService(out TestService? resolvedInstance));
        Assert.Same(instance, resolvedInstance);

        AnotherTestService first = manager.GetRequiredService<AnotherTestService>();
        AnotherTestService second = manager.GetRequiredService<AnotherTestService>();

        Assert.Equal("factory", first.Name);
        Assert.Same(first, second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task UnloadAsync_RemovesSourceAndDisposesExactlyOnce()
    {
        AssetManager manager = new();
        TestAssetSourceReader reader = new(
            request => request.Kind == AssetSourceKind.File,
            async (request, _, _, _) =>
            {
                Asset asset = new("root/file.bin", "Binary");
                return await Task.FromResult(new TestAssetSource(
                    request.DisplayName,
                    [asset],
                    new Dictionary<string, byte[]> { [NormalizePath(asset.Name)] = [1, 2, 3] }));
            });

        manager.RegisterSourceReader(reader);
        IAssetSource source = await manager.MountFileAsync("pak0.ff");
        TestAssetSource testSource = Assert.IsType<TestAssetSource>(source);

        bool unloaded = await manager.UnloadAsync(source);

        Assert.True(unloaded);
        Assert.Equal(1, testSource.DisposeCount);
        Assert.Empty(manager.Sources);
        Assert.Empty(manager.Assets);
        Assert.DoesNotContain(source, manager.Sources);
    }

    [Fact]
    public async Task Assets_AreTrackedAcrossMountsAndRemovedOnUnload()
    {
        AssetManager manager = new();
        TestAssetSourceReader reader = new(
            request => request.Kind == AssetSourceKind.File,
            async (request, _, _, _) =>
            {
                Asset a = new("first/a.bin", "Data");
                Asset b = new("second/b.bin", "Data");
                return await Task.FromResult(new TestAssetSource(request.DisplayName, [a, b], new Dictionary<string, byte[]>()));
            });

        manager.RegisterSourceReader(reader);

        IAssetSource source1 = await manager.MountFileAsync("one.ff");
        Assert.Equal(2, manager.Assets.Count);

        IAssetSource source2 = await manager.MountFileAsync("two.ff");
        Assert.Equal(4, manager.Assets.Count);

        await manager.UnloadAsync(source1);
        Assert.Equal(2, manager.Assets.Count);

        await manager.UnloadAsync(source2);
        Assert.Empty(manager.Assets);
    }

    [Fact]
    public void FindHandler_CanUseAssetTypeAndMetadata()
    {
        AssetManager manager = new();
        Asset asset = new(
            "hero/model.asset",
            "Scene",
            null,
            null,
            new Dictionary<string, object?> { ["platform"] = "pc" });
        TestAssetHandler wrongHandler = new() { CanHandleDelegate = candidate => candidate.Type == "Image" };
        TestAssetHandler rightHandler = new()
        {
            CanHandleDelegate = candidate =>
                candidate.Type == "Scene" &&
                candidate.Metadata.TryGetValue("platform", out object? value) &&
                string.Equals(value?.ToString(), "pc", StringComparison.OrdinalIgnoreCase)
        };

        manager.RegisterHandler(wrongHandler);
        manager.RegisterHandler(rightHandler);

        IAssetHandler? resolved = manager.FindHandler(asset);

        Assert.Same(rightHandler, resolved);
    }

    [Fact]
    public async Task ReadAsync_ReturnsHandlerResult()
    {
        AssetManager manager = CreateManagerWithSingleAsset(out Asset asset);
        TestAssetHandler handler = new()
        {
            ReadAsyncDelegate = (candidate, _, _) =>
            {
                return Task.FromResult<AssetReadResult>(new AssetReadResult<string>
                {
                    Asset = candidate,
                    Data = candidate.Name,
                });
            }
        };

        manager.RegisterHandler(handler);

        AssetReadResult result = await manager.ReadAsync(asset);
        AssetReadResult<string> typedResult = Assert.IsType<AssetReadResult<string>>(result);

        Assert.Equal("root/file.bin", typedResult.Data);
        Assert.Equal(1, handler.ReadCalls);
    }

    [Fact]
    public async Task ExportAsync_SkipsReadWhenHandlerDecidesExportIsNotNeeded()
    {
        AssetManager manager = CreateManagerWithSingleAsset(out Asset asset);
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string expectedPath = Path.Combine(tempDirectory, "root", "file.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
        await File.WriteAllBytesAsync(expectedPath, [9]);

        TestAssetHandler handler = new()
        {
            ShouldExportAsyncDelegate = (candidate, context, _) =>
            {
                string outputPath = context.ResolveAssetPath(candidate);
                return Task.FromResult(!File.Exists(outputPath) || context.ExportConfiguration.Overwrite);
            }
        };

        manager.RegisterHandler(handler);

        ExportConfiguration configuration = new()
        {
            OutputDirectory = tempDirectory,
            Overwrite = false,
        };

        await manager.ExportAsync(asset, configuration);

        Assert.Equal(1, handler.ShouldExportCalls);
        Assert.Equal(0, handler.ReadCalls);
        Assert.Equal(0, handler.ExportCalls);
    }

    [Fact]
    public async Task ExportAsync_ReturnsPromptlyWhenShouldExportBlocksSynchronously()
    {
        AssetManager manager = CreateManagerWithSingleAsset(out Asset asset);
        using ManualResetEventSlim release = new(false);
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestAssetHandler handler = new()
        {
            ShouldExportAsyncDelegate = (_, _, cancellationToken) =>
            {
                entered.TrySetResult(true);
                release.Wait(cancellationToken);
                return Task.FromResult(true);
            },
            ReadAsyncDelegate = (candidate, _, _) =>
                Task.FromResult<AssetReadResult>(new AssetReadResult<string> { Asset = candidate, Data = "ok" })
        };

        manager.RegisterHandler(handler);

        ExportConfiguration configuration = new()
        {
            OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        };

        Task<Task> invocationTask = Task.Run<Task>(() => manager.ExportAsync(asset, configuration));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task completedInvocation = await Task.WhenAny(invocationTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(invocationTask, completedInvocation);

        Task exportTask = await invocationTask;
        Assert.False(exportTask.IsCompleted);

        release.Set();
        await exportTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ExportAsync_HandlerCanEmitMultipleOutputsFromConfiguredOptions()
    {
        AssetManager manager = CreateManagerWithSingleAsset(out Asset asset);
        List<string> exports = [];
        string? pngQuality = null;
        TestAssetHandler handler = new()
        {
            ReadAsyncDelegate = (candidate, _, _) =>
                Task.FromResult<AssetReadResult>(new AssetReadResult<byte[]> { Asset = candidate, Data = [1, 2, 3] }),
            ExportAsyncDelegate = (result, context, _) =>
            {
                string[] formats = Assert.IsType<string[]>(context.ExportOptions["formats"]);
                foreach (string format in formats)
                {
                    exports.Add(context.ResolveAssetPath(result.Asset, format));
                }

                pngQuality = context.TryGetExportOption("pngQuality", out string? quality) ? quality : null;
                return Task.CompletedTask;
            }
        };

        manager.RegisterHandler(handler);

        await manager.ExportAsync(
            asset,
            new ExportConfiguration
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                Options = new Dictionary<string, object?>
                {
                    ["formats"] = new[] { ".png", ".tga" },
                    ["pngQuality"] = "high",
                },
            });

        Assert.Equal(1, handler.ReadCalls);
        Assert.Equal(1, handler.ExportCalls);
        Assert.Equal(2, exports.Count);
        Assert.Contains(exports, value => value.EndsWith("root\\file.png", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(exports, value => value.EndsWith("root\\file.tga", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("high", pngQuality);
    }

    [Fact]
    public async Task ExportAsync_ExportsReferencesOnceAndPreventsCycles()
    {
        AssetManager manager = new();
        TestAssetSourceReader reader = new(
            request => request.Kind == AssetSourceKind.File,
            async (request, _, _, _) =>
            {
                Asset first = new("root/a.asset", "Scene");
                Asset second = new("root/b.asset", "Scene");

                return await Task.FromResult(new TestAssetSource(
                    request.DisplayName,
                    [first, second],
                    new Dictionary<string, byte[]>
                    {
                        [NormalizePath(first.Name)] = [1],
                        [NormalizePath(second.Name)] = [2],
                    }));
            });

        manager.RegisterSourceReader(reader);
        IAssetSource source = await manager.MountFileAsync("refs.ff");
        Asset a = source.Assets.Single(asset => asset.Name == "root/a.asset");
        Asset b = source.Assets.Single(asset => asset.Name == "root/b.asset");
        List<string> exported = [];

        TestAssetHandler handler = new()
        {
            ReadAsyncDelegate = (candidate, _, _) =>
            {
                IReadOnlyList<AssetExportReference> references = candidate.Name == "root/a.asset"
                    ? [new AssetExportReference(b, "references")]
                    : [new AssetExportReference(a)];
                return Task.FromResult<AssetReadResult>(new AssetReadResult<string>
                {
                    Asset = candidate,
                    Data = candidate.Name,
                    References = references,
                });
            },
            ExportAsyncDelegate = (result, context, _) =>
            {
                exported.Add($"{result.Asset.Name}:{context.RelativeOutputDirectory}");
                return Task.CompletedTask;
            }
        };

        manager.RegisterHandler(handler);

        await manager.ExportAsync(
            a,
            new ExportConfiguration
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                ExportReferences = true,
            });

        Assert.Equal(2, handler.ReadCalls);
        Assert.Equal(2, handler.ExportCalls);
        Assert.Equal(["root/a.asset:", "root/b.asset:references"], exported);
    }

    [Fact]
    public async Task MountAsync_PassesPerMountOptionsToReader()
    {
        AssetManager manager = new();
        IReadOnlyDictionary<string, object?>? seenOptions = null;
        TestAssetSourceReader reader = new(
            _ => true,
            async (request, _, _, _) =>
            {
                seenOptions = request.Options;
                Asset asset = new("root/asset.bin", "Data");
                return await Task.FromResult(new TestAssetSource(request.DisplayName, [asset], new Dictionary<string, byte[]>()));
            });

        manager.RegisterSourceReader(reader);

        await manager.MountFileAsync(
            "options.ff",
            new Dictionary<string, object?> { ["key"] = "abc", ["names"] = new NameList() });

        Assert.NotNull(seenOptions);
        Assert.Equal("abc", seenOptions["key"]);
        Assert.IsType<NameList>(seenOptions["names"]);
    }

    [Fact]
    public async Task Events_FireInDeterministicOrderForSuccessFlow()
    {
        AssetManager manager = CreateManagerWithSingleAsset(out Asset asset, out IAssetSource source);
        List<string> events = [];
        TestAssetHandler handler = new()
        {
            ReadAsyncDelegate = (candidate, _, _) =>
                Task.FromResult<AssetReadResult>(new AssetReadResult<string> { Asset = candidate, Data = "ok" })
        };

        manager.RegisterHandler(handler);
        manager.SourceMounted += (_, args) => events.Add($"source-mounted:{args.Source.Name}");
        manager.SourceUnloading += (_, args) => events.Add($"source-unloading:{args.Source.Name}");
        manager.SourceUnloaded += (_, args) => events.Add($"source-unloaded:{args.Source.Name}");
        manager.AssetExportStarting += (_, args) => events.Add($"export-start:{args.Asset.Name}");
        manager.AssetReadStarting += (_, args) => events.Add($"read-start:{args.Asset.Name}");
        manager.AssetReadCompleted += (_, args) => events.Add($"read-complete:{args.Asset.Name}");
        manager.AssetExportCompleted += (_, args) => events.Add($"export-complete:{args.Asset.Name}:{args.Skipped}");

        events.Add($"source-mounted:{source.Name}");

        await manager.ExportAsync(asset, new ExportConfiguration { OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) });
        await manager.UnloadAsync(source);

        Assert.Equal(
            [
                "source-mounted:single.ff",
                "export-start:root/file.bin",
                "read-start:root/file.bin",
                "read-complete:root/file.bin",
                "export-complete:root/file.bin:False",
                "source-unloading:single.ff",
                "source-unloaded:single.ff",
            ],
            events);
    }

    [Fact]
    public async Task Events_RaiseOperationFailedForExportErrors()
    {
        AssetManager manager = CreateManagerWithSingleAsset(out Asset asset);
        List<string> events = [];
        TestAssetHandler handler = new()
        {
            ReadAsyncDelegate = (candidate, _, _) =>
                Task.FromResult<AssetReadResult>(new AssetReadResult<string> { Asset = candidate, Data = "ok" }),
            ExportAsyncDelegate = (_, _, _) => throw new InvalidOperationException("boom")
        };

        manager.RegisterHandler(handler);
        manager.AssetExportStarting += (_, args) => events.Add($"export-start:{args.Asset.Name}");
        manager.AssetReadStarting += (_, args) => events.Add($"read-start:{args.Asset.Name}");
        manager.AssetReadCompleted += (_, args) => events.Add($"read-complete:{args.Asset.Name}");
        manager.OperationFailed += (_, args) => events.Add($"failed:{args.Operation}:{args.Asset?.Name}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ExportAsync(asset, new ExportConfiguration { OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }));

        Assert.Equal(
            [
                "export-start:root/file.bin",
                "read-start:root/file.bin",
                "read-complete:root/file.bin",
                "failed:Export:root/file.bin",
            ],
            events);
    }

    private static AssetManager CreateManagerWithSingleAsset(out Asset asset)
    {
        AssetManager manager = CreateManagerWithSingleAsset(out asset, out _);
        return manager;
    }

    private static AssetManager CreateManagerWithSingleAsset(out Asset asset, out IAssetSource source)
    {
        AssetManager manager = new();
        TestAssetSourceReader reader = new(
            request => request.Kind == AssetSourceKind.File,
            async (request, _, _, _) =>
            {
                Asset created = new("root/file.bin", "Binary");
                return await Task.FromResult(new TestAssetSource(
                    request.DisplayName,
                    [created],
                    new Dictionary<string, byte[]> { [NormalizePath(created.Name)] = [1, 2, 3] }));
            });

        manager.RegisterSourceReader(reader);
        source = manager.MountFileAsync("single.ff").GetAwaiter().GetResult();
        asset = source.Assets.Single();
        return manager;
    }

    private static string NormalizePath(string path) =>
        string.Join(
            Path.DirectorySeparatorChar,
            path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private sealed class TestService(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class AnotherTestService(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class TestAssetSource : IAssetSource
    {
        private readonly Dictionary<string, Asset> _assetsByPath;
        private readonly Dictionary<string, byte[]> _contentByPath;
        private bool _disposed;

        public TestAssetSource(string name, IReadOnlyList<Asset> assets, IReadOnlyDictionary<string, byte[]> contentByPath)
        {
            Name = name;
            Assets = assets.ToArray();
            _assetsByPath = assets.ToDictionary(asset => NormalizePath(asset.Name), StringComparer.OrdinalIgnoreCase);
            _contentByPath = new Dictionary<string, byte[]>(contentByPath, StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; }

        public IReadOnlyList<Asset> Assets { get; }

        public int DisposeCount { get; private set; }

        public bool TryGetAsset(string path, [NotNullWhen(true)] out Asset? asset) =>
            _assetsByPath.TryGetValue(NormalizePath(path), out asset);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeCount++;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestAssetSourceReader : IAssetSourceReader
    {
        private readonly Func<AssetSourceRequest, bool> _canOpen;
        private readonly Func<AssetSourceRequest, AssetManager, IProgress<string>?, CancellationToken, Task<IAssetSource>> _openAsync;

        public TestAssetSourceReader(
            Func<AssetSourceRequest, bool> canOpen,
            Func<AssetSourceRequest, AssetManager, IProgress<string>?, CancellationToken, Task<IAssetSource>> openAsync)
        {
            _canOpen = canOpen;
            _openAsync = openAsync;
        }

        public int OpenCount { get; private set; }

        public bool CanOpen(AssetSourceRequest request) => _canOpen(request);

        public async Task<IAssetSource> OpenAsync(
            AssetSourceRequest request,
            AssetManager assetManager,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            OpenCount++;
            return await _openAsync(request, assetManager, progress, cancellationToken);
        }
    }

    private sealed class TestAssetHandler : IAssetHandler
    {
        public Func<Asset, bool> CanHandleDelegate { get; init; } = _ => true;

        public Func<Asset, AssetReadContext, CancellationToken, Task<AssetReadResult>> ReadAsyncDelegate { get; init; } =
            (asset, _, _) => Task.FromResult<AssetReadResult>(new AssetReadResult<string> { Asset = asset, Data = asset.Name });

        public Func<Asset, AssetExportContext, CancellationToken, Task<bool>> ShouldExportAsyncDelegate { get; init; } =
            (_, _, _) => Task.FromResult(true);

        public Func<AssetReadResult, AssetExportContext, CancellationToken, Task> ExportAsyncDelegate { get; init; } =
            (_, _, _) => Task.CompletedTask;

        public int ReadCalls { get; private set; }

        public int ShouldExportCalls { get; private set; }

        public int ExportCalls { get; private set; }

        public bool CanHandle(Asset asset) => CanHandleDelegate(asset);

        public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
        {
            ReadCalls++;
            return await ReadAsyncDelegate(asset, context, cancellationToken);
        }

        public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
        {
            ShouldExportCalls++;
            return await ShouldExportAsyncDelegate(asset, context, cancellationToken);
        }

        public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
        {
            ExportCalls++;
            await ExportAsyncDelegate(result, context, cancellationToken);
        }
    }
}
