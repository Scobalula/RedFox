namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Identifies the type of resource described by the DX10 extended DDS header.
    /// </summary>
    public enum DdsResourceDimension : uint
    {
        /// <summary>
        /// Resource dimension is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Resource is a buffer.
        /// </summary>
        Buffer = 1,

        /// <summary>
        /// Resource is a 1D texture.
        /// </summary>
        Texture1D = 2,

        /// <summary>
        /// Resource is a 2D texture (or cubemap).
        /// </summary>
        Texture2D = 3,

        /// <summary>
        /// Resource is a 3D (volume) texture.
        /// </summary>
        Texture3D = 4,
    }
}
