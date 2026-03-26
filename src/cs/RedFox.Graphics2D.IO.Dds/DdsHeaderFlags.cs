namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// DDS header flags indicating which fields in the header contain valid data.
    /// </summary>
    [Flags]
    public enum DdsHeaderFlags : uint
    {
        /// <summary>
        /// The <c>Caps</c> field is valid.
        /// </summary>
        Caps = 0x1,

        /// <summary>
        /// The <c>Height</c> field is valid.
        /// </summary>
        Height = 0x2,

        /// <summary>
        /// The <c>Width</c> field is valid.
        /// </summary>
        Width = 0x4,

        /// <summary>
        /// The <c>PitchOrLinearSize</c> field contains a row pitch value.
        /// </summary>
        Pitch = 0x8,

        /// <summary>
        /// The <c>PixelFormat</c> field is valid.
        /// </summary>
        PixelFormat = 0x1000,

        /// <summary>
        /// The <c>MipMapCount</c> field is valid.
        /// </summary>
        MipMapCount = 0x20000,

        /// <summary>
        /// The <c>PitchOrLinearSize</c> field contains a linear size value.
        /// </summary>
        LinearSize = 0x80000,

        /// <summary>
        /// The <c>Depth</c> field is valid for a volume texture.
        /// </summary>
        Depth = 0x800000,
    }
}
