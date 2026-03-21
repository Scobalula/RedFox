using System;

namespace RedFox.Graphics2D.Png;

internal readonly ref struct PngDecodePassContext // TODO: This can be made a record?
{
    public readonly PngHeader Header;
    public readonly ReadOnlySpan<byte> Inflated;
    public readonly int StartOffset;
    public readonly int PassWidth;
    public readonly int PassHeight;
    public readonly int StartX;
    public readonly int StartY;
    public readonly int StepX;
    public readonly int StepY;
    public readonly Span<byte> Rgba;
    public readonly byte[]? Palette;
    public readonly byte[]? Transparency;

    public PngDecodePassContext(
        in PngHeader header,
        ReadOnlySpan<byte> inflated,
        int startOffset,
        int passWidth,
        int passHeight,
        int startX,
        int startY,
        int stepX,
        int stepY,
        Span<byte> rgba,
        byte[]? palette,
        byte[]? transparency)
    {
        Header = header;
        Inflated = inflated;
        StartOffset = startOffset;
        PassWidth = passWidth;
        PassHeight = passHeight;
        StartX = startX;
        StartY = startY;
        StepX = stepX;
        StepY = stepY;
        Rgba = rgba;
        Palette = palette;
        Transparency = transparency;
    }
}
