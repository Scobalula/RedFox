namespace RedFox.Graphics2D
{
    /// <summary>
    /// Specifies optional conversion hints for <see cref="Image.Convert(ImageFormat, ImageConvertFlags)"/>.
    /// Flags are best-effort hints; unsupported combinations fall back to the default conversion path.
    /// </summary>
    [Flags]
    public enum ImageConvertFlags
    {
        /// <summary>
        /// Uses the default conversion behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Prefers the faster CPU-only BC6H encoding path when converting into a BC6H target format.
        /// This reduces mode search and may increase compression error compared to the default encoder.
        /// </summary>
        PreferFastBc6HEncoding = 1 << 0,

        /// <summary>
        /// Prefers the faster CPU-only BC7 encoding path when converting into a BC7 target format.
        /// This reduces mode and partition search and may increase compression error compared to the default encoder.
        /// </summary>
        PreferFastBc7Encoding = 1 << 1,

        /// <summary>
        /// Prefers the faster CPU-only BC6H and BC7 encoding paths when applicable.
        /// </summary>
        PreferFastBlockCompression = PreferFastBc6HEncoding | PreferFastBc7Encoding,
    }
}
