using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Passes;

namespace RedFox.Tests.Graphics3D;

public sealed class EnvironmentCacheKeyTests
{
    [Fact]
    public void PrefilterMipLevels_CoversFullConfiguredChain()
    {
        Assert.Equal(9, IblPrecomputePass.PrefilterMipLevels);
    }

    [Fact]
    public void Compute_ChangesWhenSourcePathChanges()
    {
        string keyA = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: false,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        string keyB = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "b.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: false,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Compute_ChangesWhenSourceStampChanges()
    {
        string keyA = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: false,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        string keyB = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 2,
            flipY: false,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Compute_ChangesWhenFlipChanges()
    {
        string keyA = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: false,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        string keyB = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: true,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Compute_ChangesWhenProbeSizeChanges()
    {
        string keyA = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: false,
            skySize: 1024,
            skyMipLevels: 11,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        string keyB = EnvironmentCacheKey.Compute(
            sourcePath: Path.Combine(Path.GetTempPath(), "a.exr"),
            sourceLastWriteTimeUtcTicks: 1,
            flipY: false,
            skySize: 512,
            skyMipLevels: 10,
            irradianceSize: 32,
            prefilterSize: 256,
            prefilterMipLevels: IblPrecomputePass.PrefilterMipLevels,
            brdfLutSize: 256);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void ManifestMatch_DoesNotRequirePixelHashForMetadataOnlyLookup()
    {
        EnvironmentCacheManifest loaded = new()
        {
            CacheVersion = EnvironmentCacheKey.CacheVersion,
            Key = "cache-key",
            SourcePath = Path.Combine(Path.GetTempPath(), "a.exr"),
            SourceLastWriteTimeUtcTicks = 7,
            SourcePixelHashHex = "abc",
            FlipY = true,
            SkySize = 1024,
            SkyMipLevels = 11,
            IrradianceSize = 32,
            PrefilterSize = 256,
            PrefilterMipLevels = IblPrecomputePass.PrefilterMipLevels,
            BrdfLutSize = 256,
        };

        EnvironmentCacheManifest expected = new()
        {
            CacheVersion = EnvironmentCacheKey.CacheVersion,
            Key = "cache-key",
            SourcePath = Path.Combine(Path.GetTempPath(), "a.exr"),
            SourceLastWriteTimeUtcTicks = 7,
            SourcePixelHashHex = null,
            FlipY = true,
            SkySize = 1024,
            SkyMipLevels = 11,
            IrradianceSize = 32,
            PrefilterSize = 256,
            PrefilterMipLevels = IblPrecomputePass.PrefilterMipLevels,
            BrdfLutSize = 256,
        };

        Assert.True(IblPrecomputePassManifestProbe.ManifestMatches(loaded, expected));
    }
}
