using System.Collections.Generic;

namespace RedFox.Imaging.Formats.Png;

internal readonly record struct PngWriteSelection(byte ColorType, byte BitDepth, byte[]? Palette, byte[]? PaletteAlpha, Dictionary<uint, int>? PaletteIndices);
