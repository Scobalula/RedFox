namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// DDS surface capability flags indicating the complexity of the surface stored in the file.
    /// </summary>
    [Flags]
    public enum DdsCaps : uint
    {
        /// <summary>
        /// Indicates the surface is complex (contains mipmaps, cubemap faces, or multiple array slices).
        /// </summary>
        Complex = 0x8,

        /// <summary>
        /// Indicates the surface contains a mipmap chain.
        /// </summary>
        MipMap = 0x400000,

        /// <summary>
        /// Required capability; indicates this is a texture surface.
        /// </summary>
        Texture = 0x1000,
    }
}
