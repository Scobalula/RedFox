namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Represents an EXR box2i (axis-aligned integer bounding box) value.
    /// </summary>
    /// <param name="MinX">Minimum X coordinate (inclusive).</param>
    /// <param name="MinY">Minimum Y coordinate (inclusive).</param>
    /// <param name="MaxX">Maximum X coordinate (inclusive).</param>
    /// <param name="MaxY">Maximum Y coordinate (inclusive).</param>
    public readonly record struct ExrBox2I(int MinX, int MinY, int MaxX, int MaxY);
}
