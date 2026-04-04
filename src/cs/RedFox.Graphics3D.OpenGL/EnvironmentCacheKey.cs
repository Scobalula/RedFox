using System.Security.Cryptography;
using System.Text;

namespace RedFox.Graphics3D.OpenGL;

public static class EnvironmentCacheKey
{
    public const int CacheVersion = 8;

    public static string Compute(
        string sourcePath,
        long sourceLastWriteTimeUtcTicks,
        bool flipY,
        int skySize,
        int skyMipLevels,
        int irradianceSize,
        int prefilterSize,
        int prefilterMipLevels,
        int brdfLutSize)
    {
        string normalizedPath = NormalizePath(sourcePath);

        string canonical =
            $"v{CacheVersion}|" +
            $"path:{normalizedPath}|" +
            $"stamp:{sourceLastWriteTimeUtcTicks}|" +
            $"flipY:{(flipY ? 1 : 0)}|" +
            $"sky:{skySize}:{skyMipLevels}|" +
            $"irr:{irradianceSize}|" +
            $"pre:{prefilterSize}:{prefilterMipLevels}|" +
            $"brdf:{brdfLutSize}";

        byte[] bytes = Encoding.UTF8.GetBytes(canonical);
        byte[] digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static string NormalizePath(string value)
    {
        string full = Path.GetFullPath(value);
        return full.Replace('\\', '/').ToLowerInvariant();
    }
}
