namespace RedFox.Graphics2D.Png;

internal readonly record struct PngColorModelInfo(
    bool IsGrayscale,
    bool IsOpaque,
    bool CanPalette,
    Dictionary<uint, int> PaletteLookup,
    List<uint> PaletteColors);