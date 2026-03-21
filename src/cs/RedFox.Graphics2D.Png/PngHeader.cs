namespace RedFox.Graphics2D.Png;

internal readonly record struct PngHeader(
    int Width,
    int Height,
    byte BitDepth,
    byte ColorType,
    byte InterlaceMethod);
