namespace RedFox.Imaging.Formats.Png;

internal readonly record struct PngHeader(
    int Width,
    int Height,
    byte BitDepth,
    byte ColorType,
    byte InterlaceMethod);
