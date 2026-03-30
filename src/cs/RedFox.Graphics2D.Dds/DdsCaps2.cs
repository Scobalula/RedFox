namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Extended DDS surface capability flags for cubemaps and volume textures.
    /// </summary>
    [Flags]
    public enum DdsCaps2 : uint
    {
        /// <summary>
        /// No extended capabilities.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates the surface is a cubemap.
        /// </summary>
        Cubemap = 0x200,
        /// <summary>
        /// Cubemap positive X face is present.
        /// </summary>
        CubemapPositiveX = 0x400,

        /// <summary>
        /// Cubemap negative X face is present.
        /// </summary>
        CubemapNegativeX = 0x800,

        /// <summary>
        /// Cubemap positive Y face is present.
        /// </summary>
        CubemapPositiveY = 0x1000,

        /// <summary>
        /// Cubemap negative Y face is present.
        /// </summary>
        CubemapNegativeY = 0x2000,

        /// <summary>
        /// Cubemap positive Z face is present.
        /// </summary>
        CubemapPositiveZ = 0x4000,

        /// <summary>
        /// Cubemap negative Z face is present.
        /// </summary>
        CubemapNegativeZ = 0x8000,

        /// <summary>
        /// All six cubemap faces are present.
        /// </summary>
        CubemapAllFaces = CubemapPositiveX | CubemapNegativeX | CubemapPositiveY | CubemapNegativeY | CubemapPositiveZ | CubemapNegativeZ,

        /// <summary>
        /// Indicates the surface is a volume (3D) texture.
        /// </summary>
        Volume = 0x200000,
    }
}
