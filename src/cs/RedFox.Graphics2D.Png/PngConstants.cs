namespace RedFox.Graphics2D.Png;

internal static class PngConstants
{
    public static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    public static readonly int[] Adam7StartX = [0, 4, 0, 2, 0, 1, 0];
    public static readonly int[] Adam7StartY = [0, 0, 4, 0, 2, 0, 1];

    public static readonly int[] Adam7StepX = [8, 8, 4, 4, 2, 2, 1];
    public static readonly int[] Adam7StepY = [8, 8, 8, 4, 4, 2, 2];

    public const int MaxChunkWriteSize = 64 * 1024;
    public const int PaletteAnalysisMaxPixels = 262_144;
    public const int FullAdaptiveFilterMaxBytes = 1_048_576;
    public const int FastWriteModeMinPixels = 262_144;
}
