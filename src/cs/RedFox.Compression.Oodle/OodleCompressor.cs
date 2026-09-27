

namespace RedFox.Compression.Oodle
{
    /// <summary>
    /// Oodle compressor algorithm.
    /// </summary>
    public enum OodleCompressor
    {
        /// <summary>
        /// Selects the LZH compressor.
        /// </summary>
        LZH       = 0,
        /// <summary>
        /// Selects the LZHLW compressor.
        /// </summary>
        LZHLW     = 1,
        /// <summary>
        /// Selects the LZNIB compressor.
        /// </summary>
        LZNIB     = 2,
        /// <summary>
        /// Selects no compressor.
        /// </summary>
        None      = 3,
        /// <summary>
        /// Selects the LZB16 compressor.
        /// </summary>
        LZB16     = 4,
        /// <summary>
        /// Selects the LZBLW compressor.
        /// </summary>
        LZBLW     = 5,
        /// <summary>
        /// Selects the LZA compressor.
        /// </summary>
        LZA       = 6,
        /// <summary>
        /// Selects the LZNA compressor.
        /// </summary>
        LZNA      = 7,
        /// <summary>
        /// Selects the Kraken compressor.
        /// </summary>
        Kraken    = 8,
        /// <summary>
        /// Selects the Mermaid compressor.
        /// </summary>
        Mermaid   = 9,
        /// <summary>
        /// Selects the BitKnit compressor.
        /// </summary>
        BitKnit   = 10,
        /// <summary>
        /// Selects the Selkie compressor.
        /// </summary>
        Selkie    = 11,
        /// <summary>
        /// Selects the Hydra compressor.
        /// </summary>
        Hydra     = 12,
        /// <summary>
        /// Selects the Leviathan compressor.
        /// </summary>
        Leviathan = 13,
        /// <summary>
        /// Indicates an invalid compressor.
        /// </summary>
        Invalid   = -1,
    }
}
