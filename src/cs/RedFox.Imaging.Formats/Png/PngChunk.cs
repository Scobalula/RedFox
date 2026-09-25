namespace RedFox.Imaging.Formats.Png;

internal readonly record struct PngChunk(
    string Type,
    byte[] Data);
