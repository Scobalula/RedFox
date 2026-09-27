

namespace RedFox.Compression.Oodle
{
    /// <summary>
    /// Oodle compression level.
    /// </summary>
    public enum OodleLevel
    {
        /// <summary>
        /// Selects the LZH compression level.
        /// </summary>
        LZH       = 0,
        /// <summary>
        /// Selects the LZHLW compression level.
        /// </summary>
        LZHLW     = 1,
        /// <summary>
        /// Selects the LZNIB compression level.
        /// </summary>
        LZNIB     = 2,
        /// <summary>
        /// Selects no compression.
        /// </summary>
        None      = 3,
        /// <summary>
        /// Selects the LZB16 compression level.
        /// </summary>
        LZB16     = 4,
        /// <summary>
        /// Selects the LZBLW compression level.
        /// </summary>
        LZBLW     = 5,
        /// <summary>
        /// Selects the LZA compression level.
        /// </summary>
        LZA       = 6,
        /// <summary>
        /// Selects the LZNA compression level.
        /// </summary>
        LZNA      = 7,
        /// <summary>
        /// Selects the Kraken compression level.
        /// </summary>
        Kraken    = 8,
        /// <summary>
        /// Selects the Mermaid compression level.
        /// </summary>
        Mermaid   = 9,
        /// <summary>
        /// Selects the BitKnit compression level.
        /// </summary>
        BitKnit   = 10,
        /// <summary>
        /// Selects the Selkie compression level.
        /// </summary>
        Selkie    = 11,
        /// <summary>
        /// Selects the Hydra compression level.
        /// </summary>
        Hydra     = 12,
        /// <summary>
        /// Selects the Leviathan compression level.
        /// </summary>
        Leviathan = 13,
        /// <summary>
        /// Indicates an invalid compression level.
        /// </summary>
        Invalid   = -1,
    }
}
