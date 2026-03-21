namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Represents an EXR box2i value.
    /// </summary>
    internal readonly record struct ExrBox2I(int MinX, int MinY, int MaxX, int MaxY);
}