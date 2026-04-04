namespace RedFox.Graphics3D.OpenGL;

public sealed class EnvironmentCacheManifest
{
    public int CacheVersion { get; set; }
    public string Key { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;
    public long SourceLastWriteTimeUtcTicks { get; set; }
    public string? SourcePixelHashHex { get; set; }
    public bool FlipY { get; set; }

    public int SkySize { get; set; }
    public int SkyMipLevels { get; set; }
    public int IrradianceSize { get; set; }
    public int PrefilterSize { get; set; }
    public int PrefilterMipLevels { get; set; }
    public int BrdfLutSize { get; set; }
}

