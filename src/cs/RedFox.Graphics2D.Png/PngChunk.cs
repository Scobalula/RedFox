namespace RedFox.Graphics2D.Png;

internal readonly record struct PngChunk(
    string Type,
    byte[] Data);
