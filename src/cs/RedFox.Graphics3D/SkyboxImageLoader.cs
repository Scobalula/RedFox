using System.Security.Cryptography;
using System.Text;
using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.Graphics3D;

// Turns an equirectangular panorama into a prefiltered skybox: the source is halved to a viewport-sized background,
// then each mip is GGX-convolved for one roughness so it serves both rough reflections and background blur.
// Building it takes seconds for an 8K source, so the result is kept in memory and cached to disk as a DDS.
public sealed class SkyboxImageLoader(ImageTranslatorManager translators, string cacheDirectory) : ITextureLoader
{
    private const int MaxWidth = 4096;
    private const int RoughnessLevels = 7;
    private const string CacheVersion = "ggx-2";

    private readonly Lock _sync = new();
    private Image? _image;

    public Image? Load(Texture texture, ImageTranslatorManager translatorManager, MipType mipType)
    {
        lock (_sync)
        {
            return _image ??= LoadCachedOrBuild(texture.EffectiveFilePath);
        }
    }

    private Image LoadCachedOrBuild(string sourcePath)
    {
        string cachePath = GetCachePath(sourcePath);

        if (File.Exists(cachePath))
            return translators.Read(cachePath);

        Image image = Build(sourcePath);
        string temporaryPath = cachePath + ".tmp.dds";

        // The cache only saves time on the next launch, so failing to write it must not lose the skybox.
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            translators.Write(temporaryPath, image);
            File.Move(temporaryPath, cachePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return image;
    }

    private string GetCachePath(string sourcePath)
    {
        FileInfo source = new(sourcePath);
        string key = $"{source.FullName.ToUpperInvariant()}|{source.Length}|{source.LastWriteTimeUtc.Ticks}|{MaxWidth}|{RoughnessLevels}|{CacheVersion}";
        return Path.Combine(cacheDirectory, $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 8)}.dds");
    }

    private Image Build(string sourcePath)
    {
        Image image = translators.Read(sourcePath);

        if (image.Format != ImageFormat.R16G16B16A16Float)
            image.Convert(ImageFormat.R16G16B16A16Float);

        while (image.Width > MaxWidth)
            image = EnvironmentPrefilter.Halve(image);

        return EnvironmentPrefilter.Prefilter(image, RoughnessLevels);
    }
}
