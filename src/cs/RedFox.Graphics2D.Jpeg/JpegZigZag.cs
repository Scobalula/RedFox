namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Provides the standard JPEG zig-zag scan order for mapping between 8×8 spatial and linear coefficient indices.
/// </summary>
public static class JpegZigZag
{
    /// <summary>
    /// Gets the 64-element zig-zag scan order. Index <c>i</c> yields the spatial position of the <c>i</c>-th coefficient.
    /// </summary>
    public static ReadOnlySpan<byte> Order =>
    [
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    ];
}
