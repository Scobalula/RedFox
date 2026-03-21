using System.Collections.Generic;

namespace RedFox.Graphics2D.Png;

internal readonly record struct PngWriteSelection(
    byte ColorType,
    byte BitDepth,
    byte[]? Palette,
    byte[]? PaletteAlpha,
    Dictionary<uint, int>? PaletteIndices);
